import { screen, within } from '@testing-library/react';
import { HttpResponse, http } from 'msw';
import type { ReactElement } from 'react';
import { getClientsGetMockHandler, getClientsListMockHandler, getClientsListVisitsMockHandler } from '../api/generated/endpoints/clients/clients.msw';
import { getRoomsListMockHandler } from '../api/generated/endpoints/rooms/rooms.msw';
import { getTrainersListMockHandler } from '../api/generated/endpoints/trainers/trainers.msw';
import type { Role } from '../auth/roles';
import { CheckInPage } from '../features/check-in/CheckInPage';
import { ClientDetailsPage } from '../features/clients/ClientDetailsPage';
import { ClientsPage } from '../features/clients/ClientsPage';
import { NotificationsPage } from '../features/notifications/NotificationsPage';
import { PlansPage } from '../features/plans/PlansPage';
import { ReportsPage } from '../features/reports/ReportsPage';
import { RoomsPage } from '../features/rooms/RoomsPage';
import { SchedulePage } from '../features/schedule/SchedulePage';
import { TrainersPage } from '../features/trainers/TrainersPage';
import { signInAs } from '../test/auth';
import { clientDetails, ids } from '../test/fixtures';
import { renderRoute } from '../test/render';
import { server } from '../test/server';

const serverError = (path: string) =>
  http.get(path, () => HttpResponse.json({ title: 'Server error', detail: 'An unexpected error occurred.' }, { status: 500 }));

type Case = { name: string; role: Role; path: string; route: string; element: ReactElement; failing: string; others?: Parameters<typeof server.use>; title: string };

const cases: Case[] = [
  { name: 'clients list', role: 'Receptionist', path: '/clients', route: '/clients', element: <ClientsPage />, failing: '*/api/clients', title: 'Could not load clients' },
  { name: 'check-in client search', role: 'Receptionist', path: '/check-in', route: '/check-in', element: <CheckInPage />, failing: '*/api/clients', title: 'Could not load clients' },
  { name: 'membership plans', role: 'Admin', path: '/plans', route: '/plans', element: <PlansPage />, failing: '*/api/membership-plans', title: 'Could not load plans' },
  { name: 'rooms', role: 'Admin', path: '/rooms', route: '/rooms', element: <RoomsPage />, failing: '*/api/rooms', title: 'Could not load rooms' },
  { name: 'trainers', role: 'Admin', path: '/trainers', route: '/trainers', element: <TrainersPage />, failing: '*/api/trainers', title: 'Could not load trainers' },
  {
    name: 'notifications',
    role: 'Admin',
    path: '/notifications',
    route: '/notifications',
    element: <NotificationsPage />,
    failing: '*/api/notifications',
    others: [getClientsListMockHandler([])],
    title: 'Could not load notifications',
  },
  {
    name: 'schedule',
    role: 'Receptionist',
    path: '/schedule',
    route: '/schedule',
    element: <SchedulePage />,
    failing: '*/api/sessions',
    others: [getTrainersListMockHandler([]), getRoomsListMockHandler([])],
    title: 'Could not load sessions',
  },
  { name: 'client activity report', role: 'Admin', path: '/reports', route: '/reports', element: <ReportsPage />, failing: '*/api/reports/client-activity', title: 'Could not load the report' },
  { name: 'revenue report', role: 'Admin', path: '/reports', route: '/reports?tab=revenue', element: <ReportsPage />, failing: '*/api/reports/revenue', title: 'Could not load the report' },
  { name: 'load report', role: 'Admin', path: '/reports', route: '/reports?tab=load', element: <ReportsPage />, failing: '*/api/reports/load', title: 'Could not load the report' },
  {
    name: 'client visits',
    role: 'Receptionist',
    path: '/clients/:clientId',
    route: `/clients/${ids.client}`,
    element: <ClientDetailsPage />,
    failing: '*/api/clients/:id/visits',
    others: [getClientsGetMockHandler(clientDetails())],
    title: 'Could not load visits',
  },
];

describe('a failed load is shown as an error, never as empty data', () => {
  it.each(cases)('$name', async ({ role, path, route, element, failing, others = [], title }) => {
    signInAs(role);
    server.use(serverError(failing), ...others);

    renderRoute([{ path, element }], route);

    const alert = await screen.findByRole('alert');
    expect(within(alert).getByText(title)).toBeInTheDocument();
    expect(within(alert).getByText('An unexpected error occurred.')).toBeInTheDocument();
  });

  it('plans for sale in the membership dialog', async () => {
    signInAs('Receptionist');
    server.use(getClientsGetMockHandler(clientDetails()), getClientsListVisitsMockHandler([]), serverError('*/api/membership-plans'));
    const { user } = renderRoute([{ path: '/clients/:clientId', element: <ClientDetailsPage /> }], `/clients/${ids.client}`);

    await user.click(await screen.findByRole('button', { name: 'Sell membership' }));
    const dialog = await screen.findByRole('dialog');

    expect(await within(dialog).findByText('Could not load plans')).toBeInTheDocument();
  });
});
