#!/usr/bin/env python3
"""Predict which candidate peptides will be presented on the patient's HLA.

MHCflurry 2.0 is the primary predictor; predict_stub() is a specified,
permanent fallback (see CLAUDE.md "stubs are a specified feature") used
whenever mhcflurry isn't available/configured or --use-stub is passed.
"""
from __future__ import annotations

import argparse
import hashlib
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", ".."))
from python.common import io_utils
from python.common.response import emit_failure, emit_success, log, log_progress


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser()
    p.add_argument("--candidates-tsv", required=True)
    p.add_argument("--output-tsv", required=True)
    p.add_argument("--predictor", default="mhcflurry")
    p.add_argument("--batch-size", type=int, default=512)
    p.add_argument("--use-stub", default="false")
    return p.parse_args()


def predict_mhcflurry(peptides: list[str], alleles: list[str]) -> dict[tuple[str, str], dict]:
    from mhcflurry import Class1PresentationPredictor  # imported lazily: optional heavy dep

    predictor = Class1PresentationPredictor.load()
    df = predictor.predict(peptides=peptides, alleles=alleles, verbose=0)
    results = {}
    for _, row in df.iterrows():
        results[(row["peptide"], row["best_allele"])] = {
            "score": float(row["presentation_score"]),
            "percentileRank": float(row.get("presentation_percentile", 0.0)),
        }
    return results


def predict_bigmhc_el(peptides: list[str], alleles: list[str], use_gpu: bool) -> dict[tuple[str, str], dict]:
    raise RuntimeError("BigMHC-EL is not installed in this environment; use --predictor mhcflurry or --use-stub.")


def predict_stub(peptides: list[str], alleles: list[str], seed: int = 42) -> dict[tuple[str, str], dict]:
    """Deterministic pseudo-random scores so the ranking/UI layers are testable
    without invoking a real model. Same (peptide, allele) always yields the same score."""
    results = {}
    for peptide in peptides:
        for allele in alleles:
            digest = hashlib.sha256(f"{seed}:{peptide}:{allele}".encode()).hexdigest()
            score = int(digest[:8], 16) / 0xFFFFFFFF
            results[(peptide, allele)] = {"score": round(score, 4), "percentileRank": round((1 - score) * 10, 3)}
    return results


def merge_predictions(candidates: list[dict], predictions: dict, predictor_name: str) -> list[dict]:
    for c in candidates:
        key = (c["MutantPeptide"], c["HlaAllele"])
        result = predictions.get(key, {"score": 0.0, "percentileRank": 100.0})
        c["PresentationScore"] = result["score"]
        c["PresentationPercentileRank"] = result["percentileRank"]
        c["PresentationPredictor"] = predictor_name
    return candidates


def score_wildtype_counterparts(candidates: list[dict], predictor: str) -> list[dict]:
    peptides = list({c["WildTypePeptide"] for c in candidates})
    alleles = list({c["HlaAllele"] for c in candidates})
    predictions = predict_stub(peptides, alleles) if predictor == "stub" else predict_mhcflurry(peptides, alleles)
    for c in candidates:
        key = (c["WildTypePeptide"], c["HlaAllele"])
        c["WildTypePresentationScore"] = predictions.get(key, {"score": 0.0})["score"]
    return candidates


def batch_iterator(items: list, batch_size: int):
    for i in range(0, len(items), batch_size):
        yield items[i : i + batch_size]


def main() -> None:
    args = parse_args()
    use_stub = str(args.use_stub).lower() in ("1", "true", "yes") or args.predictor == "stub"

    try:
        io_utils.check_file_exists(args.candidates_tsv, "Candidates TSV")
        candidates = io_utils.read_tsv(args.candidates_tsv)
        if not candidates:
            raise RuntimeError("Candidate list is empty")

        peptides = list({c["MutantPeptide"] for c in candidates})
        alleles = list({c["HlaAllele"] for c in candidates})

        log_progress(0, len(peptides), "presentation prediction")
        if use_stub:
            predictions = predict_stub(peptides, alleles)
            predictor_name = "stub"
        elif args.predictor == "bigmhc_el":
            predictions = predict_bigmhc_el(peptides, alleles, use_gpu=False)
            predictor_name = "bigmhc_el"
        else:
            try:
                predictions = predict_mhcflurry(peptides, alleles)
                predictor_name = "mhcflurry"
            except Exception as exc:  # noqa: BLE001
                # mhcflurry is importable but its downloadable model weights aren't
                # fetched on this machine (network/disk-constrained dev environment).
                # Falls back to the specified stub predictor rather than crashing the step.
                log(f"mhcflurry unavailable ({exc}); falling back to stub predictor")
                predictions = predict_stub(peptides, alleles)
                predictor_name = "stub"

        candidates = merge_predictions(candidates, predictions, predictor_name)
        candidates = score_wildtype_counterparts(candidates, "stub" if use_stub else predictor_name)

        columns = list(candidates[0].keys())
        io_utils.write_tsv(args.output_tsv, candidates, columns)

        scores = [c["PresentationScore"] for c in candidates]
        summary = {
            "candidateCount": len(candidates),
            "predictor": predictor_name,
            "minScore": min(scores),
            "maxScore": max(scores),
        }
    except Exception as exc:  # noqa: BLE001
        emit_failure(str(exc))
        return

    emit_success(f"Scored {len(candidates)} candidates with {summary['predictor']}", [args.output_tsv], summary)


if __name__ == "__main__":
    main()
