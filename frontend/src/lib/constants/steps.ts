import type { StepDefinition, StepId } from "@/types/step";

export const STEP_IDS: StepId[] = [
  "01_upload",
  "02_alignment",
  "03_variants",
  "04_protein_effects",
  "05_hla_typing",
  "06_candidates",
  "07_presentation",
  "08_immunogenicity",
  "09_filtering",
  "10_ranking",
  "11_vaccine_design",
];

export const STEP_ORDER: Record<StepId, number> = {
  "01_upload": 1,
  "02_alignment": 2,
  "03_variants": 3,
  "04_protein_effects": 4,
  "05_hla_typing": 5,
  "06_candidates": 6,
  "07_presentation": 7,
  "08_immunogenicity": 8,
  "09_filtering": 9,
  "10_ranking": 10,
  "11_vaccine_design": 11,
};

export const STEP_DISPLAY_NAMES: Record<StepId, string> = {
  "01_upload": "Upload Sequencing Data",
  "02_alignment": "Align to Reference Genome",
  "03_variants": "Call Somatic Mutations",
  "04_protein_effects": "Determine Protein Consequences",
  "05_hla_typing": "HLA Typing",
  "06_candidates": "Generate Candidate Peptides",
  "07_presentation": "Predict HLA Presentation",
  "08_immunogenicity": "Predict Immunogenicity",
  "09_filtering": "Safety and Expression Filtering",
  "10_ranking": "Weighted Final Ranking",
  "11_vaccine_design": "Design Vaccine Sequence",
};

export const STEP_ICONS: Record<StepId, string> = {
  "01_upload": "upload",
  "02_alignment": "align-center",
  "03_variants": "git-compare",
  "04_protein_effects": "dna",
  "05_hla_typing": "id-card",
  "06_candidates": "list-tree",
  "07_presentation": "target",
  "08_immunogenicity": "shield-alert",
  "09_filtering": "filter",
  "10_ranking": "sliders-horizontal",
  "11_vaccine_design": "syringe",
};

export function getStepIndex(stepId: StepId): number {
  return STEP_IDS.indexOf(stepId);
}

export function getPreviousStepId(stepId: StepId): StepId | null {
  const idx = getStepIndex(stepId);
  return idx > 0 ? STEP_IDS[idx - 1] : null;
}

export function getNextStepId(stepId: StepId): StepId | null {
  const idx = getStepIndex(stepId);
  return idx >= 0 && idx < STEP_IDS.length - 1 ? STEP_IDS[idx + 1] : null;
}

export function isUploadStep(stepId: StepId): boolean {
  return stepId === "01_upload";
}

/**
 * Local, offline copy of the step definitions — the backend is the source of
 * truth (`GET /api/patients/{pid}/steps`) and useStepStore.fetchDefinitions()
 * always tries that first. This exists so panels render real explanatory
 * content (per docs/PROJECT_PLAN.md §6, "Explanation for the UI") even when
 * the API is unreachable, which is the normal case during local frontend
 * development against a not-yet-running backend. Additive to the spec's
 * §10 export list, not a replacement for any of it.
 */
