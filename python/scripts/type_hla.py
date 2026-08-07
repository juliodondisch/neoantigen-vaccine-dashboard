#!/usr/bin/env python3
"""Determine the patient's HLA class I alleles from normal-tissue reads via OptiType.

TEMP-PATCH: OptiType CLI flags are unverified against a real install per
CLAUDE.md (tool not present on this dev machine); deferred to server pass.
Local development instead uses FixtureSeeder.SeedHlaTypingAsync, which writes
a known 6-allele fixture directly.
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


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser()
    p.add_argument("--input", required=True)
    p.add_argument("--output-dir", required=True)
    p.add_argument("--output-json", required=True)
    p.add_argument("--is-bam", default="false")
    p.add_argument("--include-class-ii", default="false")
    return p.parse_args()


def run_optitype(input_file: str, output_dir: str, is_bam: bool, include_class_ii: bool) -> str:
    tools = ToolConfig.from_env()
    io_utils.ensure_dir(output_dir)
    cmd = [tools.require("optitype"), "-i", input_file, "-d", "-v", "-o", output_dir]
    io_utils.run_command(cmd, "OptiType")
    for fname in os.listdir(output_dir):
        if fname.endswith("_result.tsv"):
            return os.path.join(output_dir, fname)
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


def extract_hla_reads(bam_path: str, hla_regions: str, output_fastq: str) -> None:
    tools = ToolConfig.from_env()
    io_utils.run_command(
        [tools.samtools, "view", "-b", bam_path, hla_regions, "-o", output_fastq],
        "extract HLA-region reads",
    )


def main() -> None:
    args = parse_args()
    is_bam = str(args.is_bam).lower() in ("1", "true", "yes")
    include_class_ii = str(args.include_class_ii).lower() in ("1", "true", "yes")

    try:
        io_utils.check_file_exists(args.input, "HLA typing input")
        result_tsv = run_optitype(args.input, args.output_dir, is_bam, include_class_ii)
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
