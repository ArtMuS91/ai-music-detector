import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import App from './App';
import { job, json, mockApi, networkError, result, visualization } from './test/mockApi';

const VIDEO_URL = 'https://music.youtube.com/watch?v=jNQXAC9IVRw';
const POLL_INTERVAL_MS = 1500;

function urlInput() {
  return screen.getByRole('textbox', { name: 'Track URL' });
}

function analyzeButton() {
  return screen.getByRole('button', { name: 'Analyze' });
}

describe('App', () => {
  it('suggests YouTube Music and Spotify links as the placeholder', () => {
    mockApi({});
    render(<App />);

    expect(urlInput()).toHaveAttribute(
      'placeholder',
      'https://music.youtube.com/watch?v= or https://open.spotify.com/track/',
    );
  });

  it('explains how the analysis works beside the form', () => {
    mockApi({});
    render(<App />);

    expect(screen.getByRole('complementary', { name: 'How it works' })).toBeInTheDocument();
  });

  it('enables Analyze once a url is entered', async () => {
    mockApi({});
    const user = userEvent.setup();
    render(<App />);

    expect(analyzeButton()).toBeDisabled();

    await user.type(urlInput(), VIDEO_URL);

    expect(analyzeButton()).toBeEnabled();
  });

  it('does not submit an empty url on Enter', async () => {
    const fetchMock = mockApi({});
    const user = userEvent.setup();
    render(<App />);

    await user.type(urlInput(), '{Enter}');

    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('analyzes the link given in the address as soon as the page opens', async () => {
    window.history.replaceState(null, '', `/?url=${encodeURIComponent(VIDEO_URL)}`);
    const fetchMock = mockApi({
      'POST /api/analyze': () => json(job({ status: 'Completed', result: result() }), 200),
    });
    render(<App />);

    expect(await screen.findByRole('heading', { level: 2, name: 'AI-generated' })).toBeInTheDocument();
    expect(urlInput()).toHaveValue(VIDEO_URL);
    expect(JSON.parse(fetchMock.mock.calls[0][1]?.body as string)).toEqual({ url: VIDEO_URL });
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('puts the submitted link into the address, so the page can be shared', async () => {
    mockApi({ 'POST /api/analyze': () => json(job(), 202) });
    const user = userEvent.setup();
    render(<App />);

    await user.type(urlInput(), `  ${VIDEO_URL}  {Enter}`);
    await screen.findByText('Queued');

    expect(new URLSearchParams(window.location.search).get('url')).toBe(VIDEO_URL);
  });

  it('has no API status badge', () => {
    mockApi({});
    render(<App />);

    expect(screen.queryByText(/^API:/)).not.toBeInTheDocument();
  });

  it('says so when the API cannot be reached', async () => {
    mockApi({ 'POST /api/analyze': networkError });
    const user = userEvent.setup();
    render(<App />);

    await user.type(urlInput(), `${VIDEO_URL}{Enter}`);

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not reach the API');
  });

  it('shows the stored result of an already analyzed link at once, without polling', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const fetchMock = mockApi({
      'POST /api/analyze': () =>
        json(job({ status: 'Completed', updatedAt: '2026-09-30T18:45:00Z', result: result() }), 200),
    });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<App />);

    await user.type(urlInput(), `${VIDEO_URL}{Enter}`);

    expect(await screen.findByRole('heading', { level: 2, name: 'AI-generated' })).toBeInTheDocument();
    expect(screen.getByText(/^Result from /)).toBeInTheDocument();
    await act(() => vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS * 3));
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('shows the server validation message for a rejected url', async () => {
    mockApi({
      'POST /api/analyze': () =>
        json({ errors: { Url: ['Not a YouTube, YouTube Music or Spotify track link.'] } }, 400),
    });
    const user = userEvent.setup();
    render(<App />);

    await user.type(urlInput(), 'https://vimeo.com/123');
    await user.click(analyzeButton());

    expect(await screen.findByText('Not a YouTube, YouTube Music or Spotify track link.')).toBeInTheDocument();
    expect(urlInput()).toHaveAccessibleDescription('Not a YouTube, YouTube Music or Spotify track link.');
    expect(urlInput()).toHaveAttribute('aria-invalid', 'true');
  });

  it('clears the url error once the url is edited', async () => {
    mockApi({
      'POST /api/analyze': () =>
        json({ errors: { Url: ['Not a YouTube, YouTube Music or Spotify track link.'] } }, 400),
    });
    const user = userEvent.setup();
    render(<App />);
    await user.type(urlInput(), 'https://vimeo.com/123{Enter}');
    await screen.findByText('Not a YouTube, YouTube Music or Spotify track link.');

    await user.type(urlInput(), '4');

    expect(screen.queryByText('Not a YouTube, YouTube Music or Spotify track link.')).not.toBeInTheDocument();
    expect(urlInput()).toHaveAttribute('aria-invalid', 'false');
  });

  it('submits on Enter and shows the queued job', async () => {
    mockApi({ 'POST /api/analyze': () => json(job(), 202) });
    const user = userEvent.setup();
    render(<App />);

    await user.type(urlInput(), `${VIDEO_URL}{Enter}`);

    expect(await screen.findByText('Queued')).toBeInTheDocument();
    expect(screen.getByRole('progressbar')).toBeInTheDocument();
  });

  it('polls the job and shows the acquired track', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    mockApi({
      'POST /api/analyze': () => json(job(), 202),
      'GET /api/analyze/job-1': () =>
        json(
          job({
            status: 'Preprocessing',
            track: { title: 'Me at the zoo', artist: 'jawed', channel: 'jawed', durationSeconds: 19, matchedUrl: null },
          }),
        ),
    });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<App />);

    await user.type(urlInput(), VIDEO_URL);
    await user.click(analyzeButton());
    await screen.findByText('Queued');

    await act(() => vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS));

    expect(await screen.findByText('Me at the zoo')).toBeInTheDocument();
    expect(screen.getByText('jawed · 0:19')).toBeInTheDocument();
    expect(screen.getByText('Preprocessing audio')).toBeInTheDocument();
  });

  it('links the YouTube video a Spotify track was analyzed from', async () => {
    mockApi({
      'POST /api/analyze': () =>
        json(
          job({
            sourceUrl: 'https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC',
            status: 'Completed',
            track: {
              title: 'Never Gonna Give You Up',
              artist: 'Rick Astley',
              channel: 'Rick Astley',
              durationSeconds: 213.6,
              matchedUrl: 'https://www.youtube.com/watch?v=dQw4w9WgXcQ',
            },
            result: result(),
          }),
          200,
        ),
    });
    const user = userEvent.setup();
    render(<App />);

    await user.type(urlInput(), 'https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC{Enter}');

    expect(await screen.findByRole('link', { name: 'YouTube match' })).toHaveAttribute(
      'href',
      'https://www.youtube.com/watch?v=dQw4w9WgXcQ',
    );
    expect(screen.getByText('Rick Astley · 3:34')).toBeInTheDocument();
  });

  it('shows the failure reason and stops the progress bar when a job fails', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    mockApi({
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
    render(<App />);

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
      'POST /api/analyze': () => json(job(), 202),
      'GET /api/analyze/job-1': () =>
        json(job({ status: 'Completed', result: result(), visualization: visualization() })),
    });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<App />);

    await user.type(urlInput(), `${VIDEO_URL}{Enter}`);
    await screen.findByText('Queued');
    await act(() => vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS));

    expect(await screen.findByRole('heading', { level: 2, name: 'AI-generated' })).toBeInTheDocument();
    expect(screen.getByRole('meter', { name: 'Confidence' })).toHaveAttribute('aria-valuenow', '74');
    expect(screen.getByRole('img', { name: 'Spectrogram of the analyzed excerpt' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Generator fingerprint' })).toBeInTheDocument();
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
  });

  it('shows the audio while the detectors are still running', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    mockApi({
      'POST /api/analyze': () => json(job(), 202),
      'GET /api/analyze/job-1': () => json(job({ status: 'Analyzing', visualization: visualization() })),
    });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<App />);

    await user.type(urlInput(), `${VIDEO_URL}{Enter}`);
    await screen.findByText('Queued');
    await act(() => vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS));

    expect(await screen.findByRole('img', { name: 'Waveform of the analyzed excerpt' })).toBeInTheDocument();
    expect(screen.getByText('Running detectors')).toBeInTheDocument();
    expect(screen.queryByRole('meter', { name: 'Confidence' })).not.toBeInTheDocument();
  });

  it('warns about a failed poll and keeps polling until it recovers', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    let polls = 0;
    mockApi({
      'POST /api/analyze': () => json(job(), 202),
      'GET /api/analyze/job-1': () => {
        polls++;
        return polls === 1 ? networkError() : json(job({ status: 'Acquiring' }));
      },
    });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<App />);

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
      'POST /api/analyze': () => json(job(), 202),
      'GET /api/analyze/job-1': () => json({}, 404),
    });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<App />);

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
