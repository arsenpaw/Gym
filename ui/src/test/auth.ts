import type { Role } from '../auth/roles';
import { ROLES_CLAIM } from '../auth/roles';

const base64Url = (value: object) => btoa(JSON.stringify(value)).replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_');

export const fakeAccessToken = (roles: readonly Role[]) =>
  `${base64Url({ alg: 'none', typ: 'JWT' })}.${base64Url({ sub: 'auth0|test', [ROLES_CLAIM]: roles })}.signature`;

export const testAuth = {
  roles: [] as Role[],
  isAuthenticated: true,
  tokenError: undefined as unknown,
  error: undefined as Error | undefined,
  getAccessTokenSilently: vi.fn(async () => {
    if (testAuth.tokenError) throw testAuth.tokenError;
    return fakeAccessToken(testAuth.roles);
  }),
  loginWithRedirect: vi.fn(async () => {}),
  logout: vi.fn(async () => {}),
};

export const signInAs = (...roles: Role[]) => {
  testAuth.roles = roles;
  testAuth.isAuthenticated = true;
};

export const resetTestAuth = () => {
  testAuth.roles = [];
  testAuth.isAuthenticated = true;
  testAuth.tokenError = undefined;
  testAuth.error = undefined;
};
