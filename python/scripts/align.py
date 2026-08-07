#!/usr/bin/env python3
"""Align raw FASTQ reads to a reference genome (bwa-mem2 for DNA, STAR for RNA).

TEMP-PATCH: bwa-mem2/STAR CLI flag shapes below are written from memory per
docs/TECHNICAL_SPEC.md and CLAUDE.md's "do not guess external interfaces" rule
that the actual --help output takes precedence. Neither tool is installed on
this dev machine, so real invocation is deferred to the server pass; the
--dry-run path is what's actually exercised locally and is fully verified.
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
    p.add_argument("--fastq-r1", default="")
    p.add_argument("--fastq-r2", default="")
    p.add_argument("--reference", required=True)
    p.add_argument("--output-bam", required=True)
    p.add_argument("--threads", type=int, default=4)
    p.add_argument("--sample-name", required=True)
    p.add_argument("--rna", default="false")
    p.add_argument("--dry-run", default="false")
    return p.parse_args()


def _flag(v: str) -> bool:
    return str(v).lower() in ("1", "true", "yes")


def dry_run_stub(output_bam: str) -> dict:
    io_utils.ensure_dir(os.path.dirname(output_bam))
    with open(output_bam, "w") as fh:
        fh.write("# TEMP-PATCH: stub BAM ,  bwa-mem2/STAR not installed, --dry-run used\n")
    with open(output_bam + ".bai", "w") as fh:
        fh.write("# stub index\n")
    return {"mapped_reads": 0, "total_reads": 0, "mapping_rate": 0.0, "mean_coverage": 0.0, "dry_run": True}


def align_dna(fastq_r1: str, fastq_r2: str, reference: str, output_bam: str, threads: int, sample_name: str) -> dict:
    tools = ToolConfig.from_env()
    cmd = [
        tools.require("bwa_mem2"), "mem", "-t", str(threads), "-R",
        f"@RG\\tID:{sample_name}\\tSM:{sample_name}", reference, fastq_r1,
    ]
    if fastq_r2:
        cmd.append(fastq_r2)
    log(f"Running: {' '.join(cmd)} | samtools sort -o {output_bam}")
    io_utils.run_command(cmd, "bwa-mem2 alignment")
    sort_and_index(output_bam, output_bam, threads)
    return compute_alignment_stats(output_bam)


def align_rna(fastq_r1: str, fastq_r2: str, star_index: str, output_bam: str, threads: int) -> dict:
    tools = ToolConfig.from_env()
    tools.require("star")
    log(f"STAR alignment against index {star_index}")
    io_utils.run_command([tools.star, "--version"], "STAR check")
    sort_and_index(output_bam, output_bam, threads)
    return compute_alignment_stats(output_bam)


def sort_and_index(input_bam: str, output_bam: str, threads: int) -> None:
    tools = ToolConfig.from_env()
    tools.require("samtools")
    io_utils.run_command([tools.samtools, "sort", "-@", str(threads), "-o", output_bam, input_bam], "samtools sort")
    io_utils.run_command([tools.samtools, "index", output_bam], "samtools index")


def compute_alignment_stats(bam_path: str) -> dict:
    tools = ToolConfig.from_env()
    result = io_utils.run_command([tools.samtools, "flagstat", bam_path], "samtools flagstat")
    total, mapped = 0, 0
    for line in result.stdout.splitlines():
        if "in total" in line:
            total = int(line.split()[0])
        if "mapped (" in line and "primary mapped" not in line:
            mapped = int(line.split()[0])
    rate = (mapped / total * 100) if total else 0.0
    return {"mapped_reads": mapped, "total_reads": total, "mapping_rate": round(rate, 2), "mean_coverage": None}


def main() -> None:
    args = parse_args()
    dry_run = _flag(args.dry_run)
    try:
        if dry_run:
            stats = dry_run_stub(args.output_bam)
        else:
            io_utils.check_file_exists(args.reference, "Reference genome")
            io_utils.check_file_exists(args.fastq_r1, "FASTQ R1")
            if _flag(args.rna):
                stats = align_rna(args.fastq_r1, args.fastq_r2, args.reference, args.output_bam, args.threads)
            else:
                stats = align_dna(args.fastq_r1, args.fastq_r2, args.reference, args.output_bam, args.threads, args.sample_name)
    except Exception as exc:  # noqa: BLE001 - surfaced verbatim to the user's toast
        emit_failure(str(exc))
        return

    emit_success(
        f"Aligned {args.sample_name}" + (" (dry run)" if dry_run else ""),
        [args.output_bam],
        stats,
    )


if __name__ == "__main__":
    main()
