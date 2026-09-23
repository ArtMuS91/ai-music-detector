import type { AnalysisStatus } from './AnalysisStatus';
import type { Track } from './Track';

export type AnalysisJob = {
  id: string;
  sourceUrl: string;
  status: AnalysisStatus;
  createdAt: string;
  updatedAt: string;
  track: Track | null;
  failureReason: string | null;
};
