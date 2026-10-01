import type { EvidenceLink } from './EvidenceLink';

export type Signal = {
  name: string;
  /** 0 = strongly human, 1 = strongly AI-generated. */
  score: number;
  /** Relative influence on the verdict; 0 means the signal found nothing to go on. */
  weight: number;
  detail: string | null;
  evidence: EvidenceLink[];
};
