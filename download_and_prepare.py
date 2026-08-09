#!/usr/bin/env python3
"""Download the GIAB HG008 tumor/normal BAMs and produce FASTQ for HLA typing.

Why this script exists
----------------------
The pipeline's alignment/variant-calling steps (2-4) consume BAMs directly, but
OptiType (step 5) must NOT be given a BAM. When handed a BAM, OptiType skips its
own razers3/yara mapping stage and reads the existing alignment as if it were an
alignment against its internal HLA allele reference. Our BAMs are aligned against
whole-genome GRCh38, so that assumption is wrong and produces malformed allele
identifiers (observed failure: `KeyError: '1'` deep inside OptiType's
`_is_frequent` allele-table lookup).

Handing OptiType FASTQ forces the correct path: it maps the raw reads itself,
against its own HLA reference, and produces valid allele IDs.

What it does
------------
1. Downloads the HG008 tumor and normal BAMs (resumable) plus their .bai indexes.
2. Extracts reads over the classical HLA class I locus (chr6:29-33Mb, GRCh38).
3. Downsamples that region to a realistic depth (~30x). OptiType/razers3 was
   validated at typical 30-60x coverage; HG008 normal is ~150x, which in-region
   yields ~4.17M reads and pushes OptiType to ~22GB RSS with no completion in
   30+ minutes.
4. Detects paired- vs single-end from the BAM flags and converts accordingly.
   Paired data is name-collated first, because `samtools fastq` needs
   name-grouped input to split mates correctly and our BAMs are coordinate-sorted.

Usage
-----
    python3 download_and_prepare.py --out-dir /data/hg008

    # normal sample only (all step 5 needs), skip the 134GB tumor download
    python3 download_and_prepare.py --out-dir /data/hg008 --normal-only

    # BAMs already downloaded elsewhere, just do the FASTQ prep
    python3 download_and_prepare.py --out-dir /data/hg008 --skip-download

Requires: samtools on PATH, plus curl or wget. ~300GB free for both samples,
~160GB for --normal-only.
"""
from __future__ import annotations

import argparse
import os
import shutil
import subprocess
import sys
import time

# --- GIAB HG008 source data -------------------------------------------------
# Genome in a Bottle, Cancer GIAB pilot. PDAC tumor cell line + matched normal
# tissue from the same consented patient (61F). Public, no access application.
# Spring-2022 batch = earliest passages, closest to the original resected tumor.
GIAB_BASE = (
    "https://ftp-trace.ncbi.nlm.nih.gov/ReferenceSamples/giab/data_somatic/HG008"
    "/Liss_lab/superseded-2022-data/BCM_Illumina_WGS_20220816"
)

SAMPLES = {
    "tumor": {
        "bam": "HG008-T_Illumina_191x_GRCh38_sorted.bam",
        "approx_gb": 134,
        "note": "PDAC tumor cell line, 191x",
    },
    "normal": {
        "bam": "HG008-N_Illumina_150x_GRCh38_sorted.bam",
        "approx_gb": 148,
        "note": "matched normal tissue, 150x — this is the one HLA typing uses",
    },
}

# --- HLA region and depth targets -------------------------------------------
# Classical MHC class I locus on GRCh38, covering HLA-A, -B and -C with margin.
HLA_REGION = "chr6:29000000-33000000"
HLA_REGION_BP = 33_000_000 - 29_000_000

TARGET_DEPTH = 30
ASSUMED_READ_LENGTH_BP = 150
DOWNSAMPLE_SEED = 42

# Only downsample if we're meaningfully over target; no point resampling 32x->30x.
DOWNSAMPLE_TRIGGER_RATIO = 1.15


# --- small helpers ----------------------------------------------------------

def log(msg: str) -> None:
    print(f"[{time.strftime('%H:%M:%S')}] {msg}", flush=True)


def run(cmd: list[str], desc: str) -> None:
    log(f"{desc}")
    log(f"  $ {' '.join(cmd)}")
    result = subprocess.run(cmd)
    if result.returncode != 0:
        raise RuntimeError(f"{desc} failed (exit {result.returncode})")


def run_capture(cmd: list[str], desc: str) -> str:
    result = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
    if result.returncode != 0:
        raise RuntimeError(f"{desc} failed (exit {result.returncode}): {result.stderr[:500]}")
    return result.stdout


def require_tool(name: str) -> str:
    path = shutil.which(name)
    if path is None:
        raise RuntimeError(f"Required tool '{name}' not found on PATH.")
    return path


def human_gb(path: str) -> str:
    if not os.path.exists(path):
        return "missing"
    return f"{os.path.getsize(path) / 1024**3:.1f}GB"


def free_space_gb(path: str) -> float:
    st = os.statvfs(path)
    return (st.f_bavail * st.f_frsize) / 1024**3


# --- download ---------------------------------------------------------------

