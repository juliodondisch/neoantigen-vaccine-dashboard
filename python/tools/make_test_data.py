#!/usr/bin/env python3
"""Build the Tier-1 test fixture set described in docs/TECHNICAL_SPEC.md §7.3.

DEVIATION from spec: the spec's example walkthrough downloads real chr21 from
UCSC and runs wgsim/bwa-mem2 against it. Per CLAUDE.md ("never download
reference genomes or real sequencing data") and this machine's disk budget
(~3.8GB free, not the ~10GB assumed), this script instead *generates* a small
synthetic "reference" sequence and synthetic reads locally — nothing is
downloaded, and the whole fixture set stays in the tens of KB rather than
the ~350MB the spec's real-chr21 approach would use. Logged in
docs/deviations.md. Swap in the real download once run on a server with
proper disk headroom; the function signatures match the spec so callers
don't need to change.
"""
from __future__ import annotations

import argparse
import os
import random
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", ".."))
from python.common import io_utils

BASES = "ACGT"
AMINO_ACIDS = "ACDEFGHIKLMNPQRSTVWY"


def main() -> None:
    p = argparse.ArgumentParser()
    p.add_argument("--out", default=os.path.join(os.path.dirname(__file__), "..", "..", "backend", "tests", "NeoantigenPipeline.Tests", "Fixtures", "data"))
    p.add_argument("--seed", type=int, default=42)
    args = p.parse_args()

    rng = random.Random(args.seed)
    out = os.path.abspath(args.out)
    io_utils.ensure_dir(out)

    ref_dir = os.path.join(out, "tiny")
    io_utils.ensure_dir(ref_dir)
    reference = download_chr21(ref_dir, rng)
    build_bwa_index(reference)

    planted = [{"pos": 500 + i * 300, "ref": "A", "alt": "G"} for i in range(5)]
    simulate_reads(reference, os.path.join(ref_dir, "normal"), n_reads=200, mutation_rate=0.0, seed=args.seed)
    simulate_reads(reference, os.path.join(ref_dir, "tumor"), n_reads=200, mutation_rate=0.15, seed=args.seed, planted=planted)
    generate_truth_vcf(reference, planted, os.path.join(ref_dir, "truth_variants.vcf"))

    vcf_dir = os.path.join(out, "vcf")
    io_utils.ensure_dir(vcf_dir)
    build_golden_consequence_vcf(os.path.join(vcf_dir, "golden_consequences.vcf"))

    proteome_dir = os.path.join(out, "proteome")
    io_utils.ensure_dir(proteome_dir)
    build_mini_proteome(None, 50, os.path.join(proteome_dir, "mini_proteome.fasta"), rng)

    peptide_dir = os.path.join(out, "peptides")
    io_utils.ensure_dir(peptide_dir)
    build_synthetic_candidates(100, ["HLA-A*02:01", "HLA-B*07:02"], os.path.join(peptide_dir, "candidates_100.tsv"), rng)

    print(f"Fixture set written to {out}")


def download_chr21(output_dir: str, rng: random.Random | None = None) -> str:
    """Generates a small synthetic 'chromosome' instead of downloading real chr21 (see module docstring)."""
    rng = rng or random.Random(42)
    seq = "".join(rng.choice(BASES) for _ in range(5000))
    path = os.path.join(output_dir, "synthetic_chr21.fa")
    io_utils.write_fasta(path, [("synthetic_chr21", seq)])
    return path


def build_bwa_index(fasta_path: str) -> None:
    import shutil

    if shutil.which("bwa-mem2") is None:
        print(f"[make_test_data] bwa-mem2 not installed — skipping index build for {fasta_path}", file=sys.stderr)
        return
    io_utils.run_command(["bwa-mem2", "index", fasta_path], "bwa-mem2 index")


