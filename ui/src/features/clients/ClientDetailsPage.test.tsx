import { screen, waitFor, within } from '@testing-library/react';
import { HttpResponse, http } from 'msw';
import {
  getClientsCancelMembershipMockHandler,
  getClientsCheckInMockHandler,
  getClientsGetMockHandler,
  getClientsListVisitsMockHandler,
  getClientsPurchaseMembershipMockHandler,
  getClientsUpdateMockHandler,
} from '../../api/generated/endpoints/clients/clients.msw';
import { getClientMessagesListMockHandler, getClientMessagesPreviewMockHandler } from '../../api/generated/endpoints/client-messages/client-messages.msw';
import { getMembershipPlansListMockHandler } from '../../api/generated/endpoints/membership-plans/membership-plans.msw';
import type { PurchaseMembershipRequest } from '../../api/generated/model';
import { today } from '../../lib/dates';
import dayjs from '../../lib/dayjs';
import { signInAs } from '../../test/auth';
import { clientDetails, ids, plan, visit as visitFixture, visitPage } from '../../test/fixtures';
import { renderRoute } from '../../test/render';
import { server } from '../../test/server';
import { ClientDetailsPage } from './ClientDetailsPage';

const visit = visitFixture();

const renderDetails = () => renderRoute([{ path: '/clients/:clientId', element: <ClientDetailsPage /> }], `/clients/${ids.client}`);

