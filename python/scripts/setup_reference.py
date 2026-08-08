#!/usr/bin/env python3
"""Download a reference genome and build the aligner indexes the pipeline needs.

Meant to be run once per environment (dev machine or, more realistically, the
cloud server this app is deployed to) before step 2 (alignment) can run for
real. Invoked automatically by AlignmentService when the reference is missing
and disk space allows; can also be run by hand:

    python python/scripts/setup_reference.py --genome chr21_test --output-dir data/references/chr21_test
    python python/scripts/setup_reference.py --genome GRCh38 --output-dir data/references/GRCh38 --dry-run

TEMP-PATCH: bwa-mem2/STAR/samtools CLI flags below are written from documented
usage, not verified against a real install on this dev machine (see CLAUDE.md
"do not guess external interfaces") — deferred to server pass. Source URLs are
not guesses: the chr21 URL is the exact one already used in
docs/TECHNICAL_SPEC.md §7's fixture-generation walkthrough; the full-genome
URL is the same UCSC host/convention, just the whole-genome bigZips path
instead of the single-chromosome one.

Prebuilt aligner indexes aren't something you can reliably download — they're
tool-version-specific binaries with no stable canonical distribution the way
the FASTA itself has, so this builds the bwa-mem2/STAR index locally after
downloading the FASTA. That's the standard approach every pipeline uses.
"""
from __future__ import annotations

import argparse
import gzip
import math
import os
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

# UCSC's refGene GTF — used only for the optional STAR (RNA) index. Lower confidence in this
# exact path than the FASTA URLs above (not referenced elsewhere in this repo), so STAR/RNA
# support is opt-in and failures here are logged as warnings rather than aborting the whole run.
GTF_URL = "https://hgdownload.soe.ucsc.edu/goldenPath/hg38/bigZips/genes/hg38.refGene.gtf.gz"


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser()
    p.add_argument("--genome", required=True, choices=sorted(GENOME_SOURCES.keys()))
    p.add_argument("--output-dir", required=True)
    p.add_argument("--threads", type=int, default=4)
    p.add_argument("--include-star", default="false")
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


def download_fasta(url: str, dest_fasta: str) -> None:
    io_utils.ensure_dir(os.path.dirname(dest_fasta))
    gz_path = dest_fasta + ".gz"

    log(f"Downloading {url}")
    start = time.time()

    def _progress(block_num: int, block_size: int, total_size: int) -> None:
        if total_size > 0 and block_num % 200 == 0:
            downloaded = block_num * block_size
            log_progress(min(downloaded, total_size), total_size, "downloading reference FASTA")

    urllib.request.urlretrieve(url, gz_path, reporthook=_progress)
    log(f"Download complete in {time.time() - start:.0f}s ({io_utils.file_size_mb(gz_path):.0f}MB compressed)")

    log("Decompressing...")
    with gzip.open(gz_path, "rb") as src, open(dest_fasta, "wb") as dst:
        shutil.copyfileobj(src, dst)
    os.remove(gz_path)
    log(f"Reference FASTA ready at {dest_fasta} ({io_utils.file_size_mb(dest_fasta):.0f}MB)")


def build_bwa_index(fasta_path: str, tools: ToolConfig) -> None:
    log("Building bwa-mem2 index (this is the slow part for a full genome)...")
    io_utils.run_command([tools.require("bwa_mem2"), "index", fasta_path], "bwa-mem2 index")


def build_samtools_aux_files(fasta_path: str, tools: ToolConfig) -> None:
    io_utils.run_command([tools.require("samtools"), "faidx", fasta_path], "samtools faidx")
    dict_path = os.path.splitext(fasta_path)[0] + ".dict"
    if not os.path.exists(dict_path):
        io_utils.run_command([tools.samtools, "dict", fasta_path, "-o", dict_path], "samtools dict")


def build_star_index(fasta_path: str, output_dir: str, threads: int, tools: ToolConfig) -> dict:
    """Best-effort — RNA alignment is optional in this pipeline, so a failure here is
    logged and returned as a warning rather than aborting the (already-complete) DNA setup."""
    gtf_path = os.path.join(output_dir, "annotation.gtf")
    star_dir = os.path.join(output_dir, "star_index")
    io_utils.ensure_dir(star_dir)

    log(f"Downloading GTF annotation from {GTF_URL}")
    gz_path = gtf_path + ".gz"
    urllib.request.urlretrieve(GTF_URL, gz_path)
    with gzip.open(gz_path, "rb") as src, open(gtf_path, "wb") as dst:
        shutil.copyfileobj(src, dst)
    os.remove(gz_path)

    genome_length = sum(len(seq) for _, seq in io_utils.read_fasta(fasta_path))
    # STAR's documented formula for small genomes (its default --genomeSAindexNbases 14
    # over-allocates and errors on anything much smaller than a full human genome).
    sa_index_n_bases = min(14, int(math.log2(max(genome_length, 2)) / 2 - 1))

    io_utils.run_command(
        [
            tools.require("star"), "--runMode", "genomeGenerate",
            "--genomeDir", star_dir, "--genomeFastaFiles", fasta_path,
            "--sjdbGTFfile", gtf_path, "--sjdbOverhang", "100",
            "--runThreadN", str(threads), "--genomeSAindexNbases", str(sa_index_n_bases),
        ],
        "STAR genomeGenerate",
    )
    return {"starIndexDir": star_dir, "gtf": gtf_path}


def main() -> None:
    args = parse_args()
    dry_run = _flag(args.dry_run)
    include_star = _flag(args.include_star)
    source = GENOME_SOURCES[args.genome]
    fasta_path = os.path.join(args.output_dir, source["fasta_name"])

    try:
        check_disk_space(args.output_dir, source["required_bytes"])

        if dry_run:
            summary = {
                "dryRun": True, "genome": args.genome, "fastaUrl": source["fasta_url"],
                "targetFasta": fasta_path, "requiredBytes": source["required_bytes"], "includeStar": include_star,
            }
            emit_success(f"Dry run: would download and index '{args.genome}' at {fasta_path}", [], summary)
            return

        tools = ToolConfig.from_env()
        missing = [t for t in ("bwa_mem2", "samtools") if not tools.check_available(t)]
        if include_star and not tools.check_available("star"):
            missing.append("star")
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
        star_summary: dict = {}
        if include_star:
            try:
                star_summary = build_star_index(fasta_path, args.output_dir, args.threads, tools)
                outputs.append(star_summary["starIndexDir"])
            except Exception as exc:  # noqa: BLE001 - RNA support is best-effort, don't fail DNA setup over it
                log(f"WARNING: STAR index build failed (RNA alignment will be unavailable): {exc}")
                star_summary = {"starIndexError": str(exc)}

        summary = {
            "genome": args.genome,
            "fastaPath": fasta_path,
            "durationSeconds": round(time.time() - start, 1),
            "fastaSizeMb": round(io_utils.file_size_mb(fasta_path), 1),
            **star_summary,
        }
    except Exception as exc:  # noqa: BLE001
        emit_failure(str(exc))
        return

    emit_success(f"Reference '{args.genome}' ready at {fasta_path}", outputs, summary)


if __name__ == "__main__":
    main()
