import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import CollapsibleSection from './CollapsibleSection';

function renderSection(storageKey?: string) {
  return render(
    <CollapsibleSection title="Audio" component="h2" variant="h6" storageKey={storageKey}>
      <p>Waveform goes here</p>
    </CollapsibleSection>,
  );
}

function toggle() {
  return screen.getByRole('button', { name: 'Audio' });
}

describe('CollapsibleSection', () => {
  it('starts open, with its toggle inside a heading of the same name', () => {
    renderSection();

    expect(screen.getByRole('heading', { level: 2, name: 'Audio' })).toContainElement(toggle());
    expect(toggle()).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByText('Waveform goes here')).toBeInTheDocument();
  });

  it('hides and shows its content from the heading', async () => {
    const user = userEvent.setup();
    renderSection();

    await user.click(toggle());

    expect(toggle()).toHaveAttribute('aria-expanded', 'false');
    await waitFor(() => expect(screen.queryByText('Waveform goes here')).not.toBeInTheDocument());

    await user.click(toggle());

    expect(toggle()).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByText('Waveform goes here')).toBeInTheDocument();
  });

  it('points its toggle at the content it controls', () => {
    renderSection();

    const controlled = document.getElementById(toggle().getAttribute('aria-controls')!);
    expect(controlled).toContainElement(screen.getByText('Waveform goes here'));
  });

  it('remembers being closed across remounts when given a storage key', async () => {
    const user = userEvent.setup();
    const { unmount } = renderSection('test.audio');
    await user.click(toggle());
    unmount();

    renderSection('test.audio');

    expect(toggle()).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByText('Waveform goes here')).not.toBeInTheDocument();
  });

  it('opens fresh on every mount without a storage key', async () => {
    const user = userEvent.setup();
    const { unmount } = renderSection();
    await user.click(toggle());
    unmount();

    renderSection();

    expect(toggle()).toHaveAttribute('aria-expanded', 'true');
  });

  it('still toggles when storage is unavailable', async () => {
    vi.spyOn(window.localStorage, 'getItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    vi.spyOn(window.localStorage, 'setItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    const user = userEvent.setup();
    renderSection('test.audio');

    await user.click(toggle());

    expect(toggle()).toHaveAttribute('aria-expanded', 'false');
  });
});
