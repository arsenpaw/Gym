import { screen, waitFor, within } from '@testing-library/react';
import { getClientsListMockHandler, getClientsRegisterMockHandler } from '../../api/generated/endpoints/clients/clients.msw';
import type { ClientRequest } from '../../api/generated/model';
import dayjs from '../../lib/dayjs';
import { signInAs } from '../../test/auth';
import { clientDetails, clientSummary, ids } from '../../test/fixtures';
import { renderRoute } from '../../test/render';
import { server } from '../../test/server';
import { ClientsPage } from './ClientsPage';

const routes = [
  { path: '/clients', element: <ClientsPage /> },
  { path: '/clients/:clientId', element: <p>Client details page</p> },
];

describe('ClientsPage', () => {
  it('lists clients with their membership state', async () => {
    signInAs('Receptionist');
    server.use(
      getClientsListMockHandler([
        clientSummary(),
        clientSummary({ id: ids.otherClient, fullName: 'Franko Ivan', email: 'ivan@example.com', phone: null, activeMembership: null }),
      ]),
    );

    renderRoute(routes, '/clients');

    expect(await screen.findByText('Shevchenko Olena')).toBeInTheDocument();
    expect(screen.getByText(/Active until/)).toBeInTheDocument();
    expect(screen.getByText('No membership')).toBeInTheDocument();
    expect(screen.getByText('ivan@example.com')).toBeInTheDocument();
  });

  it('filters by email', async () => {
    signInAs('Receptionist');
    server.use(getClientsListMockHandler([clientSummary(), clientSummary({ id: ids.otherClient, fullName: 'Franko Ivan', email: 'ivan@example.com' })]));
    const { user } = renderRoute(routes, '/clients');

    await screen.findByText('Franko Ivan');
    await user.type(screen.getByRole('textbox', { name: 'Search clients' }), 'IVAN@');

    await waitFor(() => expect(screen.queryByText('Shevchenko Olena')).not.toBeInTheDocument());
    expect(screen.getByText('Franko Ivan')).toBeInTheDocument();
  });

  it('filters by phone digits ignoring spaces', async () => {
    signInAs('Receptionist');
    server.use(getClientsListMockHandler([clientSummary(), clientSummary({ id: ids.otherClient, fullName: 'Franko Ivan', email: 'ivan@example.com', phone: '+380509998877' })]));
    const { user } = renderRoute(routes, '/clients');

    await screen.findByText('Franko Ivan');
    await user.type(screen.getByRole('textbox', { name: 'Search clients' }), '050 999');

    await waitFor(() => expect(screen.queryByText('Shevchenko Olena')).not.toBeInTheDocument());
    expect(screen.getByText('Franko Ivan')).toBeInTheDocument();
  });

  it('registers a client with an email, sends empty optional fields as null and opens the new client', async () => {
    signInAs('Receptionist');
    let sent: ClientRequest | undefined;
    server.use(
      getClientsListMockHandler([]),
      getClientsRegisterMockHandler(async ({ request }) => {
        sent = (await request.json()) as ClientRequest;
        return clientDetails();
      }),
    );
    const { user, router } = renderRoute(routes, '/clients');

    await user.click(await screen.findByRole('button', { name: 'Register client' }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Save' }));
    expect(await within(dialog).findByText('Enter a full date of birth')).toBeInTheDocument();
    expect(within(dialog).getByText('Email is required')).toBeInTheDocument();
    expect(within(dialog).queryByText('Phone is required')).not.toBeInTheDocument();

    await user.type(within(dialog).getByLabelText(/Last name/), 'Shevchenko');
    await user.type(within(dialog).getByLabelText(/First name/), 'Olena');
    await user.type(within(dialog).getByLabelText(/Date of birth/), '1995-03-14');
    await user.type(within(dialog).getByLabelText(/Email/), 'olena@example.com');
    await user.click(within(dialog).getByRole('button', { name: 'Save' }));

    await waitFor(() =>
      expect(sent).toEqual({
        firstName: 'Olena',
        lastName: 'Shevchenko',
        middleName: null,
        dateOfBirth: '1995-03-14',
        email: 'olena@example.com',
        phone: null,
      }),
    );
    await waitFor(() => expect(router.state.location.pathname).toBe(`/clients/${ids.client}`));
  });

  it('rejects a date of birth in the future', async () => {
    signInAs('Receptionist');
    server.use(getClientsListMockHandler([]));
    const { user } = renderRoute(routes, '/clients');

    await user.click(await screen.findByRole('button', { name: 'Register client' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText(/Date of birth/), dayjs().add(1, 'day').format('YYYY-MM-DD'));
    await user.click(within(dialog).getByRole('button', { name: 'Save' }));

    expect(await within(dialog).findByText('Date of birth cannot be in the future')).toBeInTheDocument();
  });
});
