import { ActionIcon, Alert, Badge, Button, Center, Divider, Drawer, Group, Loader, Progress, Select, Stack, Text, Title, Tooltip } from '@mantine/core';
import { modals } from '@mantine/modals';
import { notifications } from '@mantine/notifications';
import { IconUserMinus } from '@tabler/icons-react';
import { useQueryClient } from '@tanstack/react-query';
import { DataTable } from 'mantine-datatable';
import { useState } from 'react';
import { useClientsList } from '../../api/generated/endpoints/clients/clients';
import { useRoomsList } from '../../api/generated/endpoints/rooms/rooms';
import { useSessionsBook, useSessionsCancel, useSessionsCancelBooking, useSessionsGet } from '../../api/generated/endpoints/sessions/sessions';
import { useTrainersList } from '../../api/generated/endpoints/trainers/trainers';
import { problemMessage } from '../../api/problem';
import { Role, hasAnyRole } from '../../auth/roles';
import { useRoles } from '../../auth/useRoles';
import dayjs from '../../lib/dayjs';
import { formatDateTime, formatTimeRange } from '../../lib/format';
import { invalidateSessions } from './invalidateSessions';

export const SessionDrawer = ({ sessionId, onClose }: { sessionId: string | null; onClose: () => void }) => (
  <Drawer opened={sessionId !== null} onClose={onClose} position="right" size="lg" title="Session">
    {sessionId && <SessionDetails sessionId={sessionId} />}
  </Drawer>
);

const SessionDetails = ({ sessionId }: { sessionId: string }) => {
  const queryClient = useQueryClient();
  const { roles } = useRoles();
  const isStaff = hasAnyRole(roles, [Role.Admin, Role.Receptionist]);
  const session = useSessionsGet(sessionId);
  const trainers = useTrainersList({ includeInactive: true }, { query: { enabled: isStaff } });
  const rooms = useRoomsList({ includeInactive: true });
  const clients = useClientsList({ query: { enabled: isStaff } });
  const [clientToBook, setClientToBook] = useState<string | null>(null);
  const refresh = () => invalidateSessions(queryClient);
  const book = useSessionsBook({
    mutation: {
      meta: { errorTitle: 'Could not book the client' },
      onSuccess: async () => {
        setClientToBook(null);
        await refresh();
        notifications.show({ color: 'teal', message: 'Client booked' });
      },
    },
  });
  const cancelBooking = useSessionsCancelBooking({ mutation: { onSuccess: refresh } });
  const cancelSession = useSessionsCancel({
    mutation: {
      onSuccess: async () => {
        await refresh();
        notifications.show({ color: 'teal', message: 'Session cancelled' });
      },
    },
  });

  if (session.isPending) return <Center h={200}><Loader /></Center>;
  if (session.isError) return <Alert color="red" title="Could not load the session">{problemMessage(session.error)}</Alert>;

  const data = session.data;
  const clientNames = new Map((clients.data ?? []).map((c) => [c.id, c.fullName]));
  const activeBookings = data.bookings.filter((b) => !b.cancelledAt);
  const booked = new Set(activeBookings.map((b) => b.clientId));
  const editable = isStaff && data.status === 'Scheduled' && dayjs(data.start).isAfter(dayjs());
  const trainerName = trainers.data?.find((t) => t.id === data.trainerId)?.fullName;
  const roomName = rooms.data?.find((r) => r.id === data.roomId)?.name;
  const confirmCancelSession = () =>
    modals.openConfirmModal({
      title: `Cancel ${data.title}?`,
      children: 'All bookings for this session are cancelled too.',
      labels: { confirm: 'Cancel session', cancel: 'Keep it' },
      confirmProps: { color: 'red' },
      onConfirm: () => cancelSession.mutate({ id: data.id }),
    });

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={3}>{data.title}</Title>
        <Group gap="xs">
          <Badge variant="light" color={data.type === 'Group' ? 'teal' : 'violet'}>{data.type}</Badge>
          {data.status === 'Cancelled' && <Badge color="red" variant="light">Cancelled</Badge>}
        </Group>
      </Group>
      <Text>{formatTimeRange(data.start, data.end)}</Text>
      <Text c="dimmed">{[trainerName, roomName].filter(Boolean).join(' · ')}</Text>
      <div>
        <Group justify="space-between">
          <Text size="sm">Booked</Text>
          <Text size="sm" fw={600}>{data.activeBookingCount} of {data.capacity}</Text>
        </Group>
        <Progress value={(data.activeBookingCount / data.capacity) * 100} mt={4} />
      </div>
      {isStaff && (
        <>
          <Divider label="Bookings" labelPosition="left" />
          {editable && (
            <Group align="flex-end">
              <Select
                aria-label="Client to book"
                placeholder="Find a client"
                searchable
                style={{ flex: 1 }}
                data={(clients.data ?? []).filter((c) => !booked.has(c.id)).map((c) => ({ value: c.id, label: `${c.fullName} · ${c.phone}` }))}
                value={clientToBook}
                onChange={setClientToBook}
              />
              <Button
                disabled={!clientToBook || data.activeBookingCount >= data.capacity}
                loading={book.isPending}
                onClick={() => clientToBook && book.mutate({ id: data.id, data: { clientId: clientToBook } })}
              >
                Book
              </Button>
            </Group>
          )}
          <DataTable
            minHeight={100}
            idAccessor="clientId"
            records={activeBookings}
            noRecordsText="No bookings yet"
            columns={[
              { accessor: 'clientId', title: 'Client', render: (b) => clientNames.get(b.clientId) ?? 'Client' },
              { accessor: 'bookedAt', title: 'Booked', render: (b) => formatDateTime(b.bookedAt) },
              {
                accessor: 'actions',
                title: '',
                textAlign: 'right',
                hidden: !editable,
                render: (b) => (
                  <Tooltip label="Cancel booking">
                    <ActionIcon
                      variant="subtle"
                      color="red"
                      aria-label={`Cancel booking for ${clientNames.get(b.clientId) ?? b.clientId}`}
                      onClick={() => cancelBooking.mutate({ id: data.id, clientId: b.clientId })}
                    >
                      <IconUserMinus size={16} />
                    </ActionIcon>
                  </Tooltip>
                ),
              },
            ]}
          />
          {editable && (
            <Button color="red" variant="light" onClick={confirmCancelSession}>Cancel session</Button>
          )}
        </>
      )}
    </Stack>
  );
};
