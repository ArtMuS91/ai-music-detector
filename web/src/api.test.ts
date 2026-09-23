import { describe, expect, it } from 'vitest';
import { checkHealth, getAnalysis, submitAnalysis } from './api';
import { job, json, mockApi, networkError } from './test/mockApi';

describe('checkHealth', () => {
  it('is true when the API answers 200', async () => {
    mockApi({ 'GET /health': () => json({ status: 'ok' }) });

    expect(await checkHealth()).toBe(true);
  });

  it('is false when the API answers with an error status', async () => {
    mockApi({ 'GET /health': () => json({}, 503) });

    expect(await checkHealth()).toBe(false);
  });

  it('is false instead of throwing when the API is unreachable', async () => {
    mockApi({ 'GET /health': networkError });

    expect(await checkHealth()).toBe(false);
  });
});

describe('submitAnalysis', () => {
  it('posts the url as JSON and returns the created job', async () => {
    const fetchMock = mockApi({ 'POST /api/analyze': () => json(job(), 202) });

    const created = await submitAnalysis('https://music.youtube.com/watch?v=jNQXAC9IVRw');

    expect(created.id).toBe('job-1');
    const [, init] = fetchMock.mock.calls[0];
    expect(JSON.parse(init?.body as string)).toEqual({
      url: 'https://music.youtube.com/watch?v=jNQXAC9IVRw',
    });
  });

  it('surfaces the server validation message for a rejected url', async () => {
    mockApi({
      'POST /api/analyze': () =>
        json({ errors: { Url: ['Not a YouTube or YouTube Music video link.'] } }, 400),
    });

    await expect(submitAnalysis('not a link')).rejects.toThrow(
      'Not a YouTube or YouTube Music video link.',
    );
  });

  it('falls back to a generic message when a 400 has no field errors', async () => {
    mockApi({ 'POST /api/analyze': () => json({ title: 'Bad Request' }, 400) });

    await expect(submitAnalysis('x')).rejects.toThrow('That link could not be accepted.');
  });

  it('reports the status code for other failures', async () => {
    mockApi({ 'POST /api/analyze': () => json({}, 500) });

    await expect(submitAnalysis('x')).rejects.toThrow('The API responded with 500.');
  });
});

describe('getAnalysis', () => {
  it('returns the job for its id', async () => {
    mockApi({ 'GET /api/analyze/job-1': () => json(job({ status: 'Acquiring' })) });

    expect((await getAnalysis('job-1')).status).toBe('Acquiring');
  });

  it('throws on an unknown job', async () => {
    mockApi({ 'GET /api/analyze/missing': () => json({}, 404) });

    await expect(getAnalysis('missing')).rejects.toThrow('The API responded with 404.');
  });
});