describe('ClientDetailsPage', () => {
  beforeEach(() => {
    signInAs('Receptionist');
    server.use(
      getClientMessagesPreviewMockHandler({ template: 'ExpiryReminder', recipient: 'olena@example.com', subject: 'Your membership expires soon', html: '<p>Hi</p>' }),
      getClientMessagesListMockHandler([]),
    );
  });

  it('shows the contacts, current membership and membership history', async () => {
    server.use(getClientsGetMockHandler(clientDetails()), getClientsListVisitsMockHandler(visitPage([visit])));

    renderDetails();

    expect(await screen.findByRole('heading', { name: 'Shevchenko Olena' })).toBeInTheDocument();
    expect(screen.getByLabelText('Email')).toHaveTextContent('olena@example.com');
    expect(screen.getByLabelText('Phone')).toHaveTextContent('+380671234567');
    expect(screen.getByText(/Monthly · valid .* · unlimited visits/)).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Memberships (1)' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByText('3 / unlimited')).toBeInTheDocument();
  });

  it('leaves the phone out when the client has none', async () => {
    server.use(getClientsGetMockHandler(clientDetails({ phone: null })), getClientsListVisitsMockHandler(visitPage()));

    renderDetails();

    expect(await screen.findByLabelText('Email')).toBeInTheDocument();
    expect(screen.queryByLabelText('Phone')).not.toBeInTheDocument();
  });

  it('shows visits in their own tab and pages through them on the server', async () => {
    const requested: string[] = [];
    const firstPage = Array.from({ length: 10 }, (_, day) =>
      visitFixture({ id: `99999999-9999-4999-8999-0000000000${day}0`, checkedInAt: dayjs().subtract(day + 1, 'day').hour(9).minute(30).format() }),
    );
    const secondPageVisit = visitFixture({ id: '99999999-9999-4999-8999-999999999999', checkedInAt: dayjs().subtract(20, 'day').hour(18).minute(5).format() });
    server.use(
      getClientsGetMockHandler(clientDetails()),
      getClientsListVisitsMockHandler(({ request }) => {
        const page = new URL(request.url).searchParams.get('Page') ?? '1';
        requested.push(page);
        return page === '2' ? visitPage([secondPageVisit], 11) : visitPage(firstPage, 11);
      }),
    );
    const { user } = renderDetails();

    await user.click(await screen.findByRole('tab', { name: 'Visits (11)' }));
    expect(await screen.findByText(dayjs(firstPage[0].checkedInAt).format('D MMM YYYY, HH:mm'))).toBeInTheDocument();
    expect(screen.getByText('1–10 of 11')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: '2' }));

    expect(await screen.findByText(dayjs(secondPageVisit.checkedInAt).format('D MMM YYYY, HH:mm'))).toBeInTheDocument();
    expect(requested).toContain('2');
  });

  it('shows the email preview in the Messages tab', async () => {
    server.use(getClientsGetMockHandler(clientDetails()), getClientsListVisitsMockHandler(visitPage()));
    const { user } = renderDetails();

    await user.click(await screen.findByRole('tab', { name: 'Messages' }));

    expect(await screen.findByTitle('Email preview')).toHaveAttribute('srcdoc', '<p>Hi</p>');
  });

  it('checks the client in and reloads the visits', async () => {
    let visitLoads = 0;
    server.use(
      getClientsGetMockHandler(clientDetails()),
      getClientsListVisitsMockHandler(() => {
        visitLoads += 1;
        return visitLoads === 1 ? visitPage() : visitPage([visit]);
      }),
      getClientsCheckInMockHandler(visit),
    );
    const { user } = renderDetails();

    await user.click(await screen.findByRole('button', { name: 'Check in' }));

    expect(await screen.findByText('Visit recorded')).toBeInTheDocument();
    await waitFor(() => expect(visitLoads).toBe(2));
  });

  it('explains why a check-in was refused', async () => {
    server.use(
      getClientsGetMockHandler(clientDetails({ activeMembership: null })),
      getClientsListVisitsMockHandler(visitPage()),
      http.post('*/api/clients/:id/visits', () =>
        HttpResponse.json({ title: 'Invalid request', detail: 'The client has no active membership today.' }, { status: 400 }),
      ),
    );
    const { user } = renderDetails();

    await user.click(await screen.findByRole('button', { name: 'Check in' }));

    expect(await screen.findByText('Check-in failed')).toBeInTheDocument();
    expect(screen.getByText('The client has no active membership today.')).toBeInTheDocument();
  });

  it('sells a membership starting today unless another start date is chosen', async () => {
    let sent: PurchaseMembershipRequest | undefined;
    server.use(
      getClientsGetMockHandler(clientDetails()),
      getClientsListVisitsMockHandler(visitPage()),
      getMembershipPlansListMockHandler([plan(), plan({ id: '22222222-2222-4222-8222-333333333333', name: 'Yearly', price: 9000, validityDays: 365 })]),
      getClientsPurchaseMembershipMockHandler(async ({ request }) => {
        sent = (await request.json()) as PurchaseMembershipRequest;
        return clientDetails().memberships[0];
      }),
    );
    const { user } = renderDetails();

    await user.click(await screen.findByRole('button', { name: 'Sell membership' }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Sell' }));
    expect(await within(dialog).findByText('Choose a plan')).toBeInTheDocument();

    await user.click(within(dialog).getByRole('combobox', { name: /Plan/ }));
    await user.click(await within(dialog).findByRole('option', { name: /Yearly/ }));
    expect(within(dialog).getByText('Valid for 365 days · unlimited visits')).toBeInTheDocument();
    await user.click(within(dialog).getByText('Cash'));
    await user.click(within(dialog).getByRole('button', { name: 'Sell' }));

    await waitFor(() =>
      expect(sent).toEqual({ planId: '22222222-2222-4222-8222-333333333333', startsOn: today(), paymentMethod: 'Cash' }),
    );
    expect(await screen.findByText('Membership sold')).toBeInTheDocument();
  });

  it('cancels a membership after confirmation', async () => {
    let cancelled = false;
    server.use(
      getClientsGetMockHandler(clientDetails()),
      getClientsListVisitsMockHandler(visitPage()),
      getClientsCancelMembershipMockHandler(() => {
        cancelled = true;
      }),
    );
    const { user } = renderDetails();

    await user.click(await screen.findByRole('button', { name: 'Cancel Monthly' }));
    await user.click(await screen.findByRole('button', { name: 'Cancel membership' }));

    await waitFor(() => expect(cancelled).toBe(true));
  });

  it('never saves an impossible date of birth as the old one', async () => {
    let updates = 0;
    server.use(
      getClientsGetMockHandler(clientDetails()),
      getClientsListVisitsMockHandler(visitPage()),
      getClientsUpdateMockHandler(() => {
        updates += 1;
        return clientDetails();
      }),
    );
    const { user } = renderDetails();

    await user.click(await screen.findByRole('button', { name: 'Edit' }));
    const dialog = await screen.findByRole('dialog');
    const dateOfBirth = within(dialog).getByLabelText(/Date of birth/);
    await user.clear(dateOfBirth);
    await user.type(dateOfBirth, '1995-02-30');
    await user.click(within(dialog).getByRole('button', { name: 'Save' }));

    expect(await within(dialog).findByText('Enter a full date of birth')).toBeInTheDocument();
    expect(updates).toBe(0);
  });

  it('never sells a membership starting today when an impossible start date was typed', async () => {
    let sales = 0;
    server.use(
      getClientsGetMockHandler(clientDetails()),
      getClientsListVisitsMockHandler(visitPage()),
      getMembershipPlansListMockHandler([plan()]),
      getClientsPurchaseMembershipMockHandler(() => {
        sales += 1;
        return clientDetails().memberships[0];
      }),
    );
    const { user } = renderDetails();

    await user.click(await screen.findByRole('button', { name: 'Sell membership' }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('combobox', { name: /Plan/ }));
    await user.click(await within(dialog).findByRole('option', { name: /Monthly/ }));
    const startsOn = within(dialog).getByLabelText(/Starts on/);
    await user.clear(startsOn);
    await user.type(startsOn, '2026-11-31');
    await user.click(within(dialog).getByRole('button', { name: 'Sell' }));

    expect(await within(dialog).findByText('Enter a full start date')).toBeInTheDocument();
    expect(sales).toBe(0);
  });
});
