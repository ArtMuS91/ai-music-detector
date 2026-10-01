import type { AnalysisVerdict } from './AnalysisVerdict';
import type { Signal } from './Signal';

export type AnalysisResult = {
  verdict: AnalysisVerdict;
  /** Aggregated 0..1 likelihood the track is AI-generated. */
  aiProbability: number;
  /** 0..1 certainty in the verdict, independent of which way it leans. */
  confidence: number;
  signals: Signal[];
  explanation: string | null;
};
