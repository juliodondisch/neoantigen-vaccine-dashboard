export interface NeoantigenCandidate {
  candidateId: string;
  mutantPeptide: string;
  wildTypePeptide: string;
  hlaAllele: string;
  peptideLength: number;

  geneSymbol: string;
  transcriptId: string;
  sourceVariantId: string;
  chromosome: string;
  position: number;
  consequence: string;
  mutationOffsetInPeptide: number;

  presentationScore?: number;
  presentationPercentileRank?: number;
  wildTypePresentationScore?: number;
  presentationPredictor?: string;

  immunogenicityScore?: number;
  immunogenicityPredictor?: string;

  passedSelfFilter: boolean;
  passedExpressionFilter: boolean;
  removalReason?: string;
  selfSimilarityScore?: number;
  expressionTpm?: number;

  vaf: number;

  agretopicity?: number;
  finalScore?: number;
  finalRank?: number;
  isSelected: boolean;
}

export interface RankingWeights {
  presentation: number;
  immunogenicity: number;
  agretopicity: number;
  expression: number;
  clonality: number;
  hlaSpread: number;
}

export interface HlaProfile {
  classIAlleles: string[];
  classIIAlleles: string[];
  confidence: Record<string, number>;
  typedAt: string;
  source: string;
}

export interface VaccineConstruct {
  fullSequence: string;
  totalLengthBp: number;
  elements: ConstructElement[];
  peptideOrder: string[];
  junctionalEpitopesAvoided: number;
  linkerSequence: string;
  fivePrimeUtr: string;
  threePrimeUtr: string;
  polyATailLength: number;
  designedAt: string;
}

export interface ConstructElement {
  type: "5utr" | "signal" | "neoantigen" | "linker" | "3utr" | "polyA";
  sequence: string;
  startPosition: number;
  endPosition: number;
  label?: string;
}