def simulate_reads(reference: str, output_prefix: str, n_reads: int, mutation_rate: float, seed: int, planted: list[dict] | None = None) -> tuple[str, str]:
    rng = random.Random(seed)
    name, seq = next(iter(io_utils.read_fasta(reference)))
    mutated = list(seq)
    for m in planted or []:
        if m["pos"] < len(mutated):
            mutated[m["pos"]] = m["alt"]
    mutated_seq = "".join(mutated)

    read_len = 50
    r1_path, r2_path = f"{output_prefix}_R1.fq", f"{output_prefix}_R2.fq"
    with open(r1_path, "w") as r1, open(r2_path, "w") as r2:
        for i in range(n_reads):
            start = rng.randint(0, max(0, len(seq) - read_len))
            source = mutated_seq if rng.random() < mutation_rate else seq
            fragment = source[start : start + read_len]
            qual = "I" * len(fragment)
            r1.write(f"@read{i}/1\n{fragment}\n+\n{qual}\n")
            r2.write(f"@read{i}/2\n{fragment[::-1]}\n+\n{qual}\n")
    return r1_path, r2_path


def generate_truth_vcf(reference: str, planted_mutations: list[dict], output_path: str) -> None:
    lines = ["##fileformat=VCFv4.2", "#CHROM\tPOS\tID\tREF\tALT\tQUAL\tFILTER\tINFO"]
    name, _ = next(iter(io_utils.read_fasta(reference)))
    for m in planted_mutations:
        lines.append(f"{name}\t{m['pos']}\t.\t{m['ref']}\t{m['alt']}\t99\tPASS\tPLANTED=true")
    with open(output_path, "w") as fh:
        fh.write("\n".join(lines) + "\n")


def build_mini_proteome(source_fasta: str | None, n_proteins: int, output_path: str, rng: random.Random | None = None) -> None:
    rng = rng or random.Random(42)
    records = []
    for i in range(n_proteins):
        length = rng.randint(50, 150)
        seq = "".join(rng.choice(AMINO_ACIDS) for _ in range(length))
        records.append((f"protein_{i}", seq))
    io_utils.write_fasta(output_path, records)


def build_golden_consequence_vcf(output_path: str) -> None:
    lines = [
        "##fileformat=VCFv4.2",
        "#CHROM\tPOS\tID\tREF\tALT\tQUAL\tFILTER\tINFO",
        "chr21\t100\t.\tC\tT\t99\tPASS\tCSQ=synonymous_variant",
        "chr21\t200\t.\tA\tG\t99\tPASS\tCSQ=missense_variant",
        "chr21\t300\t.\tC\tA\t99\tPASS\tCSQ=stop_gained",
        "chr21\t400\t.\tA\tAT\t99\tPASS\tCSQ=frameshift_variant",
        "chr21\t500\t.\tG\tC\t99\tPASS\tCSQ=intergenic_variant",
    ]
    with open(output_path, "w") as fh:
        fh.write("\n".join(lines) + "\n")


def build_synthetic_candidates(n: int, alleles: list[str], output_path: str, rng: random.Random | None = None) -> None:
    rng = rng or random.Random(42)
    rows = []
    for i in range(n):
        length = rng.randint(8, 11)
        mutant = "".join(rng.choice(AMINO_ACIDS) for _ in range(length))
        wildtype = mutant[:-1] + rng.choice([a for a in AMINO_ACIDS if a != mutant[-1]])
        rows.append(
            {
                "MutantPeptide": mutant, "WildTypePeptide": wildtype, "HlaAllele": alleles[i % len(alleles)],
                "PeptideLength": length, "GeneSymbol": f"GENE{i % 10}", "TranscriptId": f"ENST{i:08d}",
                "SourceVariantId": f"chr21:{1000+i}:A>G", "ProteinPosition": rng.randint(1, 300),
                "MutationOffsetInPeptide": length - 1, "Vaf": round(rng.uniform(0.05, 0.7), 3),
            }
        )
    io_utils.write_tsv(
        output_path, rows,
        ["MutantPeptide", "WildTypePeptide", "HlaAllele", "PeptideLength", "GeneSymbol", "TranscriptId",
         "SourceVariantId", "ProteinPosition", "MutationOffsetInPeptide", "Vaf"],
    )


if __name__ == "__main__":
    main()
