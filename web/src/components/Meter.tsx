import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { formatPercent } from '../format';

type MeterProps = {
  label: string;
  /** 0..1 */
  value: number;
  color: 'primary' | 'secondary' | 'error' | 'warning' | 'info' | 'success';
};

/** A labelled 0..100% bar; a meter rather than a progressbar, since nothing is loading. */
function Meter({ label, value, color }: MeterProps) {
  const fraction = Math.max(0, Math.min(1, value));
  const percent = Math.round(fraction * 100);

  return (
    <Stack spacing={0.5}>
      <Stack direction="row" sx={{ justifyContent: 'space-between' }}>
        <Typography variant="body2" color="text.secondary">
          {label}
        </Typography>
        <Typography variant="body2">{formatPercent(fraction)}</Typography>
      </Stack>
      <Box
        role="meter"
        aria-label={label}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={percent}
        aria-valuetext={formatPercent(fraction)}
        sx={{ height: 8, borderRadius: 4, bgcolor: 'action.hover', overflow: 'hidden' }}
      >
        <Box sx={{ width: `${percent}%`, height: '100%', bgcolor: `${color}.main`, transition: 'width 300ms' }} />
      </Box>
    </Stack>
  );
}

export default Meter;
