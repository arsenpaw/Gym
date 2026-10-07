import { createTheme } from '@mantine/core';

const fontFamily = "'Inter Variable', system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif";

export const theme = createTheme({
  primaryColor: 'teal',
  defaultRadius: 'md',
  fontFamily,
  headings: { fontFamily, fontWeight: '650' },
  cursorType: 'pointer',
});
