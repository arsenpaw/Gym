import { screen, waitFor, within } from '@testing-library/react';
import { getRoomsListMockHandler } from '../../api/generated/endpoints/rooms/rooms.msw';
import { getSessionsScheduleMockHandler } from '../../api/generated/endpoints/sessions/sessions.msw';
import { getTrainersListMockHandler } from '../../api/generated/endpoints/trainers/trainers.msw';
import type { ScheduleSessionRequest } from '../../api/generated/model';
import dayjs from '../../lib/dayjs';
import { signInAs } from '../../test/auth';
import { ids, room, session, trainerSummary } from '../../test/fixtures';
import { renderPage } from '../../test/render';
import { server } from '../../test/server';
import { ScheduleSessionModal } from './ScheduleSessionModal';

const tomorrow = dayjs().add(1, 'day').format('YYYY-MM-DD');

const fillCommonFields = async (user: ReturnType<typeof renderPage>['user'], dialog: HTMLElement) => {
  await user.type(within(dialog).getByLabelText(/Title/), 'Morning yoga');
  await user.click(within(dialog).getByRole('combobox', { name: /Trainer/ }));
  await user.click(await within(dialog).findByRole('option', { name: /Bondar Taras/ }));
  await user.click(within(dialog).getByRole('combobox', { name: /Room/ }));
  await user.click(await within(dialog).findByRole('option', { name: /Yoga studio/ }));
};

describe('ScheduleSessionModal', () => {
  beforeEach(() => {
    signInAs('Receptionist');
    server.use(getTrainersListMockHandler([trainerSummary()]), getRoomsListMockHandler([room()]));
  });

  it('sends start and end as local time with offset, from the chosen slot and duration', async () => {
    let sent: ScheduleSessionRequest | undefined;
    server.use(
      getSessionsScheduleMockHandler(async ({ request }) => {
        sent = (await request.json()) as ScheduleSessionRequest;
        return session();
      }),
    );
    const { user } = renderPage(<ScheduleSessionModal slot={{ date: tomorrow, startTime: '07:30' }} onClose={() => {}} />);
    const dialog = await screen.findByRole('dialog');

    await fillCommonFields(user, dialog);
    await user.click(within(dialog).getByRole('combobox', { name: /Duration/ }));
    await user.click(await within(dialog).findByRole('option', { name: '1 h 30 min' }));
    await user.clear(within(dialog).getByLabelText(/Places/));
    await user.type(within(dialog).getByLabelText(/Places/), '15');
    await user.click(within(dialog).getByRole('button', { name: 'Schedule' }));

    await waitFor(() => expect(sent).toBeDefined());
    expect(sent).toEqual({
      title: 'Morning yoga',
      type: 'Group',
      trainerId: ids.trainer,
      roomId: ids.room,
      start: dayjs(`${tomorrow}T07:30`).format(),
      end: dayjs(`${tomorrow}T09:00`).format(),
      capacity: 15,
    });
    expect(sent?.start).toMatch(/[+-]\d{2}:\d{2}$/);
  });

  it('books an individual session for exactly one place', async () => {
    let sent: ScheduleSessionRequest | undefined;
    server.use(
      getSessionsScheduleMockHandler(async ({ request }) => {
        sent = (await request.json()) as ScheduleSessionRequest;
        return session();
      }),
    );
    const { user } = renderPage(<ScheduleSessionModal slot={{ date: tomorrow, startTime: '10:00' }} onClose={() => {}} />);
    const dialog = await screen.findByRole('dialog');

    await user.click(within(dialog).getByText('Individual'));
    expect(within(dialog).queryByLabelText(/Places/)).not.toBeInTheDocument();
    await fillCommonFields(user, dialog);
    await user.click(within(dialog).getByRole('button', { name: 'Schedule' }));

    await waitFor(() => expect(sent).toMatchObject({ type: 'Individual', capacity: 1 }));
  });

  it('does not let a session start in the past', async () => {
    const yesterday = dayjs().subtract(1, 'day').format('YYYY-MM-DD');
    const { user } = renderPage(<ScheduleSessionModal slot={{ date: yesterday, startTime: '10:00' }} onClose={() => {}} />);
    const dialog = await screen.findByRole('dialog');

    await fillCommonFields(user, dialog);
    await user.click(within(dialog).getByRole('button', { name: 'Schedule' }));

    expect(await within(dialog).findByText('The session must start in the future')).toBeInTheDocument();
  });
});
