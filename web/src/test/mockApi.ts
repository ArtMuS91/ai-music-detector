import { vi } from 'vitest';
import type { AnalysisJob, AnalysisResult, AudioVisualization, Signal } from '../models';

type Route = (init?: RequestInit) => Response | Promise<Response>;

/** Replaces global fetch with a router keyed by "METHOD /path"; unknown requests fail the test loudly. */
export function mockApi(routes: Record<string, Route>) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = typeof input === 'string' ? input : input.toString();
    const key = `${init?.method ?? 'GET'} ${new URL(url).pathname}`;
    const route = routes[key];

    if (!route) {
      throw new Error(`Unexpected request: ${key}`);
    }

    return route(init);
  });

  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

export function json(body: unknown, status = 200, headers: Record<string, string> = {}) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json', ...headers },
  });
}

/** The API's answer to a poll whose If-None-Match still matches the job. */
export function notModified() {
  return new Response(null, { status: 304 });
}

export function networkError(): never {
  throw new TypeError('Failed to fetch');
}

export function job(overrides: Partial<AnalysisJob> = {}): AnalysisJob {
  return {
    id: 'job-1',
    sourceUrl: 'https://www.youtube.com/watch?v=jNQXAC9IVRw',
    status: 'Pending',
    createdAt: '2026-09-23T10:00:00Z',
    updatedAt: '2026-09-23T10:00:00Z',
    track: null,
    result: null,
    visualization: null,
    failureReason: null,
    failedStage: null,
    ...overrides,
  };
}

export function signal(overrides: Partial<Signal> = {}): Signal {
  return {
    name: 'Generator fingerprint',
    score: 0.97,
    weight: 0.8,
    detail: 'Model probability 0.97.',
    evidence: [],
    ...overrides,
  };
}

export function result(overrides: Partial<AnalysisResult> = {}): AnalysisResult {
  return {
    verdict: 'AiGenerated',
    aiProbability: 0.91,
    confidence: 0.74,
    signals: [signal()],
    explanation: 'The audio carries a generator fingerprint.',
    ...overrides,
  };
}

export function visualization(overrides: Partial<AudioVisualization> = {}): AudioVisualization {
  return {
    startSeconds: 90,
    durationSeconds: 180,
    waveform: [0.2, 1, 0.5],
    spectrogram: {
      frames: 2,
      bands: 2,
      minFrequency: 30,
      maxFrequency: 22050,
      minDecibels: -80,
      values: 'AAH//g==',
    },
    ...overrides,
  };
}
