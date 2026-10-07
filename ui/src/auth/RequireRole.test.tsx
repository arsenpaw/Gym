import { screen } from '@testing-library/react';
import { ForbiddenPage } from '../pages/StatusPages';
import { signInAs } from '../test/auth';
import { renderRoute } from '../test/render';
import { RequireRole } from './RequireRole';

const routes = [
  { element: <RequireRole allowed={['Admin']} />, children: [{ path: '/reports', element: <p>Admin only</p> }] },
  { path: '/forbidden', element: <ForbiddenPage /> },
];

describe('RequireRole', () => {
  it('shows the page to an allowed role', async () => {
    signInAs('Admin');
    renderRoute(routes, '/reports');

    expect(await screen.findByText('Admin only')).toBeInTheDocument();
  });

  it('sends any other role to the no-access page', async () => {
    signInAs('Receptionist');
    const { router } = renderRoute(routes, '/reports');

    expect(await screen.findByRole('heading', { name: 'No access' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/forbidden');
  });
});
