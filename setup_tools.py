#!/usr/bin/env python3
"""Install everything this app needs to run, in one shot: conda itself (if missing),
.NET SDK, Node.js, and every bioinformatics tool the pipeline calls out to.

Run this ONCE on the machine that will actually execute the pipeline (the
cloud server) — not on a disk-constrained dev laptop. It does not touch
patient data or reference genomes; see python/scripts/setup_reference.py for
that (and run it separately, after this).

    python3 setup_tools.py                  # check everything, install what's missing
    python3 setup_tools.py --dry-run         # show the plan without installing anything
    python3 setup_tools.py --env-name myenv  # custom conda env name

Every check below follows the same pattern: is it already there (and good
enough)? If so, skip it. If not, install it. Safe to re-run any time.

Why conda for the bioinformatics tools instead of downloading raw binaries:
bwa-mem2/samtools/STAR/GATK4/OptiType/VEP are compiled tools with real
transitive dependencies (htslib, Java, Perl modules, razers3, R, ...) that
differ by platform. There is no single stable "prebuilt binary" URL for all
of them the way there is for a plain source download, and guessing
per-platform binary URLs is exactly what CLAUDE.md's "do not guess external
interfaces" rule warns against. conda's bioconda channel is the standard,
actually-maintained distribution mechanism for this exact toolset — it's also
the path docs/PROJECT_PLAN.md's own open questions section pointed at
("Conda environments are the lighter alternative [to Docker]"). Node.js is
installed the same way (conda-forge::nodejs) so architecture detection
(x86_64 vs. arm64/Graviton) is handled by conda rather than guessed here.

.NET is the one piece NOT installed via conda — Microsoft's own
dotnet-install.sh is the canonical, architecture-aware installer and is more
reliable than any conda-forge .NET package.

What this script does NOT and CANNOT install (flagged clearly at the end):
  - NetMHCpan/NetMHCIIpan for pvactools' full function — DTU Health Tech
    requires manual academic registration; there's no programmatic download.
    Steps 8/11 route around this via BigMHC/MHCflurry instead (see below).
  - BigMHC's pretrained model weights (--include-bigmhc clones the repo, but
    the weights need fetching per its own README — no confident stable URL).
  - PRIME for step 8 immunogenicity — not wired in at all.
"""
from __future__ import annotations

import argparse
import os
import platform
import shutil
import subprocess
import sys
import urllib.request

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
    "conda-forge::nodejs>=20",
]

BIGMHC_REPO_URL = "https://github.com/KarchinLab/bigmhc.git"

MINIFORGE_BASE_URL = "https://github.com/conda-forge/miniforge/releases/latest/download"
MINIFORGE_INSTALL_DIR = os.path.expanduser("~/miniforge3")

DOTNET_INSTALL_DIR = os.path.expanduser("~/.dotnet")
DOTNET_CHANNEL = "10.0"
DOTNET_MIN_MAJOR = 10

# Conda env with all of the above (mostly GATK's JDK, VEP's Perl stack, Node,
# and OptiType's razers3/HDF5 deps) — generous estimate, checked before starting.
REQUIRED_FREE_GB = 12


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


def run(cmd: list[str], dry_run: bool) -> None:
    print(f"$ {' '.join(cmd)}")
    if dry_run:
        return
    result = subprocess.run(cmd)
    if result.returncode != 0:
        print(f"Command failed (exit {result.returncode}): {' '.join(cmd)}", file=sys.stderr)
        sys.exit(result.returncode)


def _ensure_path_export(export_line: str, label: str) -> None:
    rc_path = os.path.expanduser("~/.bashrc")
    existing = open(rc_path).read() if os.path.exists(rc_path) else ""
    if export_line in existing:
        print(f"{label}: PATH already configured in {rc_path}")
        return
    with open(rc_path, "a") as fh:
        fh.write(f"\n# Added by setup_tools.py for {label}\n{export_line}\n")
    print(f"{label}: added to {rc_path} — run `source ~/.bashrc` (or start a new shell) to pick it up")


# --- conda ------------------------------------------------------------------

def find_conda() -> str | None:
    for candidate in ("mamba", "conda"):
        path = shutil.which(candidate)
        if path:
            return candidate
    candidate = os.path.join(MINIFORGE_INSTALL_DIR, "bin", "conda")
    return candidate if os.path.exists(candidate) else None


