import { ActionIcon, Badge, Button, Group, Stack, Switch, Title, Tooltip } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { modals } from '@mantine/modals';
import { IconPencil, IconPlayerPause, IconPlayerPlay, IconPlus } from '@tabler/icons-react';
import { useQueryClient } from '@tanstack/react-query';
import { DataTable } from 'mantine-datatable';
import { useState } from 'react';
import {
  getRoomsListQueryKey,
  useRoomsActivate,
  useRoomsDeactivate,
  useRoomsList,
} from '../../api/generated/endpoints/rooms/rooms';
import type { RoomResponse } from '../../api/generated/model';
import { Role, hasAnyRole } from '../../auth/roles';
import { useRoles } from '../../auth/useRoles';
import { RoomFormModal } from './RoomFormModal';

export const RoomsPage = () => {
  const { roles } = useRoles();
  const canEdit = hasAnyRole(roles, [Role.Admin]);
  const [includeInactive, setIncludeInactive] = useState(false);
  const [editing, setEditing] = useState<RoomResponse | undefined>();
  const [formOpened, form] = useDisclosure(false);
  const queryClient = useQueryClient();
  const rooms = useRoomsList({ includeInactive });
  const refresh = { mutation: { onSuccess: () => queryClient.invalidateQueries({ queryKey: getRoomsListQueryKey() }) } };
  const activate = useRoomsActivate(refresh);
  const deactivate = useRoomsDeactivate(refresh);

  const openForm = (room?: RoomResponse) => {
    setEditing(room);
    form.open();
  };
  const confirmDeactivate = (room: RoomResponse) =>
    modals.openConfirmModal({
      title: `Deactivate ${room.name}?`,
      children: 'New sessions cannot be scheduled in an inactive room.',
      labels: { confirm: 'Deactivate', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: () => deactivate.mutate({ id: room.id }),
    });

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>Rooms</Title>
        <Group>
          <Switch label="Show inactive" checked={includeInactive} onChange={(e) => setIncludeInactive(e.currentTarget.checked)} />
          {canEdit && <Button leftSection={<IconPlus size={16} />} onClick={() => openForm()}>New room</Button>}
        </Group>
      </Group>
      <DataTable
        withTableBorder
        borderRadius="md"
        striped
        highlightOnHover
        minHeight={200}
        fetching={rooms.isFetching}
        records={rooms.data ?? []}
        noRecordsText="No rooms yet"
        columns={[
          { accessor: 'name', title: 'Name' },
          { accessor: 'capacity', title: 'Capacity', textAlign: 'right' },
          {
            accessor: 'isActive',
            title: 'Status',
            render: (room) => <Badge color={room.isActive ? 'teal' : 'gray'} variant="light">{room.isActive ? 'Active' : 'Inactive'}</Badge>,
          },
          {
            accessor: 'actions',
            title: '',
            textAlign: 'right',
            hidden: !canEdit,
            render: (room) => (
              <Group gap={4} justify="flex-end" wrap="nowrap">
                <Tooltip label="Edit"><ActionIcon variant="subtle" aria-label={`Edit ${room.name}`} onClick={() => openForm(room)}><IconPencil size={16} /></ActionIcon></Tooltip>
                {room.isActive ? (
                  <Tooltip label="Deactivate"><ActionIcon variant="subtle" color="red" aria-label={`Deactivate ${room.name}`} onClick={() => confirmDeactivate(room)}><IconPlayerPause size={16} /></ActionIcon></Tooltip>
                ) : (
                  <Tooltip label="Activate"><ActionIcon variant="subtle" aria-label={`Activate ${room.name}`} onClick={() => activate.mutate({ id: room.id })}><IconPlayerPlay size={16} /></ActionIcon></Tooltip>
                )}
              </Group>
            ),
          },
        ]}
      />
      <RoomFormModal opened={formOpened} onClose={form.close} room={editing} />
    </Stack>
  );
};
