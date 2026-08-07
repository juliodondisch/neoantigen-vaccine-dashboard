#!/usr/bin/env python3
"""Annotate variant consequences with VEP and keep only protein-altering ones.

TEMP-PATCH: VEP CLI flags are unverified against a real install per CLAUDE.md;
VEP is not present on this dev machine. Deferred to server pass.
"""
from __future__ import annotations

import argparse
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", ".."))
from python.common import io_utils
from python.common.config import ToolConfig
from python.common.response import emit_failure, emit_success, log

DEFAULT_KEEP = [
    "missense_variant", "stop_gained", "frameshift_variant",
    "inframe_insertion", "inframe_deletion", "start_lost",
]


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser()
    p.add_argument("--input-vcf", required=True)
    p.add_argument("--output-vcf", required=True)
    p.add_argument("--output-tsv", required=True)
    p.add_argument("--use-database", default="true")
    p.add_argument("--cache-dir", default="")
    p.add_argument("--keep-consequences", default=",".join(DEFAULT_KEEP))
    return p.parse_args()


def run_vep(input_vcf: str, output_vcf: str, use_database: bool, cache_dir: str | None) -> None:
    tools = ToolConfig.from_env()
    cmd = [tools.require("vep"), "-i", input_vcf, "-o", output_vcf, "--vcf"]
    cmd += ["--database"] if use_database else ["--cache", "--dir_cache", cache_dir or ""]
    io_utils.run_command(cmd, "VEP annotation")


def parse_vep_consequences(vcf_path: str) -> list[dict]:
    records = []
    for record in io_utils.read_vcf(vcf_path):
        csq = _extract_csq(record.get("INFO", ""))
        record["_consequence"] = csq
        records.append(record)
    return records


def filter_protein_altering(records: list[dict], kept_consequences: list[str]) -> list[dict]:
    return [r for r in records if r.get("_consequence") in kept_consequences]


def extract_protein_sequences(records: list[dict], reference_proteome: str) -> list[dict]:
    # TEMP-PATCH: real implementation should pull the transcript's actual CDS/protein
    # sequence from the reference proteome/GTF; without VEP's transcript mapping available
    # locally there is nothing to look up, so this is a no-op passthrough for the stub path.
    return records


def build_mutant_sequence(wildtype_seq: str, protein_position: int, wt_aa: str, mut_aa: str, consequence: str) -> str:
    if protein_position < 1 or protein_position > len(wildtype_seq):
        return wildtype_seq
    idx = protein_position - 1
    if consequence == "stop_gained":
        return wildtype_seq[:idx] + "*"
    if consequence == "frameshift_variant":
        return wildtype_seq[:idx]  # downstream sequence is scrambled/unknown without real annotation
    return wildtype_seq[:idx] + mut_aa + wildtype_seq[idx + 1 :]


def count_by_consequence(records: list[dict]) -> dict[str, int]:
    counts: dict[str, int] = {}
    for r in records:
        c = r.get("_consequence", "unknown")
        counts[c] = counts.get(c, 0) + 1
    return counts


def _extract_csq(info: str) -> str:
    for part in info.split(";"):
        if part.startswith("CSQ=") or part.startswith("Consequence="):
            return part.split("=", 1)[1].split("|")[0]
    return "unknown"


def main() -> None:
    args = parse_args()
    keep = [c for c in args.keep_consequences.split(",") if c]
    try:
        io_utils.check_file_exists(args.input_vcf, "Input VCF")
        use_db = str(args.use_database).lower() in ("1", "true", "yes")
        run_vep(args.input_vcf, args.output_vcf, use_db, args.cache_dir or None)

        records = parse_vep_consequences(args.output_vcf)
        kept = filter_protein_altering(records, keep)
        counts = count_by_consequence(records)

        rows = [
            {
                "Chromosome": r.get("CHROM", ""), "Position": r.get("POS", ""),
                "Ref": r.get("REF", ""), "Alt": r.get("ALT", ""), "Consequence": r.get("_consequence", ""),
            }
            for r in kept
        ]
        io_utils.write_tsv(args.output_tsv, rows, ["Chromosome", "Position", "Ref", "Alt", "Consequence"])

        summary = {
            "inputVariants": len(records),
            "proteinAltering": len(kept),
            "discarded": len(records) - len(kept),
            "consequenceCounts": counts,
        }
    except Exception as exc:  # noqa: BLE001
        emit_failure(str(exc))
        return

    emit_success(f"{len(kept)} of {len(records)} variants are protein-altering", [args.output_vcf, args.output_tsv], summary)


if __name__ == "__main__":
    main()
