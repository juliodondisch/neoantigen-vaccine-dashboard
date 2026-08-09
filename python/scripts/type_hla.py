#!/usr/bin/env python3
"""Determine the patient's HLA class I alleles from normal-tissue reads via OptiType.

TEMP-PATCH: this pipeline (extract -> downsample -> FASTQ -> OptiType) and the OptiType CLI
form below were derived from a real deployment against genome-aligned WGS BAMs
(docs/CORRECTION_PLAN.md §4) but the exact `optitype run` flags are still only as verified as
that one server pass — different OptiType install methods (pip vs. source vs. conda) have been
observed to expose different CLIs historically. Re-check with `optitype run --help` if this
fails on a fresh install.

Local development instead uses FixtureSeeder.SeedHlaTypingAsync, which writes a known 6-allele
fixture directly.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", ".."))
from python.common import io_utils
from python.common.config import ToolConfig
from python.common.response import emit_failure, emit_success, log

ALLELE_RE = re.compile(r"^HLA-[A-C]\*\d{2}:\d{2}$")

# Classical HLA class I locus (HLA-A/B/C) on GRCh38, "chr"-prefixed naming. If the reference in
# use has bare "6" naming instead, override via --hla-region.
HLA_REGION = "chr6:29000000-33000000"
HLA_REGION_BP = 4_000_000
TARGET_DEPTH = 30  # OptiType is validated at typical 30-60x sequencing depth, not 150x+ WGS
ASSUMED_READ_LENGTH_BP = 150


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser()
    p.add_argument("--input", required=True)
    p.add_argument("--output-dir", required=True)
    p.add_argument("--output-json", required=True)
    p.add_argument("--is-bam", default="false")
    p.add_argument("--include-class-ii", default="false")
    p.add_argument("--hla-region", default=HLA_REGION)
    p.add_argument("--target-depth", type=int, default=TARGET_DEPTH)
    return p.parse_args()


def extract_hla_reads(bam_path: str, hla_region: str, output_bam: str) -> None:
    tools = ToolConfig.from_env()
    io_utils.run_command(
        [tools.samtools, "view", "-b", "-o", output_bam, bam_path, hla_region],
        "extract HLA-region reads",
    )
    io_utils.run_command([tools.samtools, "index", output_bam], "index HLA-region BAM")


def count_reads(bam_path: str) -> int:
    tools = ToolConfig.from_env()
    result = io_utils.run_command([tools.samtools, "view", "-c", bam_path], "count HLA-region reads")
    return int(result.stdout.strip() or "0")


def estimate_depth(read_count: int, region_bp: int, read_length_bp: int = ASSUMED_READ_LENGTH_BP) -> float:
    return (read_count * read_length_bp) / region_bp if region_bp else 0.0


def downsample_bam(bam_path: str, fraction: float, output_bam: str, seed: int = 42) -> None:
    tools = ToolConfig.from_env()
    # samtools view -s SEED.FRAC: the seed and fraction are combined into one decimal, e.g.
    # "42.030" means seed 42, keep-fraction 0.030.
    frac_str = f"{seed}.{int(round(fraction * 1000)):03d}"
    io_utils.run_command(
        [tools.samtools, "view", "-b", "-s", frac_str, "-o", output_bam, bam_path],
        "downsample HLA-region reads",
    )


def bam_to_fastq(bam_path: str, output_prefix: str) -> tuple[str, str | None]:
    """Convert a BAM to FASTQ so OptiType performs its own mapping.

    OptiType skips its internal razers3/yara mapping stage when given a BAM, and instead reads
    the existing alignment as if it were alignment against its own HLA allele reference. Our
    BAMs are aligned against whole-genome GRCh38, so that assumption is wrong and produces
    malformed allele IDs (observed on real data: KeyError '1'). Handing it FASTQ forces the
    correct, self-mapping code path.
    """
    tools = ToolConfig.from_env()
    r1 = f"{output_prefix}_R1.fastq"
    r2 = f"{output_prefix}_R2.fastq"
    io_utils.run_command(
        [tools.samtools, "fastq", "-n", "-1", r1, "-2", r2, "-0", os.devnull, "-s", os.devnull, bam_path],
        "convert HLA-region BAM to FASTQ",
    )
    if os.path.getsize(r2) == 0:
        os.remove(r2)
        return r1, None
    return r1, r2


def run_optitype(fastq_r1: str, fastq_r2: str | None, output_dir: str) -> str:
    tools = ToolConfig.from_env()
    io_utils.ensure_dir(output_dir)
    fastq_args = [fastq_r1, fastq_r2] if fastq_r2 else [fastq_r1]
    cmd = [tools.optitype, "run", "-i", *fastq_args, "--dna", "-o", output_dir, "--verbose"]
    io_utils.run_command(cmd, "OptiType", timeout=3600)
    for root, _dirs, files in os.walk(output_dir):
        for fname in files:
            if fname.endswith("_result.tsv"):
                return os.path.join(root, fname)
    raise RuntimeError("OptiType did not produce a _result.tsv output")


def parse_optitype_output(tsv_path: str) -> dict:
    rows = io_utils.read_tsv(tsv_path)
    if not rows:
        raise RuntimeError(f"OptiType output {tsv_path} was empty")
    row = rows[0]
    alleles = []
    for col in ("A1", "A2", "B1", "B2", "C1", "C2"):
        if row.get(col):
            alleles.append(normalize_allele(row[col]))
    return {"classIAlleles": alleles}


def normalize_allele(raw: str) -> str:
    raw = raw.strip()
    return raw if raw.startswith("HLA-") else f"HLA-{raw}"


def validate_alleles(alleles: list[str]) -> tuple[bool, list[str]]:
    invalid = [a for a in alleles if not ALLELE_RE.match(a)]
    return (len(invalid) == 0, invalid)


def main() -> None:
    args = parse_args()
    is_bam = str(args.is_bam).lower() in ("1", "true", "yes")
    include_class_ii = str(args.include_class_ii).lower() in ("1", "true", "yes")
    io_utils.ensure_dir(args.output_dir)

    try:
        io_utils.check_file_exists(args.input, "HLA typing input")

        if is_bam:
            region_bam = os.path.join(args.output_dir, "hla_region_reads.bam")
            extract_hla_reads(args.input, args.hla_region, region_bam)

            read_count = count_reads(region_bam)
            est_depth = estimate_depth(read_count, HLA_REGION_BP)
            log(f"Extracted {read_count} reads in {args.hla_region} (~{est_depth:.0f}x estimated depth)")

            if est_depth > args.target_depth * 1.15:
                fraction = args.target_depth / est_depth
                downsampled_bam = os.path.join(args.output_dir, "hla_region_downsampled.bam")
                downsample_bam(region_bam, fraction, downsampled_bam)
                log(f"Downsampled to fraction {fraction:.3f} (target ~{args.target_depth}x)")
                region_bam = downsampled_bam

            fastq_r1, fastq_r2 = bam_to_fastq(region_bam, os.path.join(args.output_dir, "hla_reads"))
            result_tsv = run_optitype(fastq_r1, fastq_r2, args.output_dir)
        else:
            # Already FASTQ — hand straight to OptiType.
            result_tsv = run_optitype(args.input, None, args.output_dir)

        profile = parse_optitype_output(result_tsv)
        valid, invalid = validate_alleles(profile["classIAlleles"])
        if not valid:
            raise RuntimeError(f"OptiType produced malformed allele(s): {invalid}")

        profile["classIIAlleles"] = []
        profile["confidence"] = {}
        profile["source"] = "OptiType"
        with open(args.output_json, "w") as fh:
            json.dump(profile, fh, indent=2)
    except Exception as exc:  # noqa: BLE001
        emit_failure(str(exc))
        return

    emit_success(
        f"Typed {len(profile['classIAlleles'])} class I alleles",
        [args.output_json],
        {"alleleCount": len(profile["classIAlleles"]), "alleles": profile["classIAlleles"]},
    )


if __name__ == "__main__":
    main()
