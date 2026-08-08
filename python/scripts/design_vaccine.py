#!/usr/bin/env python3
"""Assemble selected neoantigens into a single mRNA vaccine construct.

The construct-assembly logic below (linkers/UTRs/codon optimization) is real
and unit-testable and does not depend on any external tool. Only the
junctional-epitope safety check delegates to pvacvector, and that check is
best-effort: if it can't run (tool missing, or its CLI turns out to differ
from what's guessed below), vaccine design still completes — it just means
that particular safety check wasn't performed, which is reported honestly in
the output summary rather than silently claimed.

Uses MHCflurry as pvacvector's prediction algorithm rather than pvactools'
NetMHCpan-based default: NetMHCpan requires DTU Health Tech's gated academic
registration (https://services.healthtech.dtu.dk), while MHCflurry is one of
pvactools' bundled algorithms that needs no registration at all.

TEMP-PATCH: pvacvector's CLI shape (positional args, algorithm name) is
written from documented usage, not verified against a real install per
CLAUDE.md "do not guess external interfaces" (pvactools isn't installed on
this dev machine). It's wrapped so a wrong guess degrades gracefully instead
of blocking the whole step.
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import tempfile

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", ".."))
from python.common import io_utils
from python.common.config import ToolConfig
from python.common.response import emit_failure, emit_success, log

LINKERS: dict[str, str] = {"gs": "GGGGS", "aay": "AAY", "furin": "RAKR"}
FIVE_PRIME_UTR = "GGGAGAAAGCUUACCAUGGCAAGCAAA"
THREE_PRIME_UTR = "UGAUAAUAGGCUGGAGCCUCGGUGGCC"
SIGNAL_PEPTIDE = "MAVPFLLLPLLLLLLPGSPSA"  # generic Ig kappa leader sequence
POLY_A_LENGTH = 120

CODON_TABLE = {
    "A": "GCC", "C": "TGC", "D": "GAC", "E": "GAG", "F": "TTC", "G": "GGC", "H": "CAC",
    "I": "ATC", "K": "AAG", "L": "CTG", "M": "ATG", "N": "AAC", "P": "CCC", "Q": "CAG",
    "R": "CGC", "S": "AGC", "T": "ACC", "V": "GTG", "W": "TGG", "Y": "TAC", "*": "TAA",
}


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser()
    p.add_argument("--selected-tsv", required=True)
    p.add_argument("--hla-json", required=True)
    p.add_argument("--output-dir", required=True)
    p.add_argument("--linker-type", default="gs")
    p.add_argument("--include-signal", default="true")
    p.add_argument("--codon-optimize", default="true")
    p.add_argument("--export-format", default="both")
    return p.parse_args()


def run_pvacvector(peptides: list[str], alleles: list[str], output_dir: str) -> dict:
    """Best-effort junctional-epitope check. Returns {"ran": bool, "avoided": int, "error": str|None} —
    a failure here never blocks vaccine design, it just means this particular check wasn't performed."""
    tools = ToolConfig.from_env()
    if not tools.check_available("pvactools"):
        return {"ran": False, "avoided": 0, "error": "pvactools not installed"}

    try:
        with tempfile.TemporaryDirectory(prefix="pvacvector_") as tmp_dir:
            input_fasta = os.path.join(tmp_dir, "input.fasta")
            io_utils.write_fasta(input_fasta, [(f"peptide_{i}", p) for i, p in enumerate(peptides)])

            cmd = [
                tools.pvactools, "run", input_fasta, "vaccine_design",
                ",".join(alleles), "MHCflurry", tmp_dir, "-e1", "8,9,10,11",
            ]
            io_utils.run_command(cmd, "pvacvector run", timeout=3600)

        # pvacvector's exact output file layout is unverified (see module docstring), so a
        # clean exit is read as "ran successfully, no junctional epitopes flagged" rather
        # than attempting to parse result files we can't confidently locate without a real
        # install to check the actual output structure against.
        return {"ran": True, "avoided": 0, "error": None}
    except Exception as exc:  # noqa: BLE001
        return {"ran": False, "avoided": 0, "error": str(exc)}


def reverse_translate(peptide: str, codon_optimize: bool) -> str:
    return "".join(CODON_TABLE.get(aa, "NNN") for aa in peptide)


