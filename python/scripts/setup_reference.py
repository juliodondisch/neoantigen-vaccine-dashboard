#!/usr/bin/env python3
"""Download a reference genome and build the aligner indexes the pipeline needs.

Meant to be run once per environment (dev machine or, more realistically, the
cloud server this app is deployed to) before step 2 (alignment) can run for
real. Invoked automatically by AlignmentService when the reference is missing
and disk space allows; can also be run by hand:

    python python/scripts/setup_reference.py --genome chr21_test --output-dir data/references/chr21_test
    python python/scripts/setup_reference.py --genome GRCh38 --output-dir data/references/GRCh38 --include-rna --dry-run

TEMP-PATCH: bwa-mem2/samtools/salmon CLI flags below are written from documented
usage, not verified against a real install on this dev machine (see CLAUDE.md
"do not guess external interfaces") — deferred to server pass. Source URLs are
not guesses where avoidable: the chr21 FASTA URL is the exact one already used
in docs/TECHNICAL_SPEC.md §7's fixture-generation walkthrough; the full-genome
FASTA URL is the same UCSC host/convention, just the whole-genome bigZips path.
The Ensembl cDNA URL's release number changes over time — check
https://ftp.ensembl.org/pub/ for the current one and pass --ensembl-release if
this one has gone stale.

Prebuilt aligner indexes aren't something you can reliably download — they're
tool-version-specific binaries with no stable canonical distribution the way
the FASTA itself has, so this builds the bwa-mem2/salmon index locally after
downloading the sequence. That's the standard approach every pipeline uses.

This app runs fully offline at request time (see CLAUDE.md / docs/PROGRESS.md)
— every download here is a one-time setup step with inbound network access,
not something the pipeline reaches for while actually processing a patient.
"""
from __future__ import annotations

import argparse
import gzip
import os
import re
import shutil
import sys
import time
import urllib.request

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", ".."))
from python.common import io_utils
from python.common.config import ToolConfig
from python.common.response import emit_failure, emit_success, log, log_progress

# UCSC goldenPath — same host/convention docs/TECHNICAL_SPEC.md §7 already uses for chr21.
GENOME_SOURCES = {
    "chr21_test": {
        "fasta_url": "https://hgdownload.soe.ucsc.edu/goldenPath/hg38/chromosomes/chr21.fa.gz",
        "fasta_name": "chr21.fa",
        "required_bytes": 600 * 1024 * 1024,  # ~600MB: ~15MB compressed download + index, generous
    },
    "GRCh38": {
        "fasta_url": "https://hgdownload.soe.ucsc.edu/goldenPath/hg38/bigZips/hg38.fa.gz",
        "fasta_name": "GRCh38.fa",
        "required_bytes": 40 * 1024 * 1024 * 1024,  # ~40GB: ~1GB download, ~3.1GB decompressed, ~10-12GB bwa-mem2 index, margin
    },
}

RNA_EXTRA_BYTES = 6 * 1024 * 1024 * 1024  # cDNA download + decompress + salmon index, generous
ENSEMBL_CDNA_URL_TEMPLATE = "https://ftp.ensembl.org/pub/release-{release}/fasta/homo_sapiens/cdna/Homo_sapiens.GRCh38.cdna.all.fa.gz"
DEFAULT_ENSEMBL_RELEASE = 110  # TEMP-PATCH: check https://ftp.ensembl.org/pub/ for the current release before relying on this

HEADER_GENE_RE = re.compile(r"gene_symbol:(\S+)")
HEADER_GENE_ID_RE = re.compile(r"gene:(\S+)")


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser()
    p.add_argument("--genome", required=True, choices=sorted(GENOME_SOURCES.keys()))
    p.add_argument("--output-dir", required=True)
    p.add_argument("--threads", type=int, default=4)
    p.add_argument("--include-rna", default="false", help="also build a Salmon transcriptome index for expression quantification (step 9); GRCh38 only")
    p.add_argument("--ensembl-release", type=int, default=DEFAULT_ENSEMBL_RELEASE)
    p.add_argument("--dry-run", default="false")
    return p.parse_args()


