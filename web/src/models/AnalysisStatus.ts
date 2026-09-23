export type AnalysisStatus =
  | 'Pending'
  | 'Acquiring'
  | 'Preprocessing'
  | 'Analyzing'
  | 'Completed'
  | 'Failed';

export const TERMINAL_STATUSES: AnalysisStatus[] = ['Completed', 'Failed'];
