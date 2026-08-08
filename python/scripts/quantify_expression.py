#!/usr/bin/env python3
"""Quantify RNA-seq expression (gene-level TPM) with Salmon, for step 9's expression filter.

Uses Salmon's quasi-mapping mode directly against the raw RNA FASTQ — it doesn't need a
STAR alignment first, just a transcriptome index (built once by setup_reference.py
--include-rna). Salmon reports transcript-level TPM in quant.sf; this aggregates to
gene-level TPM (summing transcript TPMs per gene) via the tx2gene mapping built alongside
the index, since that's the "gene\ttpm" shape python/scripts/filter_candidates.py expects.

TEMP-PATCH: salmon CLI flags are written from documented usage, not verified against a
real install on this dev machine (salmon isn't installed here) — deferred to server pass.
"""
from __future__ import annotations

import argparse
import os
import shutil
import sys
import tempfile

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", ".."))
from python.common import io_utils
from python.common.config import ToolConfig
from python.common.response import emit_failure, emit_success, log


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser()
    p.add_argument("--rna-r1", required=True)
    p.add_argument("--rna-r2", default="")
    p.add_argument("--salmon-index", required=True)
    p.add_argument("--tx2gene", required=True)
    p.add_argument("--output-tsv", required=True)
    p.add_argument("--threads", type=int, default=4)
    return p.parse_args()


def load_tx2gene(path: str) -> dict[str, str]:
    mapping = {}
    for row in io_utils.read_tsv(path):
        if "transcript" in row and "gene" in row:
            mapping[row["transcript"]] = row["gene"]
    return mapping


def run_salmon(rna_r1: str, rna_r2: str, salmon_index: str, threads: int, out_dir: str, tools: ToolConfig) -> str:
    cmd = [tools.require("salmon"), "quant", "-i", salmon_index, "-l", "A", "-p", str(threads), "-o", out_dir, "--validateMappings"]
    if rna_r2:
        cmd += ["-1", rna_r1, "-2", rna_r2]
    else:
        cmd += ["-r", rna_r1]  # single-end
    io_utils.run_command(cmd, "salmon quant")
    quant_path = os.path.join(out_dir, "quant.sf")
    io_utils.check_file_exists(quant_path, "salmon quant.sf output")
    return quant_path


def aggregate_to_gene_tpm(quant_sf_path: str, tx2gene: dict[str, str]) -> dict[str, float]:
    gene_tpm: dict[str, float] = {}
    unmapped = 0
    for row in io_utils.read_tsv(quant_sf_path):
        transcript_id = row.get("Name", "").split(".")[0]  # strip Ensembl version suffix
        gene = tx2gene.get(transcript_id) or tx2gene.get(row.get("Name", ""))
        if gene is None:
            unmapped += 1
            continue
        gene_tpm[gene] = gene_tpm.get(gene, 0.0) + float(row.get("TPM", 0.0))
    if unmapped:
        log(f"WARNING: {unmapped} transcript(s) had no tx2gene mapping and were skipped")
    return gene_tpm


def main() -> None:
    args = parse_args()
    try:
        io_utils.check_file_exists(args.rna_r1, "RNA FASTQ R1")
        io_utils.check_file_exists(args.salmon_index, "Salmon index")
        io_utils.check_file_exists(args.tx2gene, "tx2gene mapping")

        tools = ToolConfig.from_env()
        tx2gene = load_tx2gene(args.tx2gene)

        with tempfile.TemporaryDirectory(prefix="salmon_quant_") as tmp_dir:
            quant_path = run_salmon(args.rna_r1, args.rna_r2, args.salmon_index, args.threads, tmp_dir, tools)
            gene_tpm = aggregate_to_gene_tpm(quant_path, tx2gene)

        rows = [{"gene": gene, "tpm": round(tpm, 4)} for gene, tpm in sorted(gene_tpm.items())]
        io_utils.write_tsv(args.output_tsv, rows, ["gene", "tpm"])

        summary = {"genesQuantified": len(rows), "totalTpmSum": round(sum(gene_tpm.values()), 1)}
    except Exception as exc:  # noqa: BLE001
        emit_failure(str(exc))
        return

    emit_success(f"Quantified {len(rows)} genes", [args.output_tsv], summary)


if __name__ == "__main__":
    main()
