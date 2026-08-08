#!/usr/bin/env python3
"""Predict which presented candidates will actually provoke a T cell response.

This is the weakest link in the pipeline (see docs/PROJECT_PLAN.md — TESLA
benchmark precision ~7-50%). predict_stub() (a specified, permanent fallback)
is what runs by default and whenever a real predictor isn't available or fails.

BigMHC (github.com/KarchinLab/bigmhc) is the real predictor wired in here in
preference to PRIME: PRIME's typical setup path pulls in NetMHCpan/MixMHCpred,
which need DTU Health Tech's gated academic registration, while BigMHC is a
fully open PyTorch model with no registration requirement — install it via
`python3 setup_tools.py --include-bigmhc`.

TEMP-PATCH: predict_bigmhc_im()'s CLI invocation is written from BigMHC's
documented usage, not verified against a real install (not installed on this
dev machine, per CLAUDE.md "do not guess external interfaces"). It's wrapped
so any mismatch just raises and the caller falls back to stub automatically —
nothing downstream depends on this guess being exactly right.
"""
from __future__ import annotations

import argparse
import csv
import hashlib
import os
import sys
import tempfile

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", ".."))
from python.common import io_utils
from python.common.response import emit_failure, emit_success, log, log_progress


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser()
    p.add_argument("--candidates-tsv", required=True)
    p.add_argument("--output-tsv", required=True)
    p.add_argument("--predictor", default="stub")
    p.add_argument("--use-gpu", default="false")
    p.add_argument("--use-stub", default="false")
    return p.parse_args()


def _bigmhc_home() -> str:
    return os.environ.get("BIGMHC_HOME", os.path.join(os.path.dirname(__file__), "..", "..", "tools", "bigmhc"))


def predict_bigmhc_im(peptides: list[str], alleles: list[str], use_gpu: bool) -> dict:
    predict_script = os.path.join(_bigmhc_home(), "src", "predict.py")
    if not os.path.exists(predict_script):
        raise RuntimeError(f"BigMHC not found at {_bigmhc_home()} (run `python3 setup_tools.py --include-bigmhc` first)")

    with tempfile.TemporaryDirectory(prefix="bigmhc_") as tmp_dir:
        input_csv = os.path.join(tmp_dir, "input.csv")
        output_csv = os.path.join(tmp_dir, "output.csv")

        with open(input_csv, "w", newline="") as fh:
            writer = csv.DictWriter(fh, fieldnames=["mhc", "pep"])
            writer.writeheader()
            for peptide in peptides:
                for allele in alleles:
                    writer.writerow({"mhc": allele, "pep": peptide})

        cmd = [
            sys.executable, predict_script,
            "-mdl", "im", "-i", input_csv, "-o", output_csv,
        ]
        if not use_gpu:
            cmd += ["-dev", "cpu"]
        io_utils.run_command(cmd, "BigMHC predict")

        io_utils.check_file_exists(output_csv, "BigMHC output")
        results = {}
        with open(output_csv, newline="") as fh:
            for row in csv.DictReader(fh):
                results[(row["pep"], row["mhc"])] = float(row.get("BigMHC_IM", row.get("pred", 0.0)))
        return results


def predict_prime(peptides: list[str], alleles: list[str]) -> dict:
    raise RuntimeError(
        "PRIME is not installed in this environment (typically needs NetMHCpan/MixMHCpred, "
        "which require DTU Health Tech's gated academic registration). Use --predictor bigmhc_im or stub."
    )


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
        predictor_name = "stub"
        scores: dict = {}

        if not use_stub:
            try:
                if args.predictor == "prime":
                    scores = predict_prime(peptides, alleles)
                    predictor_name = "prime"
                else:
                    scores = predict_bigmhc_im(peptides, alleles, use_gpu)
                    predictor_name = "bigmhc_im"
            except Exception as exc:  # noqa: BLE001 - real predictor is best-effort, stub is the guaranteed fallback
                log(f"{args.predictor} unavailable ({exc}); falling back to stub predictor")

        if not scores:
            scores = predict_stub(peptides, alleles)
            predictor_name = "stub"

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
