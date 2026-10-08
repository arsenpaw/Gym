import type { QueryClient } from '@tanstack/react-query';

export const invalidateSessions = (queryClient: QueryClient) =>
  queryClient.invalidateQueries({
    predicate: (query) => typeof query.queryKey[0] === 'string' && query.queryKey[0].startsWith('/api/sessions'),
  });