def _flag(v: str) -> bool:
    return str(v).lower() in ("1", "true", "yes")


def check_disk_space(output_dir: str, required_bytes: int) -> None:
    stat = shutil.disk_usage(os.path.dirname(os.path.abspath(output_dir)) or "/")
    if stat.free < required_bytes:
        raise RuntimeError(
            f"Not enough free disk space: need ~{required_bytes / (1024**3):.0f}GB, "
            f"have {stat.free / (1024**3):.1f}GB free at {output_dir}"
        )


def _download_and_gunzip(url: str, dest_path: str) -> None:
    io_utils.ensure_dir(os.path.dirname(dest_path))
    gz_path = dest_path + ".gz"

    log(f"Downloading {url}")
    start = time.time()

    def _progress(block_num: int, block_size: int, total_size: int) -> None:
        if total_size > 0 and block_num % 200 == 0:
            log_progress(min(block_num * block_size, total_size), total_size, f"downloading {os.path.basename(dest_path)}")

    urllib.request.urlretrieve(url, gz_path, reporthook=_progress)
    log(f"Download complete in {time.time() - start:.0f}s ({io_utils.file_size_mb(gz_path):.0f}MB compressed)")

    with gzip.open(gz_path, "rb") as src, open(dest_path, "wb") as dst:
        shutil.copyfileobj(src, dst)
    os.remove(gz_path)
    log(f"Ready at {dest_path} ({io_utils.file_size_mb(dest_path):.0f}MB)")


def download_fasta(url: str, dest_fasta: str) -> None:
    _download_and_gunzip(url, dest_fasta)


def build_bwa_index(fasta_path: str, tools: ToolConfig) -> None:
    log("Building bwa-mem2 index (this is the slow part for a full genome)...")
    io_utils.run_command([tools.require("bwa_mem2"), "index", fasta_path], "bwa-mem2 index")


def build_samtools_aux_files(fasta_path: str, tools: ToolConfig) -> None:
    io_utils.run_command([tools.require("samtools"), "faidx", fasta_path], "samtools faidx")
    dict_path = os.path.splitext(fasta_path)[0] + ".dict"
    if not os.path.exists(dict_path):
        io_utils.run_command([tools.samtools, "dict", fasta_path, "-o", dict_path], "samtools dict")


def build_tx2gene_from_cdna_headers(cdna_fasta_path: str, output_path: str) -> int:
    """Ensembl's cDNA FASTA headers carry gene info inline
    (">ENST00000... gene:ENSG00000... gene_symbol:TP53 ...") — parsing them directly
    means expression quantification doesn't need a separate GTF download at all."""
    count = 0
    with open(cdna_fasta_path) as src, open(output_path, "w") as dst:
        dst.write("transcript\tgene\n")
        for line in src:
            if not line.startswith(">"):
                continue
            transcript_id = line[1:].split()[0].split(".")[0]
            symbol_match = HEADER_GENE_RE.search(line)
            gene = symbol_match.group(1) if symbol_match else None
            if gene is None:
                id_match = HEADER_GENE_ID_RE.search(line)
                gene = id_match.group(1).split(".")[0] if id_match else None
            if gene is None:
                continue
            dst.write(f"{transcript_id}\t{gene}\n")
            count += 1
    return count


def build_salmon_index(cdna_fasta_path: str, output_dir: str, threads: int, tools: ToolConfig) -> str:
    salmon_dir = os.path.join(output_dir, "salmon_index")
    io_utils.run_command(
        [tools.require("salmon"), "index", "-t", cdna_fasta_path, "-i", salmon_dir, "-k", "31", "-p", str(threads)],
        "salmon index",
    )
    return salmon_dir


