import { screen, waitFor, within } from '@testing-library/react';
import { getClientsListMockHandler } from '../../api/generated/endpoints/clients/clients.msw';
import { getRoomsListMockHandler } from '../../api/generated/endpoints/rooms/rooms.msw';
import {
  getSessionsBookMockHandler,
  getSessionsGetMockHandler,
  getSessionsListMockHandler,
  getSessionsMineMockHandler,
} from '../../api/generated/endpoints/sessions/sessions.msw';
import { getTrainersListMockHandler } from '../../api/generated/endpoints/trainers/trainers.msw';
import type { SessionSummaryResponse } from '../../api/generated/model';
import dayjs from '../../lib/dayjs';
import { signInAs } from '../../test/auth';
import { clientSummary, ids, room, session, trainerSummary } from '../../test/fixtures';
import { renderPage } from '../../test/render';
import { server } from '../../test/server';
import { SchedulePage } from './SchedulePage';

const summary = (overrides: Partial<SessionSummaryResponse> = {}): SessionSummaryResponse => {
  const { bookings: _bookings, ...rest } = session();
  return { ...rest, ...overrides };
};

describe('SchedulePage', () => {
  it('shows this week from Monday to Sunday, including Sunday sessions, for staff', async () => {
    signInAs('Receptionist');
    const sunday = dayjs().startOf('week').add(6, 'day').hour(10);
    let from = '';
    server.use(
      getTrainersListMockHandler([trainerSummary()]),
      getRoomsListMockHandler([room()]),
      getSessionsListMockHandler(({ request }) => {
        from = new URL(request.url).searchParams.get('From') ?? '';
        return [summary({ title: 'Sunday stretch', start: sunday.format(), end: sunday.add(1, 'hour').format() })];
      }),
    );

    renderPage(<SchedulePage />);

    expect(await screen.findByText('Sunday stretch')).toBeInTheDocument();
    expect(screen.getByText('Yoga studio · 1/12')).toBeInTheDocument();
    expect(dayjs(from).format('ddd')).toBe('Mon');
    const headers = document.querySelectorAll('.rbc-time-header .rbc-header');
    expect(headers[0]?.textContent).toMatch(/Mon/);
    expect(headers[6]?.textContent).toMatch(/Sun/);
  });

  it('shows a trainer only their own sessions, read-only', async () => {
    signInAs('Trainer');
    server.use(getRoomsListMockHandler([room()]), getSessionsMineMockHandler([summary({ title: 'My personal session', type: 'Individual', capacity: 1 })]));

    renderPage(<SchedulePage />);

    expect(await screen.findByRole('heading', { name: 'My schedule' })).toBeInTheDocument();
    expect(await screen.findByText('My personal session')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'New session' })).not.toBeInTheDocument();
  });

  it('refreshes the calendar after a client is booked from the session drawer', async () => {
    signInAs('Receptionist');
    let listLoads = 0;
    server.use(
      getTrainersListMockHandler([trainerSummary()]),
      getRoomsListMockHandler([room()]),
      getClientsListMockHandler([clientSummary(), clientSummary({ id: ids.otherClient, fullName: 'Franko Ivan', phone: '+380509998877' })]),
      getSessionsListMockHandler(() => {
        listLoads += 1;
        return [summary({ activeBookingCount: listLoads === 1 ? 1 : 2 })];
      }),
      getSessionsGetMockHandler(session()),
      getSessionsBookMockHandler({ clientId: ids.otherClient, bookedAt: dayjs().format(), cancelledAt: null }),
    );
    const { user } = renderPage(<SchedulePage />);

    await user.click(await screen.findByText('Evening yoga'));
    const drawer = await screen.findByRole('dialog');
    await user.type(await within(drawer).findByRole('combobox', { name: 'Client to book' }), 'Ivan');
    await user.click(await within(drawer).findByRole('option', { name: /Franko Ivan/ }));
    await user.click(within(drawer).getByRole('button', { name: 'Book' }));

    expect(await screen.findByText('Client booked')).toBeInTheDocument();
    await waitFor(() => expect(listLoads).toBe(2));
    expect(await screen.findByText('Yoga studio · 2/12')).toBeInTheDocument();
  });
});