export const STEP_DEFINITIONS: StepDefinition[] = [
  {
    id: "01_upload",
    order: 1,
    displayName: STEP_DISPLAY_NAMES["01_upload"],
    shortDescription: "Upload tumor DNA, normal DNA, and optional tumor RNA.",
    longExplanation:
      "Every analysis starts with two DNA samples from the same person: one from their tumor, one from healthy tissue. Comparing them is what reveals which mutations belong to the cancer specifically, rather than being part of the person's normal inherited genetics. Optionally, you can also upload RNA sequencing data, which shows which genes the tumor is actually using — this improves target selection later but isn't required.",
    toolName: "None (file intake only)",
    requiredInputStepIds: [],
    isUploadStep: true,
    hasParameters: false,
    producesDownload: false,
    requiredTools: [],
  },
  {
    id: "02_alignment",
    order: 2,
    displayName: STEP_DISPLAY_NAMES["02_alignment"],
    shortDescription: "Map sequencing reads onto the reference genome.",
    longExplanation:
      "Sequencing machines don't read DNA in order — they shatter it into millions of short fragments and read those. Alignment figures out where each fragment belongs on the human genome, like matching puzzle pieces to the picture on the box. If you uploaded BAM files, this step is already done and can be skipped.",
    toolName: "bwa-mem2 (DNA) / STAR (RNA)",
    requiredInputStepIds: ["01_upload"],
    isUploadStep: false,
    hasParameters: false,
    producesDownload: false,
    requiredTools: ["bwa-mem2", "STAR", "samtools"],
  },
  {
    id: "03_variants",
    order: 3,
    displayName: STEP_DISPLAY_NAMES["03_variants"],
    shortDescription: "Compare tumor and normal DNA to find cancer-specific mutations.",
    longExplanation:
      "This compares the tumor DNA against the healthy DNA from the same person and flags every position where they differ. Those differences are mutations that arose in the cancer specifically. The comparison against the person's own healthy tissue is essential — without it, you'd be flagging thousands of harmless inherited variations that every human has.",
    toolName: "Mutect2 (GATK)",
    requiredInputStepIds: ["02_alignment"],
    isUploadStep: false,
    hasParameters: false,
    producesDownload: false,
    requiredTools: ["gatk"],
  },
  {
    id: "04_protein_effects",
    order: 4,
    displayName: STEP_DISPLAY_NAMES["04_protein_effects"],
    shortDescription: "Translate mutations into protein-level effects.",
    longExplanation:
      "Not every DNA mutation matters. Only about 1–2% of the genome codes for proteins at all, and even within that, some mutations happen to produce the same amino acid as before — changing the DNA without changing the protein. This step translates each mutation into its protein-level effect and keeps only the ones that genuinely alter a protein, since those are the only ones the immune system could possibly notice.",
    toolName: "VEP (Variant Effect Predictor)",
    requiredInputStepIds: ["03_variants"],
    isUploadStep: false,
    hasParameters: false,
    producesDownload: false,
    requiredTools: ["vep"],
  },
  {
    id: "05_hla_typing",
    order: 5,
    displayName: STEP_DISPLAY_NAMES["05_hla_typing"],
    shortDescription: "Determine the patient's HLA class I alleles.",
    longExplanation:
      "HLA molecules are the display cases cells use to show the immune system samples of what they're building inside. Everyone inherits a specific set of HLA variants, and different variants physically hold different protein fragments — a target that works for one person may be invisible in another. This step reads the healthy DNA (HLA type is inherited, not caused by the cancer) to determine this patient's specific HLA variants.",
    toolName: "OptiType",
    requiredInputStepIds: ["01_upload"],
    isUploadStep: false,
    hasParameters: false,
    producesDownload: false,
    requiredTools: ["OptiType"],
  },
  {
    id: "06_candidates",
    order: 6,
    displayName: STEP_DISPLAY_NAMES["06_candidates"],
    shortDescription: "Slide a window across each mutation to generate candidate peptides.",
    longExplanation:
      "HLA display cases only hold short fragments — around 8 to 11 amino acids. Since we can't know exactly where the cell's internal machinery will cut a protein, this step generates every plausible short fragment containing each mutation, sliding a window across the mutated position. It also generates the matching unmutated version of each fragment, which is needed later to check how different the mutant version really looks to the immune system.",
    toolName: "pVACtools",
    requiredInputStepIds: ["04_protein_effects", "05_hla_typing"],
    isUploadStep: false,
    hasParameters: false,
    producesDownload: false,
    requiredTools: ["pvacseq"],
  },
  {
    id: "07_presentation",
    order: 7,
    displayName: STEP_DISPLAY_NAMES["07_presentation"],
    shortDescription: "Predict which candidates will be displayed on this patient's HLA.",
    longExplanation:
      "This predicts which candidate fragments will actually be displayed on this patient's HLA molecules. A fragment that can't physically fit the display case will never be seen by the immune system, no matter how foreign it looks. Roughly half to three-quarters of the top-ranked predictions here turn out to be genuinely displayed.",
    toolName: "MHCflurry 2.0 (optional: BigMHC-EL)",
    requiredInputStepIds: ["06_candidates"],
    isUploadStep: false,
    hasParameters: true,
    producesDownload: false,
    requiredTools: ["mhcflurry"],
  },
  {
    id: "08_immunogenicity",
    order: 8,
    displayName: STEP_DISPLAY_NAMES["08_immunogenicity"],
    shortDescription: "Predict which displayed peptides will actually provoke a T-cell response.",
    longExplanation:
      "Being displayed isn't the same as being noticed. Most displayed fragments never provoke an immune response. This step predicts which ones will actually attract T cells — and it's the least reliable part of the whole pipeline. Current tools score only modestly better than chance, and this is an open research problem across the entire field, not a limitation of this app specifically.",
    toolName: "BigMHC-IM (alternatives: PRIME, PepFore)",
    requiredInputStepIds: ["07_presentation"],
    isUploadStep: false,
    hasParameters: true,
    producesDownload: false,
    requiredTools: [],
  },
  {
    id: "09_filtering",
    order: 9,
    displayName: STEP_DISPLAY_NAMES["09_filtering"],
    shortDescription: "Remove self-similar and (if RNA available) unexpressed candidates.",
    longExplanation:
      "Two filters here. First, safety: if a candidate fragment closely resembles a normal human protein, targeting it risks the immune system attacking healthy tissue — those are removed. Second, if RNA data was provided: mutations in genes the tumor isn't actually using are removed, since a gene that's switched off produces no protein and therefore no target.",
    toolName: "Reference proteome comparison + RNA-seq quantification",
    requiredInputStepIds: ["08_immunogenicity"],
    isUploadStep: false,
    hasParameters: true,
    producesDownload: false,
    requiredTools: [],
  },
  {
    id: "10_ranking",
    order: 10,
    displayName: STEP_DISPLAY_NAMES["10_ranking"],
    shortDescription: "Combine weighted signals into a final ranked, selected list.",
    longExplanation:
      "The final ranking combines several signals, and you can control how much each one matters. Binding strength difference (agretopicity) measures how much more strongly the mutated fragment binds compared to its normal counterpart — a bigger gap means it looks more foreign. Expression is how actively the gene is used. Clonality is what fraction of tumor cells carry the mutation — targeting a mutation present in every cell is safer than one present in only some. HLA spread means deliberately choosing targets across different HLA types, so the tumor can't escape by losing just one.",
    toolName: "Custom scoring logic (C#)",
    requiredInputStepIds: ["09_filtering"],
    isUploadStep: false,
    hasParameters: true,
    producesDownload: false,
    requiredTools: [],
  },
  {
    id: "11_vaccine_design",
    order: 11,
    displayName: STEP_DISPLAY_NAMES["11_vaccine_design"],
    shortDescription: "Assemble selected targets into a single mRNA construct.",
    longExplanation:
      "This assembles the final selected targets into a single mRNA sequence — the actual blueprint a lab would synthesize. The chosen fragments are strung together with short connector sequences between them, wrapped in standard start and end elements that help cells read the instructions properly. The output is a sequence file, not a physical vaccine; manufacturing requires specialized facilities and regulatory approval.",
    toolName: "pVACvector",
    requiredInputStepIds: ["10_ranking"],
    isUploadStep: false,
    hasParameters: true,
    producesDownload: true,
    requiredTools: ["pvacvector"],
  },
];

export function getStepDefinition(stepId: StepId): StepDefinition | undefined {
  return STEP_DEFINITIONS.find((d) => d.id === stepId);
}
