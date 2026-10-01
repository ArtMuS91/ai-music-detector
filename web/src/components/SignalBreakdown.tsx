import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import Link from '@mui/material/Link';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import type { EvidenceStance, Signal } from '../models';
import { formatPercent } from '../format';
import { leanOf, type Lean } from '../signals';
import CollapsibleSection from './CollapsibleSection';

type ChipColor = 'error' | 'success' | 'default';

const STANCES: Record<EvidenceStance, { label: string; color: ChipColor }> = {
  AiGenerated: { label: 'Says AI', color: 'error' },
  Human: { label: 'Says human', color: 'success' },
  Neutral: { label: 'Neutral', color: 'default' },
};

/** Evidence comes from web search; anything but an http(s) link (javascript:, data:) is not rendered as one. */
function isWebUrl(url: string) {
  try {
    return ['http:', 'https:'].includes(new URL(url).protocol);
  } catch {
    return false;
  }
}

const LEANS: Record<Lean, { label: string; color: ChipColor }> = {
  ai: { label: 'Leans AI', color: 'error' },
  human: { label: 'Leans human', color: 'success' },
  undecided: { label: 'Undecided', color: 'default' },
  none: { label: 'No evidence either way', color: 'default' },
};

/** A bar growing from the middle: left toward human, right toward AI. */
function LeanBar({ signal }: { signal: Signal }) {
  const offset = Math.abs(signal.score - 0.5) * 100;
  const towardAi = signal.score >= 0.5;

  return (
    <Box aria-hidden sx={{ position: 'relative', height: 6, borderRadius: 3, bgcolor: 'action.hover' }}>
      <Box sx={{ position: 'absolute', left: '50%', top: -2, bottom: -2, width: '1px', bgcolor: 'text.disabled' }} />
      {signal.weight > 0 && (
        <Box
          sx={{
            position: 'absolute',
            top: 0,
            bottom: 0,
            borderRadius: 3,
            left: towardAi ? '50%' : `${50 - offset}%`,
            width: `${offset}%`,
            bgcolor: towardAi ? 'error.main' : 'success.main',
          }}
        />
      )}
    </Box>
  );
}

function SignalItem({ signal }: { signal: Signal }) {
  const { label, color } = LEANS[leanOf(signal)];

  return (
    // Each signal is its own bordered panel, so where one ends and the next begins is plain.
    <Stack
      component="li"
      sx={{ p: 2, border: 1, borderColor: 'divider', borderRadius: 2, bgcolor: 'action.hover' }}
    >
      <CollapsibleSection
        title={signal.name}
        component="h3"
        variant="subtitle2"
        trailing={<Chip size="small" variant="outlined" label={label} color={color} />}
      >
        <Stack spacing={1} sx={{ pt: 1 }}>
          <LeanBar signal={signal} />
          <Stack direction="row" sx={{ justifyContent: 'space-between' }}>
            <Typography variant="caption" color="text.secondary">
              Human
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {`Score ${signal.score.toFixed(2)} · weight ${formatPercent(signal.weight)}`}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              AI
            </Typography>
          </Stack>
          {signal.detail && (
            <Typography variant="body2" color="text.secondary">
              {signal.detail}
            </Typography>
          )}
          {signal.evidence.some((link) => isWebUrl(link.url)) && (
            <Stack component="ul" spacing={0.5} sx={{ listStyle: 'none', p: 0, m: 0 }}>
              {signal.evidence.filter((link) => isWebUrl(link.url)).map((link) => (
                <Stack
                  component="li"
                  key={link.url}
                  direction="row"
                  spacing={1}
                  sx={{ alignItems: 'center', minWidth: 0 }}
                >
                  <Chip size="small" label={STANCES[link.stance].label} color={STANCES[link.stance].color} />
                  <Link href={link.url} target="_blank" rel="noopener noreferrer" variant="body2" noWrap>
                    {link.title ?? link.url}
                  </Link>
                </Stack>
              ))}
            </Stack>
          )}
        </Stack>
      </CollapsibleSection>
    </Stack>
  );
}

function SignalBreakdown({ signals }: { signals: Signal[] }) {
  // Heaviest first: those are the ones that move the verdict.
  const ordered = [...signals].sort((a, b) => b.weight - a.weight);

  return (
    <Stack spacing={0.5}>
      <Typography variant="h6" component="h2">
        Signals
      </Typography>
      {ordered.length === 0 ? (
        <Typography variant="body2" color="text.secondary">
          No detection signals were available for this track.
        </Typography>
      ) : (
        <Stack component="ul" aria-label="Signals" spacing={1.5} sx={{ listStyle: 'none', p: 0, m: 0, pt: 1 }}>
          {ordered.map((signal) => (
            <SignalItem key={signal.name} signal={signal} />
          ))}
        </Stack>
      )}
    </Stack>
  );
}

export default SignalBreakdown;
