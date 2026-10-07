import { zodResolver } from '@hookform/resolvers/zod';
import { Button, Group, Modal, Stack } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { NumberInput, TextInput } from 'react-hook-form-mantine';
import {
  getRoomsGetQueryKey,
  getRoomsListQueryKey,
  useRoomsCreate,
  useRoomsUpdate,
} from '../../api/generated/endpoints/rooms/rooms';
import type { RoomResponse } from '../../api/generated/model';
import { roomFormSchema, type RoomFormValues } from './roomForm';

type Props = { opened: boolean; onClose: () => void; room?: RoomResponse };

export const RoomFormModal = ({ opened, onClose, room }: Props) => (
  <Modal opened={opened} onClose={onClose} title={room ? 'Edit room' : 'New room'} centered>
    {opened && <RoomForm room={room} onDone={onClose} />}
  </Modal>
);

const RoomForm = ({ room, onDone }: { room?: RoomResponse; onDone: () => void }) => {
  const queryClient = useQueryClient();
  const { control, handleSubmit, formState } = useForm<RoomFormValues>({
    resolver: zodResolver(roomFormSchema),
    defaultValues: { name: room?.name ?? '', capacity: room?.capacity ?? 20 },
  });
  const onSuccess = async () => {
    await queryClient.invalidateQueries({ queryKey: getRoomsListQueryKey() });
    if (room) await queryClient.invalidateQueries({ queryKey: getRoomsGetQueryKey(room.id) });
    notifications.show({ color: 'teal', message: room ? 'Room updated' : 'Room created' });
    onDone();
  };
  const create = useRoomsCreate({ mutation: { onSuccess } });
  const update = useRoomsUpdate({ mutation: { onSuccess } });

  const submit = handleSubmit(async (data) => {
    await (room ? update.mutateAsync({ id: room.id, data }) : create.mutateAsync({ data })).catch(() => undefined);
  });

  return (
    <form onSubmit={submit} noValidate>
      <Stack>
        <TextInput control={control} name="name" label="Name" withAsterisk data-autofocus />
        <NumberInput control={control} name="capacity" label="Capacity" withAsterisk min={1} allowDecimal={false} />
        <Group justify="flex-end">
          <Button variant="default" onClick={onDone}>Cancel</Button>
          <Button type="submit" loading={formState.isSubmitting}>Save</Button>
        </Group>
      </Stack>
    </form>
  );
};
