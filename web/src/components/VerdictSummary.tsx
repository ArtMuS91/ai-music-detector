import type { ReactElement } from 'react';
import Box from '@mui/material/Box';
import Grid from '@mui/material/Grid';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import SmartToyOutlined from '@mui/icons-material/SmartToyOutlined';
import PersonOutlined from '@mui/icons-material/PersonOutlined';
import CallSplitOutlined from '@mui/icons-material/CallSplitOutlined';
import HelpOutlineOutlined from '@mui/icons-material/HelpOutlineOutlined';
import type { AnalysisResult, AnalysisVerdict, Signal } from '../models';
import { formatPercent } from '../format';
import { highlightParts, leanOf, type Lean } from '../signals';
import { numberSx, signalNameSx } from '../theme';
import Meter from './Meter';

const VERDICTS: Record<AnalysisVerdict, { label: string; background: string; foreground: string; icon: ReactElement }> = {
  AiGenerated: {
    label: 'AI-generated',
    background: 'error.main',
    foreground: 'error.contrastText',
    icon: <SmartToyOutlined fontSize="inherit" />,
  },
  Human: {
    label: 'Human-made',
    background: 'success.main',
    foreground: 'success.contrastText',
    icon: <PersonOutlined fontSize="inherit" />,
  },
  Mixed: {
    label: 'Mixed',
    background: 'warning.main',
    foreground: 'warning.contrastText',
    icon: <CallSplitOutlined fontSize="inherit" />,
  },
  Inconclusive: {
    label: 'Inconclusive',
    background: 'grey.700',
    foreground: 'common.white',
    icon: <HelpOutlineOutlined fontSize="inherit" />,
  },
};

const COLUMNS: { title: string; leans: Lean[]; accent: string }[] = [
  { title: 'Points to AI', leans: ['ai'], accent: 'error.main' },
  { title: 'Points to human', leans: ['human'], accent: 'success.main' },
  { title: 'No clear lean', leans: ['undecided', 'none'], accent: 'text.disabled' },
];

function confidenceWord(confidence: number) {
  if (confidence >= 0.7) {
    return 'High confidence';
  }

  if (confidence >= 0.4) {
    return 'Moderate confidence';
  }

  return 'Low confidence';
}

function VerdictBanner({ result }: { result: AnalysisResult }) {
  const verdict = VERDICTS[result.verdict];

  return (
    <Stack
      direction="row"
      spacing={2}
      sx={{
        alignItems: 'center',
        p: { xs: 2, sm: 3 },
        borderRadius: 2,
        boxShadow: 3,
        bgcolor: verdict.background,
        color: verdict.foreground,
      }}
    >
      <Box aria-hidden sx={{ fontSize: { xs: 40, sm: 56 }, display: 'flex' }}>
        {verdict.icon}
      </Box>
      <Box>
        <Typography variant="overline" sx={{ opacity: 0.85, lineHeight: 1.5 }}>
          Verdict
        </Typography>
        <Typography variant="h4" component="h2" sx={{ fontWeight: 700, lineHeight: 1.1 }}>
          {verdict.label}
        </Typography>
        <Typography variant="body2" sx={{ opacity: 0.9, mt: 0.5 }}>
          {confidenceWord(result.confidence)}
        </Typography>
      </Box>
    </Stack>
  );
}

/** The signals sorted into columns by which way they point, heaviest first. */
function SignalColumns({ signals }: { signals: Signal[] }) {
  return (
    <Stack spacing={1}>
      <Typography variant="subtitle2" component="h3">
        How the signals lean
      </Typography>
      <Grid container spacing={1.5}>
        {COLUMNS.map((column) => {
          const members = signals
            .filter((signal) => column.leans.includes(leanOf(signal)))
            .sort((a, b) => b.weight - a.weight);

          return (
            <Grid key={column.title} size={{ xs: 12, sm: 4 }}>
              <Box
                sx={{
                  height: '100%',
                  p: 1.5,
                  borderRadius: 1,
                  borderTop: 3,
                  borderColor: column.accent,
                  bgcolor: 'action.hover',
                }}
              >
                <Typography variant="caption" color="text.secondary" component="h4" sx={{ fontWeight: 600 }}>
                  {column.title}
                </Typography>
                {members.length === 0 ? (
                  <Typography variant="body2" color="text.disabled">
                    None
                  </Typography>
                ) : (
                  <Box component="ul" aria-label={column.title} sx={{ listStyle: 'none', p: 0, m: 0 }}>
                    {members.map((signal) => (
                      <Box component="li" key={signal.name} sx={{ mt: 0.5 }}>
                        <Typography variant="body2" sx={{ fontWeight: 600 }}>
                          {signal.name}
                        </Typography>
                        <Typography variant="caption" color="text.secondary">
                          {signal.weight === 0 ? 'Found nothing' : `Weight ${formatPercent(signal.weight)}`}
                        </Typography>
                      </Box>
                    ))}
                  </Box>
                )}
              </Box>
            </Grid>
          );
        })}
      </Grid>
    </Stack>
  );
}

/** The explanation with signal names and figures picked out, so its key facts can be skimmed. */
function Explanation({ text, signals }: { text: string; signals: Signal[] }) {
  return (
    <Stack spacing={1}>
      <Typography variant="subtitle2" component="h3">
        Explanation
      </Typography>
      <Typography variant="body2" sx={{ borderLeft: 4, borderColor: 'divider', pl: 2, lineHeight: 1.7 }}>
        {highlightParts(text, signals).map((part, index) => {
          if (part.kind === 'signal') {
            return (
              <Box component="strong" key={index} sx={signalNameSx}>
                {part.text}
              </Box>
            );
          }

          if (part.kind === 'number') {
            return (
              <Box component="strong" key={index} sx={numberSx}>
                {part.text}
              </Box>
            );
          }

          return part.text;
        })}
      </Typography>
    </Stack>
  );
}

function VerdictSummary({ result }: { result: AnalysisResult }) {
  return (
    <Stack spacing={2.5}>
      <VerdictBanner result={result} />
      <Grid container spacing={2}>
        <Grid size={{ xs: 12, sm: 6 }}>
          <Meter label="AI likelihood" value={result.aiProbability} color="secondary" />
        </Grid>
        <Grid size={{ xs: 12, sm: 6 }}>
          <Meter label="Confidence" value={result.confidence} color="primary" />
        </Grid>
      </Grid>
      {result.signals.length > 0 && <SignalColumns signals={result.signals} />}
      {result.explanation && <Explanation text={result.explanation} signals={result.signals} />}
    </Stack>
  );
}

export default VerdictSummary;