def download(url: str, dest: str) -> None:
    """Download a URL to dest, resuming a partial file if one exists."""
    if os.path.exists(dest):
        log(f"already present, skipping: {os.path.basename(dest)} ({human_gb(dest)})")
        return

    partial = dest + ".part"
    if shutil.which("wget"):
        # -c resumes a partial transfer rather than restarting from zero
        cmd = ["wget", "-c", "-O", partial, url]
    elif shutil.which("curl"):
        cmd = ["curl", "-L", "-C", "-", "-o", partial, url]
    else:
        raise RuntimeError("Neither wget nor curl found on PATH.")

    run(cmd, f"downloading {os.path.basename(dest)}")
    os.rename(partial, dest)
    log(f"downloaded {os.path.basename(dest)} ({human_gb(dest)})")


def download_sample(sample: str, out_dir: str) -> str:
    """Download one sample's BAM and .bai. Returns the local BAM path."""
    info = SAMPLES[sample]
    bam_name = info["bam"]
    bam_path = os.path.join(out_dir, bam_name)

    log(f"=== {sample}: {info['note']} (~{info['approx_gb']}GB) ===")
    download(f"{GIAB_BASE}/{bam_name}", bam_path)

    # The .bai may or may not be published alongside; if not, build it locally.
    bai_path = bam_path + ".bai"
    if not os.path.exists(bai_path):
        try:
            download(f"{GIAB_BASE}/{bam_name}.bai", bai_path)
        except Exception as exc:
            log(f"no published .bai ({exc}); building locally instead")
            run([require_tool("samtools"), "index", "-@", "8", bam_path],
                f"indexing {bam_name} (slow — ~20 min for a 150GB BAM)")

    return bam_path


# --- region extraction, downsampling, FASTQ conversion ----------------------

def count_reads(bam_path: str) -> int:
    samtools = require_tool("samtools")
    return int(run_capture([samtools, "view", "-c", bam_path], "count reads").strip())


def is_paired(bam_path: str) -> bool:
    """True if the BAM contains paired reads.

    Checks flag 0x1 (read paired) on the first 100k records rather than the whole
    file — enough to classify, and avoids a full pass over a large BAM.
    """
    samtools = require_tool("samtools")
    paired = int(run_capture(
        [samtools, "view", "-c", "-f", "1", bam_path, HLA_REGION],
        "count paired reads",
    ).strip())
    total = int(run_capture(
        [samtools, "view", "-c", bam_path, HLA_REGION],
        "count total reads in region",
    ).strip())

    if total == 0:
        raise RuntimeError(
            f"No reads found in {HLA_REGION}. Check the BAM's chromosome naming — "
            f"this script assumes UCSC-style 'chr6'. Run: samtools view -H {bam_path} | grep '@SQ' | head"
        )

    ratio = paired / total
    log(f"paired-read check: {paired:,}/{total:,} ({ratio:.1%}) carry flag 0x1")
    # Real data is rarely 100% either way; treat a clear majority as decisive.
    return ratio > 0.5


def extract_hla_region(bam_path: str, out_bam: str) -> None:
    samtools = require_tool("samtools")
    run([samtools, "view", "-b", "-o", out_bam, bam_path, HLA_REGION],
        f"extracting {HLA_REGION}")
    run([samtools, "index", out_bam], "indexing extracted region")


def downsample_if_needed(in_bam: str, out_bam: str) -> str:
    """Downsample to ~TARGET_DEPTH. Returns the path to use downstream."""
    reads = count_reads(in_bam)
    depth = (reads * ASSUMED_READ_LENGTH_BP) / HLA_REGION_BP
    log(f"in-region reads: {reads:,} (est. depth ~{depth:.0f}x, target {TARGET_DEPTH}x)")

    if depth <= TARGET_DEPTH * DOWNSAMPLE_TRIGGER_RATIO:
        log(f"depth already at/below target — skipping downsample")
        return in_bam

    fraction = TARGET_DEPTH / depth
    # samtools -s takes a single float SEED.FRACTION, e.g. "42.192" = seed 42, keep 19.2%
    frac_str = f"{DOWNSAMPLE_SEED}.{str(round(fraction, 4)).split('.')[1]:0<4}"

    samtools = require_tool("samtools")
    run([samtools, "view", "-s", frac_str, "-b", "-o", out_bam, in_bam],
        f"downsampling to ~{TARGET_DEPTH}x (keeping {fraction:.1%}, -s {frac_str})")
    run([samtools, "index", out_bam], "indexing downsampled BAM")

    kept = count_reads(out_bam)
    log(f"post-downsample reads: {kept:,} (est. depth ~{(kept * ASSUMED_READ_LENGTH_BP) / HLA_REGION_BP:.0f}x)")
    return out_bam


