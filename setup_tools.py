#!/usr/bin/env python3
"""Install every bioinformatics tool this pipeline needs, in one shot.

Run this ONCE on the machine that will actually execute the pipeline (the
cloud server) — not on a disk-constrained dev laptop. It does not touch
patient data or reference genomes; see python/scripts/setup_reference.py for
that (and run it separately, after this).

    python3 setup_tools.py                  # install everything into a new conda env
    python3 setup_tools.py --dry-run         # show the plan without installing anything
    python3 setup_tools.py --env-name myenv  # custom conda env name

Why conda instead of downloading raw binaries: bwa-mem2/samtools/STAR/GATK4/
OptiType/VEP are compiled tools with real transitive dependencies (htslib,
Java, Perl modules, razers3, R, ...) that differ by platform. There is no
single stable "prebuilt binary" URL for all of them the way there is for a
plain source download, and guessing per-platform binary URLs is exactly what
CLAUDE.md's "do not guess external interfaces" rule warns against. conda's
bioconda channel is the standard, actually-maintained distribution mechanism
for this exact toolset — it's also the path docs/PROJECT_PLAN.md's own open
questions section pointed at ("Conda environments are the lighter
alternative [to Docker]").

What this script does NOT and CANNOT install (flagged clearly at the end):
  - NetMHCpan/NetMHCIIpan for pvactools' full function — DTU Health Tech
    requires manual academic registration; there's no programmatic download.
  - BigMHC / PRIME for step 8 immunogenicity — no simple package exists;
    the pipeline runs on its stub predictor until/unless these are wired in
    by hand.
  - The VEP cache (several GB) — off by default; database mode (VEP querying
    Ensembl over the network) is used instead, per docs/TECHNICAL_SPEC.md's
    own Tier-1 guidance. Pass --vep-cache to fetch it if you want offline VEP.
"""
from __future__ import annotations

import argparse
import os
import shutil
import subprocess
import sys

# Packages come from bioconda (+ conda-forge for shared deps); these are the
# standard, actively-maintained conda package names for each tool.
CONDA_PACKAGES = [
    "bioconda::bwa-mem2",
    "bioconda::samtools",
    "bioconda::star",
    "bioconda::salmon",
    "bioconda::gatk4",
    "bioconda::optitype",
    "bioconda::ensembl-vep",
    "bioconda::pvactools",
]

BIGMHC_REPO_URL = "https://github.com/KarchinLab/bigmhc.git"

VEP_CACHE_PACKAGE = "bioconda::ensembl-vep"  # already installed above; --vep-cache just runs vep_install after

# Conda env with all of the above (mostly GATK's JDK, VEP's Perl stack, and
# OptiType's razers3/HDF5 deps) — generous estimate, checked before starting.
REQUIRED_FREE_GB = 10


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--env-name", default="neoantigen", help="conda environment name to create/update")
    p.add_argument("--dry-run", action="store_true", help="print the plan without installing anything")
    p.add_argument("--skip-models", action="store_true", help="skip mhcflurry model weight download")
    p.add_argument("--skip-vep-cache", action="store_true",
                    help="skip the VEP cache (several GB). On by default: this app runs with no outbound "
                         "network access at request time, so VEP's database mode (which queries Ensembl "
                         "live) won't work — the cache is what makes VEP work offline.")
    p.add_argument("--include-bigmhc", action="store_true",
                    help="best-effort: git-clone BigMHC (github.com/KarchinLab/bigmhc) for step 8 "
                         "immunogenicity prediction, so it doesn't need NetMHCpan/IEDB's gated registration "
                         "the way the spec's default (PRIME) does. Unverified CLI — see predict_immunogenicity.py.")
    p.add_argument("--yes", action="store_true", help="don't prompt for confirmation before installing")
    return p.parse_args()


def find_conda() -> str:
    for candidate in ("mamba", "conda"):
        path = shutil.which(candidate)
        if path:
            return candidate
    print(
        "Neither `conda` nor `mamba` found on PATH.\n"
        "Install Miniforge first (includes conda + the bioconda/conda-forge channels\n"
        "pre-configured), then re-run this script:\n"
        "  https://github.com/conda-forge/miniforge#miniforge3\n",
        file=sys.stderr,
    )
    sys.exit(1)


def check_disk_space(required_gb: int) -> None:
    free_gb = shutil.disk_usage(".").free / (1024**3)
    if free_gb < required_gb:
        print(
            f"Not enough free disk space: need ~{required_gb}GB for the tool environment, "
            f"have {free_gb:.1f}GB free. Aborting before downloading anything.",
            file=sys.stderr,
        )
        sys.exit(1)
    print(f"Disk check OK: {free_gb:.1f}GB free (need ~{required_gb}GB).")


