import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import '../lib/dayjs';
import { resetTestAuth, testAuth } from './auth';
import { server } from './server';

vi.mock('@auth0/auth0-react', () => ({
  Auth0Provider: ({ children }: { children: unknown }) => children,
  useAuth0: () => ({
    isAuthenticated: testAuth.isAuthenticated,
    isLoading: false,
    error: testAuth.error,
    user: { name: 'Test User', email: 'test@example.com' },
    getAccessTokenSilently: testAuth.getAccessTokenSilently,
    loginWithRedirect: testAuth.loginWithRedirect,
    logout: testAuth.logout,
  }),
  withAuthenticationRequired: (component: unknown) => component,
}));

window.HTMLElement.prototype.scrollIntoView = () => {};
document.elementFromPoint = () => null;
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: (query: string) => ({
    matches: query.trim() === '',
    media: query,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => false,
  }),
});
window.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
};

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => {
  cleanup();
  server.resetHandlers();
  resetTestAuth();
  vi.clearAllMocks();
});
afterAll(() => server.close());