def install_conda(dry_run: bool) -> str:
    machine = platform.machine().lower()
    arch = "aarch64" if machine in ("arm64", "aarch64") else "x86_64"
    system = platform.system()
    if system != "Linux":
        print(f"Miniforge auto-install only handles Linux; detected {system}. "
              "Install conda yourself: https://github.com/conda-forge/miniforge#miniforge3", file=sys.stderr)
        sys.exit(1)

    installer_url = f"{MINIFORGE_BASE_URL}/Miniforge3-{system}-{arch}.sh"
    installer_path = "/tmp/miniforge_install.sh"
    print(f"conda/mamba not found — installing Miniforge from {installer_url}")
    if dry_run:
        print(f"$ curl -sSL {installer_url} -o {installer_path} && bash {installer_path} -b -p {MINIFORGE_INSTALL_DIR}")
        return os.path.join(MINIFORGE_INSTALL_DIR, "bin", "conda")

    urllib.request.urlretrieve(installer_url, installer_path)
    run(["bash", installer_path, "-b", "-p", MINIFORGE_INSTALL_DIR], dry_run=False)
    _ensure_path_export(f'export PATH="{MINIFORGE_INSTALL_DIR}/bin:$PATH"', "conda")
    return os.path.join(MINIFORGE_INSTALL_DIR, "bin", "conda")


# --- .NET SDK -----------------------------------------------------------------

def find_dotnet() -> str | None:
    path = shutil.which("dotnet")
    if path:
        return path
    candidate = os.path.join(DOTNET_INSTALL_DIR, "dotnet")
    return candidate if os.path.exists(candidate) else None


def dotnet_version_ok(dotnet_path: str) -> bool:
    try:
        result = subprocess.run([dotnet_path, "--version"], capture_output=True, text=True, timeout=10)
        major = int(result.stdout.strip().split(".")[0])
        return major >= DOTNET_MIN_MAJOR
    except Exception:  # noqa: BLE001
        return False


def install_dotnet(dry_run: bool) -> None:
    print(f"Installing .NET SDK (channel {DOTNET_CHANNEL}) to {DOTNET_INSTALL_DIR}...")
    script_url = "https://dot.net/v1/dotnet-install.sh"
    script_path = "/tmp/dotnet-install.sh"
    if dry_run:
        print(f"$ curl -sSL {script_url} -o {script_path} && bash {script_path} --channel {DOTNET_CHANNEL} --install-dir {DOTNET_INSTALL_DIR}")
        return
    urllib.request.urlretrieve(script_url, script_path)
    os.chmod(script_path, 0o755)
    run([script_path, "--channel", DOTNET_CHANNEL, "--install-dir", DOTNET_INSTALL_DIR], dry_run=False)
    _ensure_path_export(f'export PATH="{DOTNET_INSTALL_DIR}:$PATH"', "dotnet")


# --- main ---------------------------------------------------------------------

def main() -> None:
    args = parse_args()

    print("=" * 72)
    print("Checking runtimes (conda, .NET SDK) and planning bioconda packages")
    print("=" * 72)

    conda_bin = find_conda()
    if conda_bin:
        print(f"conda/mamba: found at {conda_bin}")
    else:
        conda_bin = install_conda(args.dry_run)

    dotnet_path = find_dotnet()
    if dotnet_path and dotnet_version_ok(dotnet_path):
        print(f"dotnet: found at {dotnet_path} (>= {DOTNET_MIN_MAJOR}.0)")
    else:
        if dotnet_path:
            print(f"dotnet found at {dotnet_path} but is older than {DOTNET_MIN_MAJOR}.0 — installing a current SDK")
        install_dotnet(args.dry_run)

    print()
    print("Conda packages to install (bioconda/conda-forge — includes Node.js):")
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
Next steps: see DEPLOY.md for the full sequence. Short version:
  1. If conda/dotnet were just installed, run `source ~/.bashrc` (or open a new shell).
  2. Point the backend at this conda env's tool binaries — get paths with:
       conda run -n {args.env_name} which bwa-mem2
     Set them via environment variables (see DEPLOY.md), e.g.:
       export App__PythonExecutable="$(conda run -n {args.env_name} which python3)"
       export App__ToolPaths__bwa-mem2="$(conda run -n {args.env_name} which bwa-mem2)"
  3. Download the reference genome + build its bwa-mem2 index, and (if you want
     real expression filtering) the Salmon transcriptome index:
       conda run -n {args.env_name} python3 python/scripts/setup_reference.py \\
           --genome GRCh38 --output-dir data/references/GRCh38 --include-rna
  4. Still NOT fully automatable (manual steps, see this file's docstring):
       - NetMHCpan/NetMHCIIpan for pvactools' full function (gated registration)
       - BigMHC's pretrained weights (if using --include-bigmhc)
"""
    )


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


if __name__ == "__main__":
    main()
