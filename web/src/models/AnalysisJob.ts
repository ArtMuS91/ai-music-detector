import type { AnalysisResult } from './AnalysisResult';
import type { AnalysisStatus } from './AnalysisStatus';
import type { AudioVisualization } from './AudioVisualization';
import type { Track } from './Track';

export type AnalysisJob = {
  id: string;
  sourceUrl: string;
  status: AnalysisStatus;
  createdAt: string;
  updatedAt: string;
  track: Track | null;
  result: AnalysisResult | null;
  visualization: AudioVisualization | null;
  failureReason: string | null;
  /** The stage a Failed job was in when it failed; null if unknown. */
  failedStage: AnalysisStatus | null;
};
