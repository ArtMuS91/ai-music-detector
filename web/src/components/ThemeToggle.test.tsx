import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ThemeProvider } from '@mui/material/styles';
import { afterEach, describe, expect, it } from 'vitest';
import { theme } from '../theme';
import ThemeToggle from './ThemeToggle';

function renderToggle() {
  return render(
    <ThemeProvider theme={theme}>
      <ThemeToggle />
    </ThemeProvider>,
  );
}

describe('ThemeToggle', () => {
  afterEach(() => {
    // MUI marks the chosen scheme on <html>, which jsdom keeps between tests.
    document.documentElement.className = '';
  });

  it('switches between the dark and light scheme', async () => {
    const user = userEvent.setup();
    renderToggle();

    await user.click(screen.getByRole('button', { name: 'Switch to dark theme' }));
    expect(document.documentElement).toHaveClass('dark');

    await user.click(screen.getByRole('button', { name: 'Switch to light theme' }));
    expect(document.documentElement).toHaveClass('light');
    expect(screen.getByRole('button', { name: 'Switch to dark theme' })).toBeInTheDocument();
  });

  it('remembers the choice across visits', async () => {
    const user = userEvent.setup();
    const { unmount } = renderToggle();

    await user.click(screen.getByRole('button', { name: 'Switch to dark theme' }));
    unmount();
    renderToggle();

    expect(screen.getByRole('button', { name: 'Switch to light theme' })).toBeInTheDocument();
  });
});
