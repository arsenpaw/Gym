import { useAuth0 } from '@auth0/auth0-react';
import { useLayoutEffect, type ReactNode } from 'react';
import { setAccessTokenProvider } from '../api/http';

const loginErrors = ['login_required', 'consent_required', 'missing_refresh_token', 'invalid_grant'];

const needsLogin = (error: unknown) =>
  typeof error === 'object' && error !== null && 'error' in error && loginErrors.includes(String(error.error));

export const AccessTokenBridge = ({ children }: { children: ReactNode }) => {
  const { getAccessTokenSilently, isAuthenticated, loginWithRedirect } = useAuth0();

  useLayoutEffect(() => {
    if (!isAuthenticated) return;
    setAccessTokenProvider(async () => {
      try {
        return await getAccessTokenSilently();
      } catch (error) {
        if (needsLogin(error)) await loginWithRedirect({ appState: { returnTo: window.location.pathname } });
        throw error;
      }
    });
    return () => setAccessTokenProvider(null);
  }, [getAccessTokenSilently, isAuthenticated, loginWithRedirect]);

  return children;
};
