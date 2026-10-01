import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import AudioVisuals from './AudioVisuals';
import { visualization } from '../test/mockApi';

describe('AudioVisuals', () => {
  it('draws the waveform and the spectrogram', () => {
    render(<AudioVisuals visualization={visualization()} />);

    expect(screen.getByRole('img', { name: 'Waveform of the analyzed excerpt' })).toBeInTheDocument();
    const spectrogram = screen.getByRole('img', { name: 'Spectrogram of the analyzed excerpt' });
    expect(spectrogram).toHaveAttribute('width', '2');
    expect(spectrogram).toHaveAttribute('height', '2');
  });

  it('places the excerpt within the full track', () => {
    render(<AudioVisuals visualization={visualization({ startSeconds: 90, durationSeconds: 180 })} />);

    expect(screen.getByText('Analyzed excerpt 1:30–4:30')).toBeInTheDocument();
  });

  it('labels the frequency axis', () => {
    render(<AudioVisuals visualization={visualization()} />);

    expect(screen.getByText('100 Hz')).toBeInTheDocument();
    expect(screen.getByText('1k Hz')).toBeInTheDocument();
    expect(screen.getByText('10k Hz')).toBeInTheDocument();
  });
});
