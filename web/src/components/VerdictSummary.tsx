import type { ReactElement } from 'react';
import Chip from '@mui/material/Chip';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import SmartToyOutlined from '@mui/icons-material/SmartToyOutlined';
import PersonOutlined from '@mui/icons-material/PersonOutlined';
import CallSplitOutlined from '@mui/icons-material/CallSplitOutlined';
import HelpOutlineOutlined from '@mui/icons-material/HelpOutlineOutlined';
import type { AnalysisResult, AnalysisVerdict } from '../models';
import Meter from './Meter';

const VERDICTS: Record<
  AnalysisVerdict,
  { label: string; color: 'error' | 'success' | 'warning' | 'default'; icon: ReactElement }
> = {
  AiGenerated: { label: 'AI-generated', color: 'error', icon: <SmartToyOutlined /> },
  Human: { label: 'Human-made', color: 'success', icon: <PersonOutlined /> },
  Mixed: { label: 'Mixed', color: 'warning', icon: <CallSplitOutlined /> },
  Inconclusive: { label: 'Inconclusive', color: 'default', icon: <HelpOutlineOutlined /> },
};

function VerdictSummary({ result }: { result: AnalysisResult }) {
  const verdict = VERDICTS[result.verdict];

  return (
    <Stack spacing={2}>
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
        <Typography variant="h6" component="h2">
          Verdict
        </Typography>
        <Chip icon={verdict.icon} label={verdict.label} color={verdict.color} />
      </Stack>
      <Meter label="Confidence" value={result.confidence} color="primary" />
      <Meter label="AI likelihood" value={result.aiProbability} color="secondary" />
      {result.explanation && <Typography variant="body2">{result.explanation}</Typography>}
    </Stack>
  );
}

export default VerdictSummary;
