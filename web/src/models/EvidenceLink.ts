export type EvidenceStance = 'Neutral' | 'Human' | 'AiGenerated';

export type EvidenceLink = {
  url: string;
  title: string | null;
  stance: EvidenceStance;
};
