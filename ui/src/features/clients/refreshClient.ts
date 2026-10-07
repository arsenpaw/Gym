import type { QueryClient } from '@tanstack/react-query';
import {
  getClientsGetQueryKey,
  getClientsListQueryKey,
  getClientsListVisitsQueryKey,
} from '../../api/generated/endpoints/clients/clients';

export const refreshClient = (queryClient: QueryClient, clientId: string) =>
  Promise.all([
    queryClient.invalidateQueries({ queryKey: getClientsGetQueryKey(clientId) }),
    queryClient.invalidateQueries({ queryKey: getClientsListVisitsQueryKey(clientId) }),
    queryClient.invalidateQueries({ queryKey: getClientsListQueryKey() }),
  ]);
