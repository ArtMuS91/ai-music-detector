import type { AnalysisJob } from './models';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5214';

/** The API rejected the submitted link; the message is meant for the user. */
export class InvalidUrlError extends Error {}

/** The job does not exist (any more), so polling it again cannot help. */
export class JobNotFoundError extends Error {}

export async function checkHealth(): Promise<boolean> {
  try {
    const response = await fetch(`${API_BASE_URL}/health`);
    return response.ok;
  } catch {
    return false;
  }
}

export async function submitAnalysis(url: string): Promise<AnalysisJob> {
  const response = await fetch(`${API_BASE_URL}/api/analyze`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ url }),
  });

  if (response.status === 400) {
    const problem = await response.json();
    throw new InvalidUrlError(problem.errors?.Url?.[0] ?? 'That link could not be accepted.');
  }

  if (!response.ok) {
    throw new Error(`The API responded with ${response.status}.`);
  }

  return response.json();
}

export async function getAnalysis(id: string): Promise<AnalysisJob> {
  const response = await fetch(`${API_BASE_URL}/api/analyze/${id}`);

  if (response.status === 404) {
    throw new JobNotFoundError('This analysis no longer exists. Submit the link again.');
  }

  if (!response.ok) {
    throw new Error(`The API responded with ${response.status}.`);
  }

  return response.json();
}
