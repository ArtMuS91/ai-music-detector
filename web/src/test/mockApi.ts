import { vi } from 'vitest';
import type { AnalysisJob } from '../models';

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

export function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
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
    failureReason: null,
    ...overrides,
  };
}
