import { useAuth0 } from '@auth0/auth0-react';
import { useQuery } from '@tanstack/react-query';
import { readRoles } from './roles';

export const rolesQueryKey = ['auth', 'roles'] as const;

export const useRoles = () => {
  const { getAccessTokenSilently, isAuthenticated } = useAuth0();
  const query = useQuery({
    queryKey: rolesQueryKey,
    queryFn: async () => readRoles(await getAccessTokenSilently()),
    enabled: isAuthenticated,
    staleTime: Infinity,
  });
  return { roles: query.data ?? [], isLoading: query.isPending };
};
