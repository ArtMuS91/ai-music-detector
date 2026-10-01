import { useEffect, useRef } from 'react';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import type { AudioVisualization, Spectrogram } from '../models';
import { formatDuration, formatFrequency } from '../format';
import { frequencyTicks, spectrogramPixels, waveformPath } from '../visualization';

const WAVEFORM_HEIGHT = 72;
const SPECTROGRAM_HEIGHT = 160;

function Waveform({ peaks }: { peaks: number[] }) {
  return (
    <Box
      component="svg"
      role="img"
      aria-label="Waveform of the analyzed excerpt"
      viewBox={`0 0 ${Math.max(peaks.length, 1)} 100`}
      preserveAspectRatio="none"
      sx={{ display: 'block', width: '100%', height: WAVEFORM_HEIGHT, color: 'primary.main' }}
    >
      <path d={waveformPath(peaks)} fill="currentColor" />
    </Box>
  );
}

function SpectrogramImage({ spectrogram }: { spectrogram: Spectrogram }) {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const context = canvasRef.current?.getContext('2d');
    if (!context) {
      return;
    }

    const image = context.createImageData(spectrogram.frames, spectrogram.bands);
    image.data.set(spectrogramPixels(spectrogram));
    context.putImageData(image, 0, 0);
  }, [spectrogram]);

  return (
    <Box sx={{ position: 'relative' }}>
      <Box
        component="canvas"
        ref={canvasRef}
        width={spectrogram.frames}
        height={spectrogram.bands}
        role="img"
        aria-label="Spectrogram of the analyzed excerpt"
        sx={{ display: 'block', width: '100%', height: SPECTROGRAM_HEIGHT, borderRadius: 1 }}
      />
      {frequencyTicks(spectrogram.minFrequency, spectrogram.maxFrequency).map((tick) => (
        <Typography
          key={tick.hertz}
          variant="caption"
          aria-hidden
          sx={{
            position: 'absolute',
            left: 4,
            bottom: `${tick.position * 100}%`,
            transform: 'translateY(50%)',
            color: 'common.white',
            textShadow: '0 0 2px black',
            lineHeight: 1,
          }}
        >
          {`${formatFrequency(tick.hertz)} Hz`}
        </Typography>
      ))}
    </Box>
  );
}

/** Waveform and spectrogram of the excerpt the detectors heard, on a shared time axis. */
function AudioVisuals({ visualization }: { visualization: AudioVisualization }) {
  const start = visualization.startSeconds;
  const end = start + visualization.durationSeconds;

  return (
    <Stack spacing={1}>
      <Typography variant="h6" component="h2">
        Audio
      </Typography>
      <Waveform peaks={visualization.waveform} />
      <SpectrogramImage spectrogram={visualization.spectrogram} />
      <Stack direction="row" sx={{ justifyContent: 'space-between' }}>
        <Typography variant="caption" color="text.secondary">
          {formatDuration(start)}
        </Typography>
        <Typography variant="caption" color="text.secondary">
          {`Analyzed excerpt ${formatDuration(start)}–${formatDuration(end)}`}
        </Typography>
        <Typography variant="caption" color="text.secondary">
          {formatDuration(end)}
        </Typography>
      </Stack>
    </Stack>
  );
}

export default AudioVisuals;
