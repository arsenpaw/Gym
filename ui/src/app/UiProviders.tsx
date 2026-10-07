import { MantineProvider } from '@mantine/core';
import { DatesProvider } from '@mantine/dates';
import { ModalsProvider } from '@mantine/modals';
import { Notifications } from '@mantine/notifications';
import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import { theme } from './theme';

type Props = { queryClient: QueryClient; children: ReactNode; env?: 'default' | 'test' };

export const UiProviders = ({ queryClient, children, env = 'default' }: Props) => (
  <MantineProvider theme={theme} defaultColorScheme="auto" env={env}>
    <DatesProvider settings={{ firstDayOfWeek: 1, consistentWeeks: true }}>
      <QueryClientProvider client={queryClient}>
        <ModalsProvider>
          <Notifications position="top-right" />
          {children}
        </ModalsProvider>
      </QueryClientProvider>
    </DatesProvider>
  </MantineProvider>
);
