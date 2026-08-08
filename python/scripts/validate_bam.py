#!/usr/bin/env python3
"""Check an uploaded/externally-aligned BAM is actually usable by the pipeline, and fix
what can be fixed automatically.

Externally-provided BAMs (the "we get BAMs directly, skip alignment" path) are the
riskiest input this app accepts: unlike files this app generates itself, there's no
guarantee they have a correctly-set @RG SM: tag (which call_variants.py's
get_normal_sample_name() depends on), are coordinate-sorted, or have an index. This
script checks all three and repairs what it safely can rather than letting a cryptic
GATK/samtools failure surface three steps downstream.

TEMP-PATCH: samtools flag shapes (addreplacerg, quickcheck, sort, index) are written
from documented usage, not verified against a real install on this dev machine.
"""
from __future__ import annotations

import argparse
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", ".."))
from python.common import io_utils
from python.common.config import ToolConfig
from python.common.response import emit_failure, emit_success, log


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser()
    p.add_argument("--bam", required=True)
    p.add_argument("--expected-sample-name", required=True)
    p.add_argument("--output-bam", required=True, help="where to write a repaired copy, if any repair is needed")
    p.add_argument("--fix", default="true")
    return p.parse_args()


def _flag(v: str) -> bool:
    return str(v).lower() in ("1", "true", "yes")


def check_integrity(bam_path: str, tools: ToolConfig) -> None:
    result = io_utils.run_command([tools.require("samtools"), "quickcheck", "-v", bam_path], "samtools quickcheck")
    if result.stdout.strip():
        raise RuntimeError(f"BAM failed integrity check: {result.stdout.strip()}")


def read_header(bam_path: str, tools: ToolConfig) -> str:
    return io_utils.run_command([tools.samtools, "view", "-H", bam_path], "read BAM header").stdout


def check_sample_name(header: str, expected: str) -> tuple[bool, str | None]:
    for line in header.splitlines():
        if line.startswith("@RG"):
            for field in line.split("\t"):
                if field.startswith("SM:"):
                    actual = field[3:]
                    return actual == expected, actual
    return False, None


def check_sort_order(header: str) -> str | None:
    for line in header.splitlines():
        if line.startswith("@HD"):
            for field in line.split("\t"):
                if field.startswith("SO:"):
                    return field[3:]
    return None


def fix_sample_name(bam_path: str, output_path: str, sample_name: str, tools: ToolConfig) -> None:
    io_utils.run_command(
        [tools.samtools, "addreplacerg", "-r", f"@RG\\tID:{sample_name}\\tSM:{sample_name}", "-o", output_path, bam_path],
        "samtools addreplacerg",
    )


def fix_sort_order(bam_path: str, output_path: str, tools: ToolConfig) -> None:
    io_utils.run_command([tools.samtools, "sort", "-o", output_path, bam_path], "samtools sort")


def build_index(bam_path: str, tools: ToolConfig) -> None:
    io_utils.run_command([tools.samtools, "index", bam_path], "samtools index")


def main() -> None:
    args = parse_args()
    fix = _flag(args.fix)
    warnings: list[str] = []
    fixes_applied: list[str] = []

    try:
        io_utils.check_file_exists(args.bam, "BAM file")
        tools = ToolConfig.from_env()

        check_integrity(args.bam, tools)

        current_bam = args.bam
        header = read_header(current_bam, tools)

        sample_ok, actual_sample = check_sample_name(header, args.expected_sample_name)
        if not sample_ok:
            msg = (
                f"@RG SM: tag is '{actual_sample}', expected '{args.expected_sample_name}'"
                if actual_sample else "no @RG SM: tag found"
            )
            if fix:
                log(f"Fixing sample name: {msg}")
                fix_sample_name(current_bam, args.output_bam, args.expected_sample_name, tools)
                current_bam = args.output_bam
                fixes_applied.append(f"set @RG SM: to '{args.expected_sample_name}' ({msg})")
                header = read_header(current_bam, tools)
            else:
                warnings.append(msg)

        sort_order = check_sort_order(header)
        if sort_order != "coordinate":
            msg = f"BAM sort order is '{sort_order or 'unset'}', expected 'coordinate'"
            if fix:
                log(f"Fixing sort order: {msg}")
                sorted_path = args.output_bam if current_bam == args.bam else current_bam
                fix_sort_order(current_bam, sorted_path, tools)
                current_bam = sorted_path
                fixes_applied.append(f"sorted by coordinate ({msg})")
            else:
                warnings.append(msg)

        # Always safe to build a missing index — it's additive, not a content change,
        # so this happens regardless of --fix.
        if not os.path.exists(current_bam + ".bai") and not os.path.exists(os.path.splitext(current_bam)[0] + ".bai"):
            build_index(current_bam, tools)
            fixes_applied.append("built missing .bai index")

        summary = {
            "valid": True,
            "warnings": warnings,
            "fixesApplied": fixes_applied,
            "repairedPath": current_bam if current_bam != args.bam else None,
        }
    except Exception as exc:  # noqa: BLE001
        emit_failure(str(exc))
        return

    outputs = [current_bam] if current_bam != args.bam else []
    message = "BAM is valid" if not warnings and not fixes_applied else (
        f"BAM validated with {len(fixes_applied)} fix(es), {len(warnings)} warning(s)"
    )
    emit_success(message, outputs, summary)


if __name__ == "__main__":
    main()
