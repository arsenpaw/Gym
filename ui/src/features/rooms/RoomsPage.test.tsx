import { screen, waitFor, within } from '@testing-library/react';
import { HttpResponse, http } from 'msw';
import {
  getRoomsCreateMockHandler,
  getRoomsListMockHandler,
} from '../../api/generated/endpoints/rooms/rooms.msw';
import type { RoomRequest, RoomResponse } from '../../api/generated/model';
import { signInAs } from '../../test/auth';
import { renderPage } from '../../test/render';
import { server } from '../../test/server';
import { RoomsPage } from './RoomsPage';

const yoga: RoomResponse = { id: '11111111-1111-4111-8111-111111111111', name: 'Yoga studio', capacity: 20, isActive: true };

describe('RoomsPage', () => {
  it('lists rooms from the API', async () => {
    signInAs('Receptionist');
    server.use(getRoomsListMockHandler([yoga]));

    renderPage(<RoomsPage />);

    expect(await screen.findByText('Yoga studio')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'New room' })).not.toBeInTheDocument();
  });

  it('lets an admin create a room and shows validation errors first', async () => {
    signInAs('Admin');
    let sent: RoomRequest | undefined;
    server.use(
      getRoomsListMockHandler([]),
      getRoomsCreateMockHandler(async ({ request }) => {
        sent = (await request.json()) as RoomRequest;
        return { ...yoga, id: '22222222-2222-4222-8222-222222222222', name: sent.name };
      }),
    );
    const { user } = renderPage(<RoomsPage />);

    await user.click(await screen.findByRole('button', { name: 'New room' }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Save' }));
    expect(await within(dialog).findByText('Name is required')).toBeInTheDocument();

    await user.type(within(dialog).getByLabelText(/Name/), '  Spin room ');
    await user.click(within(dialog).getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(sent).toEqual({ name: 'Spin room', capacity: 20 }));
    expect(await screen.findByText('Room created')).toBeInTheDocument();
  });

  it('shows the API problem detail when saving fails', async () => {
    signInAs('Admin');
    server.use(
      getRoomsListMockHandler([]),
      http.post('*/api/rooms', () =>
        HttpResponse.json({ title: 'Bad request', detail: 'A room named Spin room already exists.' }, { status: 400, headers: { 'Content-Type': 'application/problem+json' } }),
      ),
    );
    const { user } = renderPage(<RoomsPage />);

    await user.click(await screen.findByRole('button', { name: 'New room' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText(/Name/), 'Spin room');
    await user.click(within(dialog).getByRole('button', { name: 'Save' }));

    expect(await screen.findByText('A room named Spin room already exists.')).toBeInTheDocument();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });
});
