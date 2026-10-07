import { screen, waitFor } from '@testing-library/react';
import { HttpResponse, http } from 'msw';
import { getClientsCheckInMockHandler, getClientsListMockHandler } from '../../api/generated/endpoints/clients/clients.msw';
import dayjs from '../../lib/dayjs';
import { signInAs } from '../../test/auth';
import { clientSummary, ids } from '../../test/fixtures';
import { renderPage } from '../../test/render';
import { server } from '../../test/server';
import { CheckInPage } from './CheckInPage';

describe('CheckInPage', () => {
  beforeEach(() => signInAs('Receptionist'));

  it('finds a client by phone and checks them in', async () => {
    const checkedInAt = dayjs().hour(9).minute(15).format();
    let checkedInClient = '';
    server.use(
      getClientsListMockHandler([clientSummary(), clientSummary({ id: ids.otherClient, fullName: 'Franko Ivan', phone: '+380509998877' })]),
      getClientsCheckInMockHandler(({ params }) => {
        checkedInClient = String(params.id);
        return { id: '99999999-9999-4999-8999-999999999999', clientId: ids.otherClient, membershipId: ids.membership, checkedInAt };
      }),
    );
    const { user } = renderPage(<CheckInPage />);

    await waitFor(() => expect(screen.getByRole('combobox', { name: 'Client' })).not.toBeDisabled());
    await user.type(screen.getByRole('combobox', { name: 'Client' }), '0509998');
    await user.click(await screen.findByRole('option', { name: /Franko Ivan/ }));
    await user.click(screen.getByRole('button', { name: 'Check in' }));

    expect(await screen.findByText('Checked in at 09:15')).toBeInTheDocument();
    expect(checkedInClient).toBe(ids.otherClient);
  });

  it('warns before check-in when the client has no membership and shows the API reason', async () => {
    server.use(
      getClientsListMockHandler([clientSummary({ activeMembership: null })]),
      http.post('*/api/clients/:id/visits', () =>
        HttpResponse.json({ title: 'Invalid request', detail: 'The client has no active membership today.' }, { status: 400 }),
      ),
    );
    const { user } = renderPage(<CheckInPage />);

    await user.type(await screen.findByRole('combobox', { name: 'Client' }), 'Olena');
    await user.click(await screen.findByRole('option', { name: /Shevchenko Olena/ }));
    expect(screen.getByText(/No active membership/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Check in' }));

    expect(await screen.findByText('The client has no active membership today.')).toBeInTheDocument();
  });
});
