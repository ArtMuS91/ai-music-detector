import { describe, expect, it } from 'vitest';
import { getAnalysis, InvalidUrlError, JobNotFoundError, submitAnalysis } from './api';
import { job, json, mockApi, networkError } from './test/mockApi';

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

    const rejection = expect(submitAnalysis('not a link')).rejects;
    await rejection.toThrow('Not a YouTube or YouTube Music video link.');
    await rejection.toBeInstanceOf(InvalidUrlError);
  });

  it('falls back to a generic message when a 400 has no field errors', async () => {
    mockApi({ 'POST /api/analyze': () => json({ title: 'Bad Request' }, 400) });

    await expect(submitAnalysis('x')).rejects.toThrow('That link could not be accepted.');
  });

  it('returns an already analyzed job, completed, from a 200', async () => {
    mockApi({ 'POST /api/analyze': () => json(job({ status: 'Completed' }), 200) });

    expect((await submitAnalysis('https://youtu.be/jNQXAC9IVRw')).status).toBe('Completed');
  });

  it('explains an unreachable API instead of surfacing a raw fetch error', async () => {
    mockApi({ 'POST /api/analyze': networkError });

    await expect(submitAnalysis('x')).rejects.toThrow('Could not reach the API. Check that it is running and try again.');
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

  it('throws a not-found error for an unknown job', async () => {
    mockApi({ 'GET /api/analyze/missing': () => json({}, 404) });

    await expect(getAnalysis('missing')).rejects.toBeInstanceOf(JobNotFoundError);
  });

  it('reports the status code for other failures', async () => {
    mockApi({ 'GET /api/analyze/job-1': () => json({}, 500) });

    await expect(getAnalysis('job-1')).rejects.toThrow('The API responded with 500.');
  });
});
