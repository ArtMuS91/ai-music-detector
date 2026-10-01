import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import App from './App';
import { job, json, mockApi, networkError, result, visualization } from './test/mockApi';

const VIDEO_URL = 'https://music.youtube.com/watch?v=jNQXAC9IVRw';
const POLL_INTERVAL_MS = 1500;

const online = { 'GET /health': () => json({ status: 'ok' }) };

async function renderOnline() {
  render(<App />);
  expect(await screen.findByText('API: online')).toBeInTheDocument();
}

function urlInput() {
  return screen.getByRole('textbox', { name: 'YouTube URL' });
}

function analyzeButton() {
  return screen.getByRole('button', { name: 'Analyze' });
}

describe('App', () => {
  it('suggests a YouTube Music link as the placeholder', () => {
    mockApi(online);
    render(<App />);

    expect(urlInput()).toHaveAttribute('placeholder', 'https://music.youtube.com/watch?v=');
  });

  it('keeps Analyze disabled while the API is offline, even with a url entered', async () => {
    mockApi({ 'GET /health': networkError });
    const user = userEvent.setup();
    render(<App />);

    expect(await screen.findByText('API: offline')).toBeInTheDocument();
    await user.type(urlInput(), VIDEO_URL);

    expect(analyzeButton()).toBeDisabled();
  });

  it('does not submit on Enter while the API is offline', async () => {
    const fetchMock = mockApi({ 'GET /health': networkError });
    const user = userEvent.setup();
    render(<App />);
    await screen.findByText('API: offline');

    await user.type(urlInput(), `${VIDEO_URL}{Enter}`);

    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('enables Analyze only once the API is online and a url is entered', async () => {
    mockApi(online);
    const user = userEvent.setup();
    await renderOnline();

    expect(analyzeButton()).toBeDisabled();

    await user.type(urlInput(), VIDEO_URL);

    expect(analyzeButton()).toBeEnabled();
  });

  it('shows the server validation message for a rejected url', async () => {
    mockApi({
      ...online,
      'POST /api/analyze': () =>
        json({ errors: { Url: ['Not a YouTube or YouTube Music video link.'] } }, 400),
    });
    const user = userEvent.setup();
    await renderOnline();

    await user.type(urlInput(), 'https://vimeo.com/123');
    await user.click(analyzeButton());

    expect(await screen.findByText('Not a YouTube or YouTube Music video link.')).toBeInTheDocument();
    expect(urlInput()).toHaveAccessibleDescription('Not a YouTube or YouTube Music video link.');
    expect(urlInput()).toHaveAttribute('aria-invalid', 'true');
  });

  it('clears the url error once the url is edited', async () => {
    mockApi({
      ...online,
      'POST /api/analyze': () =>
        json({ errors: { Url: ['Not a YouTube or YouTube Music video link.'] } }, 400),
    });
    const user = userEvent.setup();
    await renderOnline();
    await user.type(urlInput(), 'https://vimeo.com/123{Enter}');
    await screen.findByText('Not a YouTube or YouTube Music video link.');

    await user.type(urlInput(), '4');

    expect(screen.queryByText('Not a YouTube or YouTube Music video link.')).not.toBeInTheDocument();
    expect(urlInput()).toHaveAttribute('aria-invalid', 'false');
  });

  it('submits on Enter and shows the queued job', async () => {
    mockApi({ ...online, 'POST /api/analyze': () => json(job(), 202) });
    const user = userEvent.setup();
    await renderOnline();

    await user.type(urlInput(), `${VIDEO_URL}{Enter}`);

    expect(await screen.findByText('Queued')).toBeInTheDocument();
    expect(screen.getByRole('progressbar')).toBeInTheDocument();
  });

  it('polls the job and shows the acquired track', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    mockApi({
      ...online,
      'POST /api/analyze': () => json(job(), 202),
      'GET /api/analyze/job-1': () =>
        json(
          job({
            status: 'Preprocessing',
            track: { title: 'Me at the zoo', artist: 'jawed', channel: 'jawed', durationSeconds: 19 },
          }),
        ),
    });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    await renderOnline();

    await user.type(urlInput(), VIDEO_URL);
    await user.click(analyzeButton());
    await screen.findByText('Queued');

    await act(() => vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS));

    expect(await screen.findByText('Me at the zoo')).toBeInTheDocument();
    expect(screen.getByText('jawed · 0:19')).toBeInTheDocument();
    expect(screen.getByText('Preprocessing audio')).toBeInTheDocument();
  });

  it('shows the failure reason and stops the progress bar when a job fails', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    mockApi({
      ...online,
      'POST /api/analyze': () => json(job(), 202),
      'GET /api/analyze/job-1': () =>
        json(
          job({
            status: 'Failed',
            failedStage: 'Acquiring',
            failureReason: 'yt-dlp failed (exit code 1): Video unavailable',
          }),
        ),
    });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    await renderOnline();

    await user.type(urlInput(), VIDEO_URL);
    await user.click(analyzeButton());
    await screen.findByText('Queued');

    await act(() => vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Could not download the track');
    expect(alert).toHaveTextContent('yt-dlp failed (exit code 1): Video unavailable');
    expect(alert).toHaveTextContent('Check that the link points to a public video');
    expect(screen.getByText('Failed')).toBeInTheDocument();
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
  });

  it('shows the verdict, audio and signals once the job completes', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    mockApi({
      ...online,
      'POST /api/analyze': () => json(job(), 202),
      'GET /api/analyze/job-1': () =>
        json(job({ status: 'Completed', result: result(), visualization: visualization() })),
    });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    await renderOnline();

    await user.type(urlInput(), `${VIDEO_URL}{Enter}`);
    await screen.findByText('Queued');
    await act(() => vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS));

    expect(await screen.findByText('AI-generated')).toBeInTheDocument();
    expect(screen.getByRole('meter', { name: 'Confidence' })).toHaveAttribute('aria-valuenow', '74');
    expect(screen.getByRole('img', { name: 'Spectrogram of the analyzed excerpt' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Generator fingerprint' })).toBeInTheDocument();
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
  });

  it('shows the audio while the detectors are still running', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    mockApi({
      ...online,
      'POST /api/analyze': () => json(job(), 202),
      'GET /api/analyze/job-1': () => json(job({ status: 'Analyzing', visualization: visualization() })),
    });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    await renderOnline();

    await user.type(urlInput(), `${VIDEO_URL}{Enter}`);
    await screen.findByText('Queued');
    await act(() => vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS));

    expect(await screen.findByRole('img', { name: 'Waveform of the analyzed excerpt' })).toBeInTheDocument();
    expect(screen.getByText('Running detectors')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Verdict' })).not.toBeInTheDocument();
  });

  it('warns about a failed poll and keeps polling until it recovers', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    let polls = 0;
    mockApi({
      ...online,
      'POST /api/analyze': () => json(job(), 202),
      'GET /api/analyze/job-1': () => {
        polls++;
        return polls === 1 ? networkError() : json(job({ status: 'Acquiring' }));
      },
    });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    await renderOnline();

    await user.type(urlInput(), `${VIDEO_URL}{Enter}`);
    await screen.findByText('Queued');
    await act(() => vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS));

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not refresh the analysis');

    await act(() => vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS));

    expect(await screen.findByText('Downloading audio')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('stops polling and says so when the job no longer exists', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const fetchMock = mockApi({
      ...online,
      'POST /api/analyze': () => json(job(), 202),
      'GET /api/analyze/job-1': () => json({}, 404),
    });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    await renderOnline();

    await user.type(urlInput(), `${VIDEO_URL}{Enter}`);
    await screen.findByText('Queued');
    await act(() => vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS));

    expect(await screen.findByRole('alert')).toHaveTextContent('This analysis no longer exists');
    expect(screen.queryByText('Queued')).not.toBeInTheDocument();

    const calls = fetchMock.mock.calls.length;
    await act(() => vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS * 3));
    expect(fetchMock).toHaveBeenCalledTimes(calls);
  });
});