def run(cmd: list[str], dry_run: bool) -> None:
    print(f"$ {' '.join(cmd)}")
    if dry_run:
        return
    result = subprocess.run(cmd)
    if result.returncode != 0:
        print(f"Command failed (exit {result.returncode}): {' '.join(cmd)}", file=sys.stderr)
        sys.exit(result.returncode)


def main() -> None:
    args = parse_args()

    print("Packages to install (bioconda/conda-forge):")
    for pkg in CONDA_PACKAGES:
        print(f"  - {pkg}")
    print(f"Conda environment: {args.env_name}")
    print(f"VEP cache: {'skipped (VEP will not work without outbound network access)' if args.skip_vep_cache else 'yes (several GB — required for offline VEP)'}")
    print(f"mhcflurry model weights: {'skipped' if args.skip_models else 'yes (~200MB)'}")
    print(f"BigMHC (step 8, no registration needed): {'yes (best-effort, unverified)' if args.include_bigmhc else 'no'}")
    print()

    if not args.yes and not args.dry_run:
        confirm = input("Proceed with installation? [y/N] ").strip().lower()
        if confirm != "y":
            print("Aborted.")
            return

    conda_bin = "conda" if args.dry_run else find_conda()
    if not args.dry_run:
        check_disk_space(REQUIRED_FREE_GB)

    # One `create` call with all packages resolves the environment together, which is far
    # more likely to succeed than installing them one at a time into an existing env.
    run(
        [conda_bin, "create", "-n", args.env_name, "-y", "-c", "bioconda", "-c", "conda-forge", *CONDA_PACKAGES],
        args.dry_run,
    )

    run(
        [conda_bin, "run", "-n", args.env_name, "pip", "install", "-r", "python/requirements.txt"],
        args.dry_run,
    )

    if not args.skip_models:
        run(
            [conda_bin, "run", "-n", args.env_name, "mhcflurry-downloads", "fetch", "models_class1_presentation"],
            args.dry_run,
        )

    if not args.skip_vep_cache:
        run(
            [conda_bin, "run", "-n", args.env_name, "vep_install", "-a", "cf", "-s", "homo_sapiens", "-y", "GRCh38", "--NO_HTSLIB"],
            args.dry_run,
        )

    if args.include_bigmhc:
        bigmhc_dir = "tools/bigmhc"
        if os.path.exists(bigmhc_dir):
            print(f"{bigmhc_dir} already exists, skipping clone")
        else:
            run(["git", "clone", "--depth", "1", BIGMHC_REPO_URL, bigmhc_dir], args.dry_run)
        requirements_path = os.path.join(bigmhc_dir, "requirements.txt")
        if args.dry_run or os.path.exists(requirements_path):
            run([conda_bin, "run", "-n", args.env_name, "pip", "install", "-r", requirements_path], args.dry_run)
        else:
            print(f"WARNING: {requirements_path} not found after clone — install BigMHC's dependencies by hand per its README")
        print(
            "NOTE: BigMHC's pretrained model weights are not bundled in the repo clone — "
            "download them per the instructions at https://github.com/KarchinLab/bigmhc "
            "(public, no registration, but not something this script can fetch blind)."
        )

    print()
    print("=" * 72)
    print("Done." if not args.dry_run else "Dry run complete — nothing was installed.")
    print("=" * 72)
    print(
        f"""
Next steps:
  1. Point the backend at this conda env's binaries. Get the env's bin path with:
       conda run -n {args.env_name} which bwa-mem2
     Then set each tool's path in backend's appsettings (App:ToolPaths) or
     PythonExecutable to that env's `python3`/tool binaries, e.g.:
       App:PythonExecutable = "<conda envs dir>/{args.env_name}/bin/python3"
       App:ToolPaths:bwa-mem2 = "<conda envs dir>/{args.env_name}/bin/bwa-mem2"
       (...and similarly for samtools, STAR, gatk4, OptiTypePipeline.py, vep, pvacvector)

  2. Download the reference genome + build its bwa-mem2 index, and (if you want
     real expression filtering) the Salmon transcriptome index (separate script,
     separate disk budget):
       conda run -n {args.env_name} python3 python/scripts/setup_reference.py \\
           --genome GRCh38 --output-dir data/references/GRCh38 --include-rna

  3. Still NOT fully automatable (manual steps, see this file's docstring):
       - NetMHCpan/NetMHCIIpan for pvactools' full function (DTU Health Tech,
         requires academic registration — https://services.healthtech.dtu.dk).
         design_vaccine.py now defaults to MHCflurry-based prediction instead,
         which avoids this, but it's unverified against a real install.
       - BigMHC's pretrained weights (if you passed --include-bigmhc, the repo
         is cloned but the weights still need fetching per its own README).
"""
    )


if __name__ == "__main__":
    main()
