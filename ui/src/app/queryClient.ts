import { notifications } from '@mantine/notifications';
import { MutationCache, QueryClient } from '@tanstack/react-query';
import { problemMessage } from '../api/problem';

declare module '@tanstack/react-query' {
  interface Register {
    mutationMeta: { errorTitle?: string };
  }
}

export const createQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      queries: { retry: 1, refetchOnWindowFocus: false },
    },
    mutationCache: new MutationCache({
      onError: (error, _variables, _context, mutation) => {
        notifications.show({
          color: 'red',
          title: mutation.meta?.errorTitle ?? 'Could not save changes',
          message: problemMessage(error),
        });
      },
    }),
  });
