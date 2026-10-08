import { screen, waitFor, within } from '@testing-library/react';
import { getClientsListMockHandler } from '../../api/generated/endpoints/clients/clients.msw';
import {
  getTrainersAssignClientMockHandler,
  getTrainersGetMockHandler,
  getTrainersLinkIdentityMockHandler,
  getTrainersSetWorkingHoursMockHandler,
} from '../../api/generated/endpoints/trainers/trainers.msw';
import type { LinkIdentityRequest, WorkingHoursRequest } from '../../api/generated/model';
import { signInAs } from '../../test/auth';
import { clientSummary, ids, trainerDetails } from '../../test/fixtures';
import { renderRoute } from '../../test/render';
import { server } from '../../test/server';
import { TrainerDetailsPage } from './TrainerDetailsPage';

const renderDetails = () => renderRoute([{ path: '/trainers/:trainerId', element: <TrainerDetailsPage /> }], `/trainers/${ids.trainer}`);

describe('TrainerDetailsPage', () => {
  it('shows working hours and hides admin actions from receptionists', async () => {
    signInAs('Receptionist');
    server.use(getTrainersGetMockHandler(trainerDetails()));

    renderDetails();

    expect(await screen.findByRole('heading', { name: 'Bondar Taras' })).toBeInTheDocument();
    expect(screen.getByText('09:00 – 17:00')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Edit hours' })).not.toBeInTheDocument();
  });

  it('saves working hours with seconds and checks that end is after start', async () => {
    signInAs('Admin');
    let sent: WorkingHoursRequest[] | undefined;
    server.use(
      getTrainersGetMockHandler(trainerDetails()),
      getClientsListMockHandler([]),
      getTrainersSetWorkingHoursMockHandler(async ({ request }) => {
        sent = (await request.json()) as WorkingHoursRequest[];
        return trainerDetails();
      }),
    );
    const { user } = renderDetails();

    await user.click(await screen.findByRole('button', { name: 'Edit hours' }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Add hours' }));
    const end = within(dialog).getByLabelText('End 2');
    await user.clear(end);
    await user.type(end, '08:00');
    await user.click(within(dialog).getByRole('button', { name: 'Save' }));
    expect(await within(dialog).findByText('End must be after start')).toBeInTheDocument();

    await user.clear(end);
    await user.type(end, '20:00');
    await user.click(within(dialog).getByRole('button', { name: 'Save' }));

    await waitFor(() =>
      expect(sent).toEqual([
        { day: 'Monday', start: '09:00:00', end: '17:00:00' },
        { day: 'Monday', start: '09:00:00', end: '20:00:00' },
      ]),
    );
  });

  it('assigns a client who is not assigned yet', async () => {
    signInAs('Admin');
    let assigned = '';
    server.use(
      getTrainersGetMockHandler(trainerDetails()),
      getClientsListMockHandler([clientSummary()]),
      getTrainersAssignClientMockHandler(({ params }) => {
        assigned = String(params.clientId);
      }),
    );
    const { user } = renderDetails();

    await user.type(await screen.findByRole('combobox', { name: 'Client to assign' }), 'Olena');
    await user.click(await screen.findByRole('option', { name: /Shevchenko Olena/ }));
    await user.click(screen.getByRole('button', { name: 'Assign' }));

    await waitFor(() => expect(assigned).toBe(ids.client));
  });

  it('links the trainer to an Auth0 login', async () => {
    signInAs('Admin');
    let sent: LinkIdentityRequest | undefined;
    server.use(
      getTrainersGetMockHandler(trainerDetails()),
      getClientsListMockHandler([]),
      getTrainersLinkIdentityMockHandler(async ({ request }) => {
        sent = (await request.json()) as LinkIdentityRequest;
      }),
    );
    const { user } = renderDetails();

    await user.type(await screen.findByLabelText(/Auth0 user id/), ' auth0|taras ');
    await user.click(screen.getByRole('button', { name: 'Link login' }));

    await waitFor(() => expect(sent).toEqual({ identityUserId: 'auth0|taras' }));
    expect(await screen.findByText('Login linked')).toBeInTheDocument();
  });
});
