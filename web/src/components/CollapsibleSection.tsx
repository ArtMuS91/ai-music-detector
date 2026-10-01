import { useId, type ReactNode } from 'react';
import ButtonBase from '@mui/material/ButtonBase';
import Collapse from '@mui/material/Collapse';
import Stack from '@mui/material/Stack';
import Typography, { type TypographyProps } from '@mui/material/Typography';
import ExpandMore from '@mui/icons-material/ExpandMore';
import { usePersistentState } from '../usePersistentState';

type CollapsibleSectionProps = {
  title: string;
  /** Heading level of the title; the toggle button sits inside it, so the heading keeps its name. */
  component: 'h2' | 'h3';
  variant: TypographyProps['variant'];
  /** Remembers the open/closed state in localStorage under this key; without it every mount starts open. */
  storageKey?: string;
  /** Shown at the end of the header row, outside the toggle (a status chip, say). */
  trailing?: ReactNode;
  /** Id for the heading, for an element labelled by it. */
  headingId?: string;
  children: ReactNode;
};

/** A titled section the viewer can show or hide by clicking its heading. Starts open. */
function CollapsibleSection({ title, component, variant, storageKey, trailing, headingId, children }: CollapsibleSectionProps) {
  const [expanded, setExpanded] = usePersistentState(storageKey, true);
  const bodyId = useId();

  return (
    <Stack>
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
        <Typography id={headingId} component={component} variant={variant} sx={{ flex: 1, m: 0 }}>
          <ButtonBase
            onClick={() => setExpanded(!expanded)}
            aria-expanded={expanded}
            aria-controls={bodyId}
            sx={{
              width: '100%',
              justifyContent: 'flex-start',
              gap: 0.5,
              py: 0.5,
              borderRadius: 1,
              font: 'inherit',
              textAlign: 'left',
              '&.Mui-focusVisible': { outline: 2, outlineColor: 'primary.main' },
            }}
          >
            <ExpandMore
              aria-hidden
              fontSize="small"
              sx={{
                color: 'text.secondary',
                transform: expanded ? 'none' : 'rotate(-90deg)',
                transition: 'transform 150ms',
              }}
            />
            {title}
          </ButtonBase>
        </Typography>
        {trailing}
      </Stack>
      {/* Unmounted when hidden: nothing collapsed is read out, and a canvas redraws on reopening. */}
      <Collapse in={expanded} id={bodyId} unmountOnExit>
        {children}
      </Collapse>
    </Stack>
  );
}

export default CollapsibleSection;