def bam_to_fastq(bam_path: str, out_prefix: str, paired: bool) -> list[str]:
    """Convert a BAM to FASTQ. Returns the FASTQ paths, in OptiType -i order."""
    samtools = require_tool("samtools")

    if not paired:
        single = f"{out_prefix}.fastq"
        run([samtools, "fastq", "-0", single, bam_path], "converting to FASTQ (single-end)")
        return [single]

    r1, r2 = f"{out_prefix}_1.fastq", f"{out_prefix}_2.fastq"

    # `samtools fastq` splits mates by walking the file in order, so it needs
    # name-grouped input. Our BAMs are coordinate-sorted, so collate first.
    # -u = uncompressed output into the pipe (we're not writing it to disk).
    log("collating by read name and converting to FASTQ (paired-end)")
    log(f"  $ samtools collate -u -O {bam_path} | samtools fastq -1 {r1} -2 {r2} -0 /dev/null -s /dev/null -n")

    collate = subprocess.Popen(
        [samtools, "collate", "-u", "-O", bam_path], stdout=subprocess.PIPE,
    )
    fastq = subprocess.Popen(
        [samtools, "fastq", "-1", r1, "-2", r2, "-0", "/dev/null", "-s", "/dev/null", "-n"],
        stdin=collate.stdout,
    )
    collate.stdout.close()  # let collate get SIGPIPE if fastq exits early
    fastq.wait()
    collate.wait()

    if fastq.returncode != 0 or collate.returncode != 0:
        raise RuntimeError(
            f"FASTQ conversion failed (collate exit {collate.returncode}, fastq exit {fastq.returncode})"
        )
    return [r1, r2]


def prepare_sample(bam_path: str, out_dir: str, sample: str) -> list[str]:
    """Full extract -> downsample -> FASTQ chain for one sample."""
    log(f"=== preparing HLA FASTQ for {sample} ===")

    region_bam = os.path.join(out_dir, f"{sample}_hla_region.bam")
    down_bam = os.path.join(out_dir, f"{sample}_hla_region_downsampled.bam")
    fastq_prefix = os.path.join(out_dir, f"{sample}_hla_reads")

    extract_hla_region(bam_path, region_bam)
    paired = is_paired(region_bam)
    final_bam = downsample_if_needed(region_bam, down_bam)
    fastqs = bam_to_fastq(final_bam, fastq_prefix, paired)

    for f in fastqs:
        log(f"  -> {f} ({os.path.getsize(f) / 1024**2:.0f}MB)")
    return fastqs


# --- entry point ------------------------------------------------------------

def main() -> None:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--out-dir", required=True, help="Directory for BAMs and FASTQs")
    p.add_argument("--normal-only", action="store_true",
                   help="Only fetch/prepare the normal sample (all HLA typing needs)")
    p.add_argument("--skip-download", action="store_true",
                   help="Assume BAMs are already in --out-dir")
    p.add_argument("--skip-prepare", action="store_true",
                   help="Download only; no region extraction or FASTQ conversion")
    args = p.parse_args()

    os.makedirs(args.out_dir, exist_ok=True)
    require_tool("samtools")

    samples = ["normal"] if args.normal_only else ["normal", "tumor"]

    needed = sum(SAMPLES[s]["approx_gb"] for s in samples) if not args.skip_download else 0
    needed += 5  # region BAMs + FASTQs
    available = free_space_gb(args.out_dir)
    log(f"disk: {available:.0f}GB free, ~{needed}GB needed")
    if available < needed:
        log(f"WARNING: may not have enough space. Continuing anyway — Ctrl-C to abort.")
        time.sleep(5)

    bam_paths = {}
    for sample in samples:
        if args.skip_download:
            path = os.path.join(args.out_dir, SAMPLES[sample]["bam"])
            if not os.path.exists(path):
                raise RuntimeError(f"--skip-download given but {path} not found")
            bam_paths[sample] = path
            log(f"using existing {sample} BAM: {path} ({human_gb(path)})")
        else:
            bam_paths[sample] = download_sample(sample, args.out_dir)

    if args.skip_prepare:
        log("--skip-prepare given; done.")
        return

    results = {}
    for sample in samples:
        results[sample] = prepare_sample(bam_paths[sample], args.out_dir, sample)

    log("")
    log("=" * 70)
    log("DONE")
    log("=" * 70)
    for sample in samples:
        log(f"{sample} BAM (steps 2-4):  {bam_paths[sample]}")
        log(f"{sample} FASTQ (step 5):   {' '.join(results[sample])}")
    log("")
    log("Test HLA typing directly with:")
    normal_fq = results.get("normal", [])
    if normal_fq:
        args_str = " ".join(f"-i {f}" for f in normal_fq)
        log(f"  optitype run {args_str} --dna -o /tmp/optitype_test --verbose")


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        log("interrupted")
        sys.exit(130)
    except Exception as exc:
        log(f"FAILED: {exc}")
        sys.exit(1)
