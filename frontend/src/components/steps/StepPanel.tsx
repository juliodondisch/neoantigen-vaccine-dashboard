import type { StepDefinition, StepId, StepState } from "@/types/step";
import { UploadPanel } from "./panels/UploadPanel";
import { AlignmentPanel } from "./panels/AlignmentPanel";
import { VariantPanel } from "./panels/VariantPanel";
import { ProteinEffectsPanel } from "./panels/ProteinEffectsPanel";
import { HlaTypingPanel } from "./panels/HlaTypingPanel";
import { CandidatePanel } from "./panels/CandidatePanel";
import { PresentationPanel } from "./panels/PresentationPanel";
import { ImmunogenicityPanel } from "./panels/ImmunogenicityPanel";
import { FilteringPanel } from "./panels/FilteringPanel";
import { RankingPanel } from "./panels/RankingPanel";
import { VaccineDesignPanel } from "./panels/VaccineDesignPanel";

interface StepPanelProps {
  patientId: string;
  stepId: StepId;
  definition: StepDefinition;
  state?: StepState;
}

interface PanelComponentProps {
  patientId: string;
  definition: StepDefinition;
  state?: StepState;
}

const PANELS: Record<StepId, (props: PanelComponentProps) => ReturnType<typeof UploadPanel>> = {
  "01_upload": UploadPanel,
  "02_alignment": AlignmentPanel,
  "03_variants": VariantPanel,
  "04_protein_effects": ProteinEffectsPanel,
  "05_hla_typing": HlaTypingPanel,
  "06_candidates": CandidatePanel,
  "07_presentation": PresentationPanel,
  "08_immunogenicity": ImmunogenicityPanel,
  "09_filtering": FilteringPanel,
  "10_ranking": RankingPanel,
  "11_vaccine_design": VaccineDesignPanel,
};

export function StepPanel({ patientId, stepId, definition, state }: StepPanelProps) {
  const Panel = PANELS[stepId];
  return <Panel patientId={patientId} definition={definition} state={state} />;
}
