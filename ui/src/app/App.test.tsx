import { render, screen } from '@testing-library/react';
import { getRoomsListMockHandler } from '../api/generated/endpoints/rooms/rooms.msw';
import { signInAs, testAuth } from '../test/auth';
import { room } from '../test/fixtures';
import { server } from '../test/server';
import { App } from './App';

describe('App', () => {
  afterEach(() => window.history.replaceState({}, '', '/'));

  it('opens the address Auth0 returned to after sign-in', async () => {
    signInAs('Admin');
    server.use(getRoomsListMockHandler([room()]));
    window.history.replaceState({}, '', '/rooms');

    render(<App />);

    expect(await screen.findByRole('heading', { name: 'Rooms' })).toBeInTheDocument();
    expect(window.location.pathname).toBe('/rooms');
  });

  it('shows a sign-in error instead of sending the user back to Auth0', async () => {
    testAuth.isAuthenticated = false;
    testAuth.error = new Error('Access denied: no club role assigned.');

    render(<App />);

    expect(await screen.findByRole('heading', { name: 'Sign-in failed' })).toBeInTheDocument();
    expect(screen.getByText('Access denied: no club role assigned.')).toBeInTheDocument();
    expect(testAuth.loginWithRedirect).not.toHaveBeenCalled();
  });
});
