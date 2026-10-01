import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import SignalBreakdown from './SignalBreakdown';
import { signal } from '../test/mockApi';

describe('SignalBreakdown', () => {
  it('lists the heaviest signals first', () => {
    render(
      <SignalBreakdown
        signals={[
          signal({ name: 'Metadata', weight: 0 }),
          signal({ name: 'Generator fingerprint', weight: 0.8 }),
          signal({ name: 'Web research', weight: 0.4 }),
        ]}
      />,
    );

    const names = screen.getAllByRole('heading', { level: 3 }).map((heading) => heading.textContent);
    expect(names).toEqual(['Generator fingerprint', 'Web research', 'Metadata']);
  });

  it.each([
    [0.95, 0.8, 'Leans AI'],
    [0.05, 0.8, 'Leans human'],
    [0.55, 0.8, 'Undecided'],
    [0.95, 0, 'No evidence either way'],
  ])('labels score %s at weight %s as "%s"', (score, weight, label) => {
    render(<SignalBreakdown signals={[signal({ score, weight })]} />);

    expect(screen.getByText(label)).toBeInTheDocument();
  });

  it('shows score, weight and detail', () => {
    render(<SignalBreakdown signals={[signal({ score: 0.97, weight: 0.8, detail: 'Model probability 0.97.' })]} />);

    expect(screen.getByText('Score 0.97 · weight 80%')).toBeInTheDocument();
    expect(screen.getByText('Model probability 0.97.')).toBeInTheDocument();
  });

  it('links evidence in a new tab with its stance', () => {
    render(
      <SignalBreakdown
        signals={[
          signal({
            name: 'Web research',
            evidence: [{ url: 'https://example.com/ai-band', title: 'AI band exposed', stance: 'AiGenerated' }],
          }),
        ]}
      />,
    );

    const link = screen.getByRole('link', { name: 'AI band exposed' });
    expect(link).toHaveAttribute('href', 'https://example.com/ai-band');
    expect(link).toHaveAttribute('target', '_blank');
    expect(link).toHaveAttribute('rel', 'noopener noreferrer');
    expect(within(link.closest('li')!).getByText('Says AI')).toBeInTheDocument();
  });

  it('does not render non-web evidence urls as links', () => {
    render(
      <SignalBreakdown
        signals={[
          signal({
            evidence: [
              { url: 'javascript:alert(1)', title: 'Sneaky', stance: 'AiGenerated' },
              { url: 'https://example.com/story', title: 'Real story', stance: 'Human' },
            ],
          }),
        ]}
      />,
    );

    expect(screen.queryByText('Sneaky')).not.toBeInTheDocument();
    expect(screen.getAllByRole('link').map((link) => link.getAttribute('href'))).toEqual([
      'https://example.com/story',
    ]);
  });

  it("hides one signal's details without touching the others", async () => {
    const user = userEvent.setup();
    render(
      <SignalBreakdown
        signals={[
          signal({ name: 'Generator fingerprint', detail: 'Model probability 0.97.' }),
          signal({ name: 'Web research', detail: 'Reported as an AI band.' }),
        ]}
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Generator fingerprint' }));

    await waitFor(() => expect(screen.queryByText('Model probability 0.97.')).not.toBeInTheDocument());
    expect(screen.getByText('Reported as an AI band.')).toBeInTheDocument();
    // The lean stays visible on a collapsed signal.
    expect(screen.getAllByText('Leans AI')).toHaveLength(2);
  });

  it('says so when there are no signals', () => {
    render(<SignalBreakdown signals={[]} />);

    expect(screen.getByText('No detection signals were available for this track.')).toBeInTheDocument();
  });
});
