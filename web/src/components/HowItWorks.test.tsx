import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import HowItWorks from './HowItWorks';

describe('HowItWorks', () => {
  it('is a labelled side panel', () => {
    render(<HowItWorks />);

    expect(screen.getByRole('complementary', { name: 'How it works' })).toBeInTheDocument();
  });

  it('can be folded down to its title', async () => {
    const user = userEvent.setup();
    render(<HowItWorks />);

    await user.click(screen.getByRole('button', { name: 'How it works' }));

    await waitFor(() => expect(screen.queryByText('Built with')).not.toBeInTheDocument());
    expect(screen.getByRole('complementary', { name: 'How it works' })).toBeInTheDocument();
  });

  it('walks through the pipeline steps in order', () => {
    render(<HowItWorks />);

    const steps = screen.getAllByRole('heading', { level: 3 }).map((heading) => heading.textContent);
    expect(steps).toEqual(['Download', 'Prepare', 'Detect', 'Decide', 'Built with']);
  });

  it('names each signal', () => {
    render(<HowItWorks />);

    for (const name of ['Generator fingerprint', 'Web research', 'Metadata clues']) {
      expect(screen.getByText(name)).toBeInTheDocument();
    }
  });

  it('links the tools and models each stage runs on', () => {
    render(<HowItWorks />);

    expect(screen.getByRole('link', { name: 'lofcz/ai-music-detector' })).toHaveAttribute(
      'href',
      'https://huggingface.co/lofcz/ai-music-detector',
    );
    for (const name of ['yt-dlp', 'FFmpeg', 'openai/gpt-oss-120b on Groq', 'whisper-large-v3-turbo on Groq']) {
      expect(screen.getByRole('link', { name })).toHaveAttribute('rel', 'noopener noreferrer');
    }
  });

  it('says the audio is deleted after the analysis', () => {
    render(<HowItWorks />);

    expect(screen.getByText(/audio is deleted as soon as the analysis finishes/)).toBeInTheDocument();
  });
});
