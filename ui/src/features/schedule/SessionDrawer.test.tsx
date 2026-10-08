import { screen, waitFor, within } from '@testing-library/react';
import { getClientsListMockHandler } from '../../api/generated/endpoints/clients/clients.msw';
import { getRoomsListMockHandler } from '../../api/generated/endpoints/rooms/rooms.msw';
import {
  getSessionsCancelBookingMockHandler,
  getSessionsCancelMockHandler,
  getSessionsGetMockHandler,
} from '../../api/generated/endpoints/sessions/sessions.msw';
import { getTrainersListMockHandler } from '../../api/generated/endpoints/trainers/trainers.msw';
import dayjs from '../../lib/dayjs';
import { signInAs } from '../../test/auth';
import { clientSummary, ids, room, session, trainerSummary } from '../../test/fixtures';
import { renderPage } from '../../test/render';
import { server } from '../../test/server';
import { SessionDrawer } from './SessionDrawer';

describe('SessionDrawer', () => {
  it('shows bookings with client names and cancels a booking', async () => {
    signInAs('Receptionist');
    let cancelledFor = '';
    server.use(
      getSessionsGetMockHandler(session()),
      getTrainersListMockHandler([trainerSummary()]),
      getRoomsListMockHandler([room()]),
      getClientsListMockHandler([clientSummary()]),
      getSessionsCancelBookingMockHandler(({ params }) => {
        cancelledFor = String(params.clientId);
      }),
    );
    const { user } = renderPage(<SessionDrawer sessionId={ids.session} onClose={() => {}} />);

    expect(await screen.findByText('Bondar Taras · Yoga studio')).toBeInTheDocument();
    expect(await screen.findByText('Shevchenko Olena')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Cancel booking for Shevchenko Olena' }));

    await waitFor(() => expect(cancelledFor).toBe(ids.client));
  });

  it('cancels the whole session after confirmation', async () => {
    signInAs('Admin');
    let cancelled = false;
    server.use(
      getSessionsGetMockHandler(session()),
      getTrainersListMockHandler([trainerSummary()]),
      getRoomsListMockHandler([room()]),
      getClientsListMockHandler([clientSummary()]),
      getSessionsCancelMockHandler(() => {
        cancelled = true;
      }),
    );
    const { user } = renderPage(<SessionDrawer sessionId={ids.session} onClose={() => {}} />);

    await user.click(await screen.findByRole('button', { name: 'Cancel session' }));
    const confirm = await screen.findByRole('dialog', { name: 'Cancel Evening yoga?' });
    await user.click(within(confirm).getByRole('button', { name: 'Cancel session' }));

    await waitFor(() => expect(cancelled).toBe(true));
  });

  it('is read-only for a past session and for trainers', async () => {
    signInAs('Trainer');
    const start = dayjs().subtract(2, 'hour');
    server.use(
      getSessionsGetMockHandler(session({ start: start.format(), end: start.add(1, 'hour').format() })),
      getRoomsListMockHandler([room()]),
    );

    renderPage(<SessionDrawer sessionId={ids.session} onClose={() => {}} />);

    expect(await screen.findByText('1 of 12')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Cancel session' })).not.toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'Client to book' })).not.toBeInTheDocument();
  });
});
