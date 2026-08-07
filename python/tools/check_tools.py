#!/usr/bin/env python3
"""Report which bioinformatics tools/packages/reference files are available locally.

Run directly: `python python/tools/check_tools.py`
"""
from __future__ import annotations

import importlib
import json
import os
import shutil
import subprocess
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", ".."))
from python.common.config import get_data_root

TOOLS = {
    "bwa-mem2": ["bwa-mem2", "version"],
    "samtools": ["samtools", "--version"],
    "gatk": ["gatk", "--version"],
    "STAR": ["STAR", "--version"],
    "vep": ["vep", "--help"],
    "OptiType": ["OptiTypePipeline.py", "--help"],
    "mhcflurry-predict": ["mhcflurry-predict", "--version"],
    "pvacseq": ["pvacseq", "--version"],
    "pvacvector": ["pvacvector", "--version"],
}

PACKAGES = ["pandas", "numpy", "pysam", "Bio", "mhcflurry"]


def check_tool(name: str, version_cmd: list[str]) -> dict:
    path = shutil.which(version_cmd[0])
    if path is None:
        return {"name": name, "available": False, "path": None, "version": None}
    try:
        result = subprocess.run(version_cmd, capture_output=True, text=True, timeout=10)
        version = (result.stdout or result.stderr).strip().splitlines()[0] if (result.stdout or result.stderr) else ""
    except Exception as exc:  # noqa: BLE001
        version = f"error: {exc}"
    return {"name": name, "available": True, "path": path, "version": version}


def check_all() -> list[dict]:
    return [check_tool(name, cmd) for name, cmd in TOOLS.items()]


def check_python_packages() -> list[dict]:
    results = []
    for pkg in PACKAGES:
        try:
            mod = importlib.import_module(pkg)
            results.append({"name": pkg, "available": True, "version": getattr(mod, "__version__", "unknown")})
        except Exception as exc:  # noqa: BLE001
            results.append({"name": pkg, "available": False, "error": str(exc)})
    return results


def check_reference_files(reference_root: str) -> list[dict]:
    expected = [
        os.path.join(reference_root, "chr21_test", "chr21.fa"),
        os.path.join(reference_root, "proteome", "mini_proteome.fasta"),
        os.path.join(reference_root, "hla", "optitype_reference"),
    ]
    return [{"path": p, "exists": os.path.exists(p)} for p in expected]


def main() -> None:
    report = {
        "tools": check_all(),
        "packages": check_python_packages(),
        "references": check_reference_files(os.path.join(get_data_root(), "references")),
    }
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
