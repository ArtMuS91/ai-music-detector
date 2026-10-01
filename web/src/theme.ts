import { createTheme } from '@mui/material/styles';

/** How a signal's name is set wherever it appears in running text (explanation, legend). */
export const signalNameSx = { color: 'primary.main', fontWeight: 700 } as const;

/** How numbers are picked out in the explanation, so the figures stand out from the prose. */
export const numberSx = {
  fontWeight: 700,
  fontVariantNumeric: 'tabular-nums',
  bgcolor: 'action.selected',
  borderRadius: 0.5,
  px: 0.5,
} as const;

// Both schemes are emitted as CSS variables, switched by a class on <html>: the UI follows the OS
// setting until the viewer picks one with the theme toggle.
export const theme = createTheme({
  cssVariables: { colorSchemeSelector: 'class' },
  colorSchemes: {
    light: true,
    dark: true,
  },
  components: {
    MuiCssBaseline: {
      styleOverrides: (theme) => ({
        // Soft coloured glows at the top fading into the page colour, after YouTube Music's player
        // page. Fixed, so the long result scrolls over the glow instead of dragging it along.
        body: {
          minHeight: '100vh',
          backgroundAttachment: 'fixed',
          backgroundImage: [
            'radial-gradient(ellipse 60% 50% at 15% 0%, rgba(214, 51, 108, 0.16), transparent 70%)',
            'radial-gradient(ellipse 55% 45% at 85% 5%, rgba(20, 160, 150, 0.16), transparent 70%)',
            'linear-gradient(180deg, rgba(25, 118, 210, 0.05) 0%, transparent 60%)',
          ].join(', '),
          ...theme.applyStyles('dark', {
            backgroundImage: [
              'radial-gradient(ellipse 60% 50% at 15% 0%, rgba(150, 30, 70, 0.45), transparent 70%)',
              'radial-gradient(ellipse 55% 45% at 85% 5%, rgba(10, 110, 100, 0.40), transparent 70%)',
              'linear-gradient(180deg, #120c12 0%, #000000 70%)',
            ].join(', '),
          }),
        },
      }),
    },
    MuiCard: {
      styleOverrides: {
        // Slightly see-through so the page glow shows behind the cards, frosted to keep text legible.
        root: {
          backgroundColor: 'color-mix(in srgb, var(--mui-palette-background-paper) 82%, transparent)',
          backdropFilter: 'blur(12px)',
        },
      },
    },
  },
});
