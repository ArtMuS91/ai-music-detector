import type { AnalysisJob } from './AnalysisJob';

/** A job as polled, with the version to send back on the next poll so an unchanged job is not resent. */
export type AnalysisSnapshot = {
  job: AnalysisJob;
  /** The response's ETag; null if the API sent none. */
  etag: string | null;
};