def build_rna_reference(output_dir: str, threads: int, ensembl_release: int, tools: ToolConfig) -> dict:
    """Best-effort — RNA expression quantification (step 9) is optional in this pipeline,
    so a failure here is logged and returned as a warning rather than aborting DNA setup."""
    cdna_path = os.path.join(output_dir, "transcriptome.cdna.fa")
    tx2gene_path = os.path.join(output_dir, "tx2gene.tsv")

    if not os.path.exists(cdna_path):
        url = ENSEMBL_CDNA_URL_TEMPLATE.format(release=ensembl_release)
        _download_and_gunzip(url, cdna_path)
    else:
        log(f"{cdna_path} already exists, skipping download")

    gene_count = build_tx2gene_from_cdna_headers(cdna_path, tx2gene_path)
    log(f"tx2gene mapping built: {gene_count} transcripts")

    salmon_dir = os.path.join(output_dir, "salmon_index")
    if os.path.exists(os.path.join(salmon_dir, "info.json")):
        log("Salmon index already present, skipping")
    else:
        build_salmon_index(cdna_path, output_dir, threads, tools)

    return {"salmonIndexDir": salmon_dir, "tx2gene": tx2gene_path, "transcriptCount": gene_count}


def main() -> None:
    args = parse_args()
    dry_run = _flag(args.dry_run)
    include_rna = _flag(args.include_rna)
    source = GENOME_SOURCES[args.genome]
    fasta_path = os.path.join(args.output_dir, source["fasta_name"])

    if include_rna and args.genome != "GRCh38":
        log(f"--include-rna is only meaningful for GRCh38 (got '{args.genome}') — ignoring it")
        include_rna = False

    required_bytes = source["required_bytes"] + (RNA_EXTRA_BYTES if include_rna else 0)

    try:
        check_disk_space(args.output_dir, required_bytes)

        if dry_run:
            summary = {
                "dryRun": True, "genome": args.genome, "fastaUrl": source["fasta_url"],
                "targetFasta": fasta_path, "requiredBytes": required_bytes, "includeRna": include_rna,
            }
            emit_success(f"Dry run: would download and index '{args.genome}' at {fasta_path}", [], summary)
            return

        tools = ToolConfig.from_env()
        missing = [t for t in ("bwa_mem2", "samtools") if not tools.check_available(t)]
        if include_rna and not tools.check_available("salmon"):
            missing.append("salmon")
        if missing:
            raise RuntimeError(f"Required tool(s) not installed: {', '.join(missing)}. Install them before setup can build indexes.")

        start = time.time()
        if os.path.exists(fasta_path):
            log(f"{fasta_path} already exists, skipping download")
        else:
            download_fasta(source["fasta_url"], fasta_path)

        index_marker = fasta_path + ".bwt.2bit.64"
        if os.path.exists(index_marker):
            log("bwa-mem2 index already present, skipping")
        else:
            build_bwa_index(fasta_path, tools)
        build_samtools_aux_files(fasta_path, tools)

        outputs = [fasta_path, index_marker]
        rna_summary: dict = {}
        if include_rna:
            try:
                rna_summary = build_rna_reference(args.output_dir, args.threads, args.ensembl_release, tools)
                outputs.append(rna_summary["salmonIndexDir"])
            except Exception as exc:  # noqa: BLE001 - RNA support is best-effort, don't fail DNA setup over it
                log(f"WARNING: RNA reference build failed (expression quantification will be unavailable): {exc}")
                rna_summary = {"rnaReferenceError": str(exc)}

        summary = {
            "genome": args.genome,
            "fastaPath": fasta_path,
            "durationSeconds": round(time.time() - start, 1),
            "fastaSizeMb": round(io_utils.file_size_mb(fasta_path), 1),
            **rna_summary,
        }
    except Exception as exc:  # noqa: BLE001
        emit_failure(str(exc))
        return

    emit_success(f"Reference '{args.genome}' ready at {fasta_path}", outputs, summary)


if __name__ == "__main__":
    main()
