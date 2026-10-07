import { Center, Loader } from '@mantine/core';
import { Navigate, Outlet } from 'react-router';
import { hasAnyRole, type Role } from './roles';
import { useRoles } from './useRoles';

export const RequireRole = ({ allowed }: { allowed: readonly Role[] }) => {
  const { roles, isLoading } = useRoles();
  if (isLoading) return <Center h={200}><Loader /></Center>;
  return hasAnyRole(roles, allowed) ? <Outlet /> : <Navigate to="/forbidden" replace />;
};
