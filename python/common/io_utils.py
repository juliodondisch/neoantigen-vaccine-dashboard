from __future__ import annotations

import csv
import gzip
import os
import subprocess
import time
from datetime import datetime
from typing import Iterable, Iterator


def _open_maybe_gzip(path: str, mode: str = "rt"):
    return gzip.open(path, mode) if path.endswith(".gz") else open(path, mode)


def read_vcf(path: str) -> Iterator[dict]:
    with _open_maybe_gzip(path) as fh:
        header: list[str] = []
        for line in fh:
            line = line.rstrip("\n")
            if line.startswith("##"):
                continue
            if line.startswith("#CHROM"):
                header = line.lstrip("#").split("\t")
                continue
            if not line.strip():
                continue
            fields = line.split("\t")
            record = dict(zip(header, fields)) if header else {}
            if not header:
                # Tolerate hand-written fixture VCFs without a #CHROM header line.
                cols = ["CHROM", "POS", "ID", "REF", "ALT", "QUAL", "FILTER", "INFO"]
                record = dict(zip(cols, fields))
            yield record


def write_vcf(path: str, records: Iterable[dict], header: str) -> None:
    with open(path, "w") as fh:
        fh.write(header.rstrip("\n") + "\n")
        for r in records:
            fh.write(
                "\t".join(
                    str(r.get(c, "."))
                    for c in ["CHROM", "POS", "ID", "REF", "ALT", "QUAL", "FILTER", "INFO"]
                )
                + "\n"
            )


def read_tsv(path: str) -> list[dict]:
    with _open_maybe_gzip(path) as fh:
        reader = csv.DictReader(fh, delimiter="\t")
        return list(reader)


def write_tsv(path: str, rows: list[dict], columns: list[str]) -> None:
    with open(path, "w", newline="") as fh:
        writer = csv.DictWriter(fh, fieldnames=columns, delimiter="\t", extrasaction="ignore")
        writer.writeheader()
        for row in rows:
            writer.writerow(row)


def read_fasta(path: str) -> Iterator[tuple[str, str]]:
    name = None
    seq_parts: list[str] = []
    with _open_maybe_gzip(path) as fh:
        for line in fh:
            line = line.rstrip("\n")
            if line.startswith(">"):
                if name is not None:
                    yield name, "".join(seq_parts)
                name = line[1:].split()[0]
                seq_parts = []
            else:
                seq_parts.append(line.strip())
        if name is not None:
            yield name, "".join(seq_parts)


def write_fasta(path: str, records: list[tuple[str, str]], line_width: int = 60) -> None:
    with open(path, "w") as fh:
        for name, seq in records:
            fh.write(f">{name}\n")
            for i in range(0, len(seq), line_width):
                fh.write(seq[i : i + line_width] + "\n")


def ensure_dir(path: str) -> None:
    os.makedirs(path, exist_ok=True)


def timestamped_name(base: str, extension: str) -> str:
    ts = datetime.now().strftime("%Y%m%d_%H%M%S")
    ext = extension if extension.startswith(".") else f".{extension}"
    return f"{base}_{ts}{ext}"


def file_size_mb(path: str) -> float:
    return os.path.getsize(path) / (1024 * 1024) if os.path.exists(path) else 0.0


def check_file_exists(path: str, description: str) -> None:
    if not path or not os.path.exists(path):
        raise FileNotFoundError(f"{description} not found at: {path}")


def run_command(cmd: list[str], description: str, timeout: int | None = None) -> subprocess.CompletedProcess:
    start = time.time()
    result = subprocess.run(cmd, capture_output=True, text=True, timeout=timeout)
    elapsed = time.time() - start
    if result.returncode != 0:
        raise RuntimeError(
            f"{description} failed (exit {result.returncode}, {elapsed:.1f}s): {result.stderr.strip()[:2000]}"
        )
    return result
