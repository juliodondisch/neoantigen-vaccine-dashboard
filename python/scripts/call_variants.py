#!/usr/bin/env python3
"""Call somatic mutations with Mutect2 (GATK), tumor vs. matched normal.

TEMP-PATCH: Mutect2 flag shapes are unverified against a real GATK install
(none present on this dev machine) per CLAUDE.md's "do not guess external
interfaces" rule; deferred to server pass. Local fixture testing goes through
FixtureSeeder's hand-written VCF instead of running this script for real.
"""
from __future__ import annotations

import argparse
import os
import statistics
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", ".."))
from python.common import io_utils
from python.common.config import ToolConfig
from python.common.response import emit_failure, emit_success, log


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser()
    p.add_argument("--tumor-bam", required=True)
    p.add_argument("--normal-bam", required=True)
    p.add_argument("--reference", required=True)
    p.add_argument("--output-vcf", required=True)
    p.add_argument("--panel-of-normals", default="")
    p.add_argument("--intervals", default="")
    p.add_argument("--min-vaf", type=float, default=0.05)
    return p.parse_args()


def get_normal_sample_name(bam_path: str) -> str:
    tools = ToolConfig.from_env()
    result = io_utils.run_command([tools.samtools, "view", "-H", bam_path], "read BAM header")
    for line in result.stdout.splitlines():
        if line.startswith("@RG"):
            for field in line.split("\t"):
                if field.startswith("SM:"):
                    return field[3:]
    raise RuntimeError(f"No @RG SM: sample name found in {bam_path}")


def run_mutect2(tumor_bam: str, normal_bam: str, reference: str, output_vcf: str, pon: str | None, intervals: str | None) -> None:
    tools = ToolConfig.from_env()
    normal_sample = get_normal_sample_name(normal_bam)
    cmd = [
        tools.require("gatk"), "Mutect2",
        "-R", reference, "-I", tumor_bam, "-I", normal_bam,
        "-normal", normal_sample, "-O", output_vcf,
    ]
    if pon:
        cmd += ["--panel-of-normals", pon]
    if intervals:
        cmd += ["-L", intervals]
    io_utils.run_command(cmd, "Mutect2")


def filter_calls(raw_vcf: str, reference: str, output_vcf: str) -> None:
    tools = ToolConfig.from_env()
    io_utils.run_command(
        [tools.gatk, "FilterMutectCalls", "-R", reference, "-V", raw_vcf, "-O", output_vcf],
        "FilterMutectCalls",
    )


def extract_pass_variants(vcf_path: str, output_path: str, min_vaf: float) -> int:
    kept = []
    for record in io_utils.read_vcf(vcf_path):
        if record.get("FILTER") not in ("PASS", "."):
            continue
        vaf = _parse_vaf(record.get("INFO", ""))
        if vaf is not None and vaf < min_vaf:
            continue
        kept.append(record)
    io_utils.write_vcf(output_path, kept, "##fileformat=VCFv4.2\n#CHROM\tPOS\tID\tREF\tALT\tQUAL\tFILTER\tINFO")
    return len(kept)


def compute_vaf_distribution(vcf_path: str) -> list[float]:
    vafs = []
    for record in io_utils.read_vcf(vcf_path):
        vaf = _parse_vaf(record.get("INFO", ""))
        if vaf is not None:
            vafs.append(vaf)
    return vafs


def summarize_filters(vcf_path: str) -> dict[str, int]:
    counts: dict[str, int] = {}
    for record in io_utils.read_vcf(vcf_path):
        filt = record.get("FILTER", "unknown") or "unknown"
        counts[filt] = counts.get(filt, 0) + 1
    return counts


def _parse_vaf(info: str) -> float | None:
    for part in info.split(";"):
        if part.startswith("VAF="):
            try:
                return float(part[4:])
            except ValueError:
                return None
    return None


def main() -> None:
    args = parse_args()
    try:
        io_utils.check_file_exists(args.tumor_bam, "Tumor BAM")
        io_utils.check_file_exists(args.normal_bam, "Normal BAM")
        io_utils.check_file_exists(args.reference, "Reference genome")

        raw_vcf = args.output_vcf.replace(".vcf.gz", ".raw.vcf.gz")
        run_mutect2(args.tumor_bam, args.normal_bam, args.reference, raw_vcf, args.panel_of_normals or None, args.intervals or None)
        filter_calls(raw_vcf, args.reference, args.output_vcf)

        pass_path = args.output_vcf.replace(".vcf.gz", "").replace("somatic", "somatic_pass") + ".vcf.gz"
        pass_count = extract_pass_variants(args.output_vcf, pass_path, args.min_vaf)
        vafs = compute_vaf_distribution(pass_path)
        filter_reasons = summarize_filters(args.output_vcf)

        summary = {
            "totalVariants": sum(filter_reasons.values()),
            "passVariants": pass_count,
            "filteredVariants": sum(filter_reasons.values()) - pass_count,
            "filterReasons": filter_reasons,
            "vafDistribution": vafs,
            "medianVaf": statistics.median(vafs) if vafs else 0.0,
        }
    except Exception as exc:  # noqa: BLE001
        emit_failure(str(exc))
        return

    emit_success(f"Called {pass_count} PASS variants", [args.output_vcf, pass_path], summary)


if __name__ == "__main__":
    main()
