import { render, screen, within } from '@testing-library/react';
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

  it('says so when there are no signals', () => {
    render(<SignalBreakdown signals={[]} />);

    expect(screen.getByText('No detection signals were available for this track.')).toBeInTheDocument();
  });
});
