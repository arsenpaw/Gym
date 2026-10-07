import { Center, Loader } from '@mantine/core';
import { Navigate } from 'react-router';
import { hasAnyRole } from '../auth/roles';
import { useRoles } from '../auth/useRoles';
import { navItems } from '../layout/navigation';

export const HomeRedirect = () => {
  const { roles, isLoading } = useRoles();
  if (isLoading) return <Center h={200}><Loader /></Center>;
  const first = navItems.find((item) => hasAnyRole(roles, item.roles));
  return <Navigate to={first?.to ?? '/forbidden'} replace />;
};
