import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import VerdictSummary from './VerdictSummary';
import { result } from '../test/mockApi';

describe('VerdictSummary', () => {
  it.each([
    ['AiGenerated', 'AI-generated'],
    ['Human', 'Human-made'],
    ['Mixed', 'Mixed'],
    ['Inconclusive', 'Inconclusive'],
  ] as const)('labels the %s verdict "%s"', (verdict, label) => {
    render(<VerdictSummary result={result({ verdict })} />);

    expect(screen.getByText(label)).toBeInTheDocument();
  });

  it('shows confidence and AI likelihood as meters', () => {
    render(<VerdictSummary result={result({ confidence: 0.74, aiProbability: 0.912 })} />);

    const confidence = screen.getByRole('meter', { name: 'Confidence' });
    expect(confidence).toHaveAttribute('aria-valuenow', '74');
    expect(confidence).toHaveAttribute('aria-valuetext', '74%');
    expect(screen.getByRole('meter', { name: 'AI likelihood' })).toHaveAttribute('aria-valuenow', '91');
  });

  it('shows the explanation', () => {
    render(<VerdictSummary result={result({ explanation: 'The audio carries a generator fingerprint.' })} />);

    expect(screen.getByText('The audio carries a generator fingerprint.')).toBeInTheDocument();
  });
});
