import { createTheme } from '@mui/material/styles';

// Both schemes are emitted as CSS variables under prefers-color-scheme, so the UI tracks the OS setting.
export const theme = createTheme({
  cssVariables: true,
  colorSchemes: {
    light: true,
    dark: true,
  },
});
