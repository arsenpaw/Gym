import { screen, waitFor } from '@testing-library/react';
import { getClientsListMockHandler } from '../api/generated/endpoints/clients/clients.msw';
import { getRoomsListMockHandler } from '../api/generated/endpoints/rooms/rooms.msw';
import { getSessionsMineMockHandler } from '../api/generated/endpoints/sessions/sessions.msw';
import { signInAs } from '../test/auth';
import { renderRoute } from '../test/render';
import { server } from '../test/server';
import { routes } from './routes';

describe('routes', () => {
  beforeEach(() => {
    server.use(getClientsListMockHandler([]), getRoomsListMockHandler([]), getSessionsMineMockHandler([]));
  });

  it('starts a receptionist at check-in', async () => {
    signInAs('Receptionist');
    const { router } = renderRoute(routes, '/');

    await waitFor(() => expect(router.state.location.pathname).toBe('/check-in'));
  });

  it('starts a trainer at the schedule', async () => {
    signInAs('Trainer');
    const { router } = renderRoute(routes, '/');

    await waitFor(() => expect(router.state.location.pathname).toBe('/schedule'));
  });

  it('shows no access to a signed-in user without a club role', async () => {
    signInAs();
    renderRoute(routes, '/');

    expect(await screen.findByRole('heading', { name: 'No access' })).toBeInTheDocument();
  });

  it('shows not found for an unknown address', async () => {
    signInAs('Admin');
    renderRoute(routes, '/nope');

    expect(await screen.findByRole('heading', { name: 'Page not found' })).toBeInTheDocument();
  });
});