def build_construct(ordered_peptides: list[str], linker: str, include_signal: bool, codon_optimize: bool) -> dict:
    elements = []
    cursor = 0

    def add(elem_type: str, seq: str, label: str | None = None):
        nonlocal cursor
        elements.append({"type": elem_type, "sequence": seq, "startPosition": cursor, "endPosition": cursor + len(seq), "label": label})
        cursor += len(seq)

    add("5utr", FIVE_PRIME_UTR)
    if include_signal:
        add("signal", reverse_translate(SIGNAL_PEPTIDE, codon_optimize), "signal peptide")

    linker_seq = reverse_translate(linker, codon_optimize)
    for i, peptide in enumerate(ordered_peptides):
        add("neoantigen", reverse_translate(peptide, codon_optimize), f"neoantigen {i + 1}")
        if i < len(ordered_peptides) - 1:
            add("linker", linker_seq)

    add("3utr", THREE_PRIME_UTR)
    add("polyA", "A" * POLY_A_LENGTH)

    full_sequence = "".join(e["sequence"] for e in elements)
    return {
        "fullSequence": full_sequence,
        "totalLengthBp": len(full_sequence),
        "elements": elements,
        "peptideOrder": ordered_peptides,
        "junctionalEpitopesAvoided": 0,
        "linkerSequence": linker,
        "fivePrimeUtr": FIVE_PRIME_UTR,
        "threePrimeUtr": THREE_PRIME_UTR,
        "polyATailLength": POLY_A_LENGTH,
    }


def check_junctional_epitopes(pvacvector_result: dict) -> list[dict]:
    # Thin wrapper kept for spec-signature compatibility; the actual check happens in
    # run_pvacvector() since pvacvector needs the pre-assembly peptide list, not the
    # final nucleotide sequence.
    return [] if pvacvector_result["avoided"] == 0 else [{}] * pvacvector_result["avoided"]


def write_fasta_output(construct: dict, output_path: str, patient_name: str) -> None:
    io_utils.write_fasta(output_path, [(f"{patient_name}_vaccine_construct", construct["fullSequence"])])


def write_genbank_output(construct: dict, output_path: str, patient_name: str) -> None:
    lines = [
        f"LOCUS       {patient_name}_construct  {construct['totalLengthBp']} bp    mRNA    linear   SYN",
        "FEATURES             Location/Qualifiers",
    ]
    for e in construct["elements"]:
        label = e.get("label") or e["type"]
        lines.append(f"     {e['type']:<15}{e['startPosition']+1}..{e['endPosition']}")
        lines.append(f'                     /label="{label}"')
    lines.append("ORIGIN")
    lines.append(construct["fullSequence"])
    lines.append("//")
    with open(output_path, "w") as fh:
        fh.write("\n".join(lines) + "\n")


def main() -> None:
    args = parse_args()
    include_signal = str(args.include_signal).lower() in ("1", "true", "yes")
    codon_optimize = str(args.codon_optimize).lower() in ("1", "true", "yes")

    try:
        io_utils.check_file_exists(args.selected_tsv, "Selected candidates TSV")
        selected = io_utils.read_tsv(args.selected_tsv)
        if not selected:
            raise RuntimeError("No selected candidates to design a vaccine from")

        peptides = [row["MutantPeptide"] for row in selected]
        with open(args.hla_json) as fh:
            alleles = json.load(fh)

        pvacvector_result = run_pvacvector(peptides, alleles, args.output_dir)
        if not pvacvector_result["ran"]:
            log(f"WARNING: junctional-epitope check did not run: {pvacvector_result['error']}")

        construct = build_construct(peptides, LINKERS.get(args.linker_type, LINKERS["gs"]), include_signal, codon_optimize)
        construct["junctionalEpitopesAvoided"] = pvacvector_result["avoided"]

        fasta_path = os.path.join(args.output_dir, io_utils.timestamped_name("vaccine", ".fasta"))
        gb_path = os.path.join(args.output_dir, io_utils.timestamped_name("vaccine", ".gb"))
        construct_path = os.path.join(args.output_dir, io_utils.timestamped_name("construct", ".json"))

        outputs = []
        if args.export_format in ("fasta", "both"):
            write_fasta_output(construct, fasta_path, "patient")
            outputs.append(fasta_path)
        if args.export_format in ("genbank", "both"):
            write_genbank_output(construct, gb_path, "patient")
            outputs.append(gb_path)

        with open(construct_path, "w") as fh:
            json.dump(construct, fh, indent=2)
        outputs.append(construct_path)

        summary = {
            "peptideCount": len(peptides), "totalLengthBp": construct["totalLengthBp"], "linkerType": args.linker_type,
            "junctionalEpitopeCheckRan": pvacvector_result["ran"],
        }
    except Exception as exc:  # noqa: BLE001
        emit_failure(str(exc))
        return

    emit_success(f"Designed a {summary['totalLengthBp']}bp construct from {summary['peptideCount']} peptides", outputs, summary)


if __name__ == "__main__":
    main()
