#!/usr/bin/env python3
"""Remove self-similar (unsafe) and, if RNA data exists, unexpressed candidates."""
from __future__ import annotations

import argparse
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", ".."))
from python.common import io_utils
from python.common.response import emit_failure, emit_success


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser()
    p.add_argument("--candidates-tsv", required=True)
    p.add_argument("--proteome-fasta", required=True)
    p.add_argument("--expression-tsv", default="")
    p.add_argument("--output-tsv", required=True)
    p.add_argument("--removed-tsv", required=True)
    p.add_argument("--min-tpm", type=float, default=1.0)
    p.add_argument("--kmer-size", type=int, default=8)
    return p.parse_args()


def load_proteome(fasta_path: str) -> dict[str, str]:
    return dict(io_utils.read_fasta(fasta_path))


def build_kmer_index(proteome: dict[str, str], k: int) -> set[str]:
    kmers: set[str] = set()
    for seq in proteome.values():
        for i in range(0, max(0, len(seq) - k + 1)):
            kmers.add(seq[i : i + k])
    return kmers


def check_self_similarity(peptide: str, kmer_index: set[str], k: int) -> tuple[bool, float]:
    if len(peptide) < k:
        return False, 0.0
    hits = sum(1 for i in range(len(peptide) - k + 1) if peptide[i : i + k] in kmer_index)
    total = len(peptide) - k + 1
    similarity = hits / total if total else 0.0
    return similarity >= 1.0, similarity  # any exact k-mer match is treated as self-derived


def load_expression(tsv_path: str) -> dict[str, float]:
    rows = io_utils.read_tsv(tsv_path)
    return {r["gene"]: float(r["tpm"]) for r in rows if "gene" in r and "tpm" in r}


def apply_expression_filter(candidates: list[dict], expression: dict[str, float], min_tpm: float) -> tuple[list[dict], list[dict]]:
    kept, removed = [], []
    for c in candidates:
        tpm = expression.get(c.get("GeneSymbol", ""))
        c["ExpressionTpm"] = tpm if tpm is not None else ""
        if tpm is not None and tpm < min_tpm:
            c["RemovalReason"] = f"Gene {c.get('GeneSymbol')} expression {tpm} TPM below threshold {min_tpm}"
            c["PassedExpressionFilter"] = "false"
            removed.append(c)
        else:
            c["PassedExpressionFilter"] = "true"
            kept.append(c)
    return kept, removed


def apply_self_filter(candidates: list[dict], kmer_index: set[str], k: int) -> tuple[list[dict], list[dict]]:
    kept, removed = [], []
    for c in candidates:
        is_self, similarity = check_self_similarity(c.get("MutantPeptide", ""), kmer_index, k)
        c["SelfSimilarityScore"] = round(similarity, 3)
        if is_self:
            c["RemovalReason"] = "Peptide matches a normal human protein fragment"
            c["PassedSelfFilter"] = "false"
            removed.append(c)
        else:
            c["PassedSelfFilter"] = "true"
            kept.append(c)
    return kept, removed


def main() -> None:
    args = parse_args()
    try:
        io_utils.check_file_exists(args.candidates_tsv, "Immunogenicity-scored candidates TSV")
        io_utils.check_file_exists(args.proteome_fasta, "Reference proteome FASTA")

        candidates = io_utils.read_tsv(args.candidates_tsv)
        input_count = len(candidates)

        proteome = load_proteome(args.proteome_fasta)
        kmer_index = build_kmer_index(proteome, args.kmer_size)
        survivors, removed_self = apply_self_filter(candidates, kmer_index, args.kmer_size)

        expression_applied = bool(args.expression_tsv)
        removed_expr: list[dict] = []
        if expression_applied:
            expression = load_expression(args.expression_tsv)
            survivors, removed_expr = apply_expression_filter(survivors, expression, args.min_tpm)
        else:
            for c in survivors:
                c["PassedExpressionFilter"] = "true"
                c["ExpressionTpm"] = ""

        removed = removed_self + removed_expr
        columns = list(candidates[0].keys()) if candidates else []
        io_utils.write_tsv(args.output_tsv, survivors, columns)
        io_utils.write_tsv(args.removed_tsv, removed, columns)

        summary = {
            "inputCount": input_count,
            "removedBySelfSimilarity": len(removed_self),
            "removedByExpression": len(removed_expr),
            "survived": len(survivors),
            "expressionFilterApplied": expression_applied,
        }
    except Exception as exc:  # noqa: BLE001
        emit_failure(str(exc))
        return

    emit_success(f"{summary['survived']} of {input_count} candidates survived filtering", [args.output_tsv, args.removed_tsv], summary)


if __name__ == "__main__":
    main()
