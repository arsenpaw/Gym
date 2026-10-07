import { jwtDecode } from 'jwt-decode';

export const ROLES_CLAIM = 'https://fitnessclub/roles';

export const Role = {
  Admin: 'Admin',
  Receptionist: 'Receptionist',
  Trainer: 'Trainer',
} as const;

export type Role = (typeof Role)[keyof typeof Role];

const knownRoles = Object.values(Role) as string[];

export const readRoles = (accessToken: string | undefined): Role[] => {
  if (!accessToken) return [];
  try {
    const claim = jwtDecode<Record<string, unknown>>(accessToken)[ROLES_CLAIM];
    return Array.isArray(claim) ? claim.filter((role): role is Role => knownRoles.includes(role)) : [];
  } catch {
    return [];
  }
};

export const hasAnyRole = (roles: readonly Role[], allowed: readonly Role[]) =>
  allowed.some((role) => roles.includes(role));
