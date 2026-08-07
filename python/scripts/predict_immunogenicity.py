#!/usr/bin/env python3
"""Predict which presented candidates will actually provoke a T cell response.

This is the weakest link in the pipeline (see docs/PROJECT_PLAN.md ,  TESLA
benchmark precision ~7-50%). BigMHC-IM/PRIME are not installed locally, so
predict_stub() (a specified, permanent fallback) is what actually runs here
during local development.
"""
from __future__ import annotations

import argparse
import hashlib
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", ".."))
from python.common import io_utils
from python.common.response import emit_failure, emit_success, log_progress


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser()
    p.add_argument("--candidates-tsv", required=True)
    p.add_argument("--output-tsv", required=True)
    p.add_argument("--predictor", default="stub")
    p.add_argument("--use-gpu", default="false")
    p.add_argument("--use-stub", default="false")
    return p.parse_args()


def predict_bigmhc_im(peptides: list[str], alleles: list[str], use_gpu: bool) -> dict:
    raise RuntimeError("BigMHC-IM is not installed in this environment; use --predictor stub.")


def predict_prime(peptides: list[str], alleles: list[str]) -> dict:
    raise RuntimeError("PRIME is not installed in this environment; use --predictor stub.")


def predict_stub(peptides: list[str], alleles: list[str], seed: int = 42) -> dict:
    results = {}
    for peptide in peptides:
        for allele in alleles:
            digest = hashlib.sha256(f"im:{seed}:{peptide}:{allele}".encode()).hexdigest()
            score = int(digest[:8], 16) / 0xFFFFFFFF
            results[(peptide, allele)] = round(score, 4)
    return results


def merge_scores(candidates: list[dict], scores: dict, predictor_name: str) -> list[dict]:
    for c in candidates:
        key = (c["MutantPeptide"], c["HlaAllele"])
        c["ImmunogenicityScore"] = scores.get(key, 0.0)
        c["ImmunogenicityPredictor"] = predictor_name
    return candidates


def main() -> None:
    args = parse_args()
    use_stub = str(args.use_stub).lower() in ("1", "true", "yes") or args.predictor == "stub"
    use_gpu = str(args.use_gpu).lower() in ("1", "true", "yes")

    try:
        io_utils.check_file_exists(args.candidates_tsv, "Presentation-scored candidates TSV")
        candidates = io_utils.read_tsv(args.candidates_tsv)
        if not candidates:
            raise RuntimeError("Candidate list is empty")

        peptides = list({c["MutantPeptide"] for c in candidates})
        alleles = list({c["HlaAllele"] for c in candidates})

        log_progress(0, len(peptides), "immunogenicity prediction")
        if use_stub:
            scores = predict_stub(peptides, alleles)
            predictor_name = "stub"
        elif args.predictor == "bigmhc_im":
            scores = predict_bigmhc_im(peptides, alleles, use_gpu)
            predictor_name = "bigmhc_im"
        else:
            scores = predict_prime(peptides, alleles)
            predictor_name = "prime"

        candidates = merge_scores(candidates, scores, predictor_name)
        columns = list(candidates[0].keys())
        io_utils.write_tsv(args.output_tsv, candidates, columns)

        summary = {"candidateCount": len(candidates), "predictor": predictor_name}
    except Exception as exc:  # noqa: BLE001
        emit_failure(str(exc))
        return

    emit_success(f"Scored {len(candidates)} candidates with {summary['predictor']}", [args.output_tsv], summary)


if __name__ == "__main__":
    main()
