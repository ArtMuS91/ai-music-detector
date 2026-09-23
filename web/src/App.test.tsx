import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import App from './App';
import { job, json, mockApi, networkError } from './test/mockApi';

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

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Not a YouTube or YouTube Music video link.',
    );
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
    expect(screen.getByText('Audio acquired — analysis not implemented yet')).toBeInTheDocument();
  });

  it('shows the failure reason and stops the progress bar when a job fails', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    mockApi({
      ...online,
      'POST /api/analyze': () => json(job(), 202),
      'GET /api/analyze/job-1': () =>
        json(job({ status: 'Failed', failureReason: 'yt-dlp failed (exit code 1): Video unavailable' })),
    });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    await renderOnline();

    await user.type(urlInput(), VIDEO_URL);
    await user.click(analyzeButton());
    await screen.findByText('Queued');

    await act(() => vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS));

    expect(await screen.findByText('yt-dlp failed (exit code 1): Video unavailable')).toBeInTheDocument();
    expect(screen.getByText('Failed')).toBeInTheDocument();
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
  });
});
