#!/usr/bin/env python3
"""Slide a window across each mutation to build mutant/wild-type peptide pairs.

Pure logic, no external tools. Duplicated deliberately in C#
(Services/06_CandidateGeneration/SlidingWindowGenerator.cs) — that version is
authoritative and unit-tested; this one exists for standalone pipeline use and
must stay behaviorally identical (see CLAUDE.md "things that will bite you").
"""
from __future__ import annotations

import argparse
import json
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", ".."))
from python.common import io_utils
from python.common.response import emit_failure, emit_success

VALID_AA = set("ACDEFGHIKLMNPQRSTVWY*")


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser()
    p.add_argument("--variants-tsv", required=True)
    p.add_argument("--hla-json", required=True)
    p.add_argument("--output-tsv", required=True)
    p.add_argument("--min-length", type=int, default=8)
    p.add_argument("--max-length", type=int, default=11)
    return p.parse_args()


def generate_windows(sequence: str, mutation_pos: int, min_len: int, max_len: int) -> list[tuple[str, int]]:
    windows = []
    for length in range(min_len, max_len + 1):
        if len(sequence) < length:
            continue
        earliest = max(0, mutation_pos - length + 1)
        latest = min(len(sequence) - length, mutation_pos)
        for start in range(earliest, latest + 1):
            window = sequence[start : start + length]
            if window and all(c in VALID_AA for c in window):
                windows.append((window, start))
    return windows


def build_peptide_pairs(variant: dict, min_len: int, max_len: int) -> list[dict]:
    wt_seq = variant.get("WildTypeProteinSequence", "")
    mut_seq = variant.get("MutantProteinSequence", "")
    pos = int(variant.get("ProteinPosition", 0))
    if not wt_seq or not mut_seq:
        return []

    pairs = []
    for length in range(min_len, max_len + 1):
        if len(mut_seq) < length:
            continue
        earliest = max(0, pos - length + 1)
        latest = min(len(mut_seq) - length, pos)
        for start in range(earliest, latest + 1):
            mutant = mut_seq[start : start + length]
            wt_start = max(0, min(start, len(wt_seq) - length))
            wt_end = wt_start + length
            if wt_end > len(wt_seq):
                continue
            wildtype = wt_seq[wt_start:wt_end]
            if not (all(c in VALID_AA for c in mutant) and all(c in VALID_AA for c in wildtype)):
                continue
            pairs.append(
                {
                    "MutantPeptide": mutant,
                    "WildTypePeptide": wildtype,
                    "Length": length,
                    "MutationOffsetInPeptide": pos - start,
                    "GeneSymbol": variant.get("GeneSymbol", ""),
                    "TranscriptId": variant.get("TranscriptId", ""),
                    "ProteinPosition": pos,
                    "Vaf": variant.get("Vaf", 0),
                    "SourceVariantId": f"{variant.get('Chromosome','')}:{variant.get('Position','')}:{variant.get('Ref','')}>{variant.get('Alt','')}",
                }
            )
    return pairs


def expand_across_alleles(pairs: list[dict], alleles: list[str]) -> list[dict]:
    candidates = []
    for pair in pairs:
        for allele in alleles:
            candidate = dict(pair)
            candidate["HlaAllele"] = allele
            candidate["PeptideLength"] = pair["Length"]
            candidates.append(candidate)
    return candidates


def write_candidates(candidates: list[dict], output_path: str) -> None:
    columns = [
        "MutantPeptide", "WildTypePeptide", "HlaAllele", "PeptideLength", "GeneSymbol",
        "TranscriptId", "SourceVariantId", "ProteinPosition", "MutationOffsetInPeptide", "Vaf",
    ]
    io_utils.write_tsv(output_path, candidates, columns)


def main() -> None:
    args = parse_args()
    try:
        io_utils.check_file_exists(args.variants_tsv, "Protein-altering variants TSV")
        io_utils.check_file_exists(args.hla_json, "HLA profile JSON")

        variants = io_utils.read_tsv(args.variants_tsv)
        with open(args.hla_json) as fh:
            hla = json.load(fh)
        alleles = hla.get("classIAlleles") or hla.get("ClassIAlleles") or []
        if not alleles:
            raise RuntimeError("HLA profile contains no class I alleles")

        all_pairs = []
        for variant in variants:
            all_pairs.extend(build_peptide_pairs(variant, args.min_length, args.max_length))
        candidates = expand_across_alleles(all_pairs, alleles)
        write_candidates(candidates, args.output_tsv)

        summary = {"mutationCount": len(variants), "peptidePairCount": len(all_pairs), "candidateCount": len(candidates)}
    except Exception as exc:  # noqa: BLE001
        emit_failure(str(exc))
        return

    emit_success(f"Generated {len(candidates)} candidates", [args.output_tsv], summary)


if __name__ == "__main__":
    main()
