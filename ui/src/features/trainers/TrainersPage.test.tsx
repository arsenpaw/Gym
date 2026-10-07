import { screen, waitFor, within } from '@testing-library/react';
import { getTrainersHireMockHandler, getTrainersListMockHandler } from '../../api/generated/endpoints/trainers/trainers.msw';
import type { TrainerRequest } from '../../api/generated/model';
import { signInAs } from '../../test/auth';
import { ids, trainerDetails, trainerSummary } from '../../test/fixtures';
import { renderRoute } from '../../test/render';
import { server } from '../../test/server';
import { TrainersPage } from './TrainersPage';

const routes = [
  { path: '/trainers', element: <TrainersPage /> },
  { path: '/trainers/:trainerId', element: <p>Trainer details page</p> },
];

describe('TrainersPage', () => {
  it('lists trainers read-only for receptionists and opens a trainer on click', async () => {
    signInAs('Receptionist');
    server.use(getTrainersListMockHandler([trainerSummary()]));
    const { user, router } = renderRoute(routes, '/trainers');

    await user.click(await screen.findByText('Bondar Taras'));

    expect(screen.queryByRole('button', { name: 'Hire trainer' })).not.toBeInTheDocument();
    await waitFor(() => expect(router.state.location.pathname).toBe(`/trainers/${ids.trainer}`));
  });

  it('lets an admin hire a trainer', async () => {
    signInAs('Admin');
    let sent: TrainerRequest | undefined;
    server.use(
      getTrainersListMockHandler([]),
      getTrainersHireMockHandler(async ({ request }) => {
        sent = (await request.json()) as TrainerRequest;
        return trainerDetails();
      }),
    );
    const { user, router } = renderRoute(routes, '/trainers');

    await user.click(await screen.findByRole('button', { name: 'Hire trainer' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText(/Last name/), 'Bondar');
    await user.type(within(dialog).getByLabelText(/First name/), 'Taras');
    await user.type(within(dialog).getByLabelText(/Specialization/), 'Yoga');
    await user.type(within(dialog).getByLabelText(/Phone/), '+380501112233');
    await user.click(within(dialog).getByRole('button', { name: 'Save' }));

    await waitFor(() =>
      expect(sent).toEqual({ firstName: 'Taras', lastName: 'Bondar', middleName: null, phone: '+380501112233', email: null, specialization: 'Yoga' }),
    );
    await waitFor(() => expect(router.state.location.pathname).toBe(`/trainers/${ids.trainer}`));
  });
});
