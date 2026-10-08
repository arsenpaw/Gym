import { zodResolver } from '@hookform/resolvers/zod';
import { Button, Group, Modal, SimpleGrid, Stack } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useQueryClient } from '@tanstack/react-query';
import { useForm, useWatch } from 'react-hook-form';
import { NumberInput, SegmentedControl, Select, TextInput, TimeInput } from 'react-hook-form-mantine';
import { useRoomsList } from '../../api/generated/endpoints/rooms/rooms';
import { useSessionsSchedule } from '../../api/generated/endpoints/sessions/sessions';
import { useTrainersList } from '../../api/generated/endpoints/trainers/trainers';
import { SessionType } from '../../api/generated/model';
import { today } from '../../lib/dates';
import { invalidateSessions } from './invalidateSessions';
import { durations, sessionFormSchema, toScheduleRequest, type SessionFormValues } from './sessionForm';

export type SessionSlot = { date: string; startTime: string };

export const ScheduleSessionModal = ({ slot, onClose }: { slot: SessionSlot | null; onClose: () => void }) => (
  <Modal opened={slot !== null} onClose={onClose} title="New session" size="lg" centered>
    {slot && <ScheduleSessionForm slot={slot} onDone={onClose} />}
  </Modal>
);

const ScheduleSessionForm = ({ slot, onDone }: { slot: SessionSlot; onDone: () => void }) => {
  const queryClient = useQueryClient();
  const trainers = useTrainersList({ includeInactive: false });
  const rooms = useRoomsList({ includeInactive: false });
  const { control, handleSubmit, formState } = useForm<SessionFormValues>({
    resolver: zodResolver(sessionFormSchema),
    defaultValues: {
      title: '',
      type: SessionType.Group,
      trainerId: null,
      roomId: null,
      date: slot.date,
      startTime: slot.startTime,
      durationMinutes: '60',
      capacity: 10,
    },
  });
  const [type, roomId] = useWatch({ control, name: ['type', 'roomId'] });
  const room = rooms.data?.find((r) => r.id === roomId);
  const schedule = useSessionsSchedule({
    mutation: {
      meta: { errorTitle: 'Could not schedule the session' },
      onSuccess: async () => {
        await invalidateSessions(queryClient);
        notifications.show({ color: 'teal', message: 'Session scheduled' });
        onDone();
      },
    },
  });

  const submit = handleSubmit(async (values) => {
    await schedule.mutateAsync({ data: toScheduleRequest(values) }).catch(() => undefined);
  });

  return (
    <form onSubmit={submit} noValidate>
      <Stack>
        <TextInput control={control} name="title" label="Title" placeholder="Morning yoga" withAsterisk data-autofocus />
        <SegmentedControl control={control} name="type" data={[SessionType.Group, SessionType.Individual]} />
        <SimpleGrid cols={{ base: 1, sm: 2 }}>
          <Select
            control={control}
            name="trainerId"
            label="Trainer"
            searchable
            withAsterisk
            data={(trainers.data ?? []).map((t) => ({ value: t.id, label: `${t.fullName} · ${t.specialization}` }))}
          />
          <Select
            control={control}
            name="roomId"
            label="Room"
            withAsterisk
            data={(rooms.data ?? []).map((r) => ({ value: r.id, label: `${r.name} (${r.capacity} places)` }))}
          />
        </SimpleGrid>
        <SimpleGrid cols={{ base: 1, sm: 3 }}>
          <TextInput control={control} name="date" type="date" label="Date" min={today()} withAsterisk />
          <TimeInput control={control} name="startTime" label="Starts at" withAsterisk />
          <Select control={control} name="durationMinutes" label="Duration" data={durations} withAsterisk allowDeselect={false} />
        </SimpleGrid>
        {type === SessionType.Group && (
          <NumberInput
            control={control}
            name="capacity"
            label="Places"
            description={room ? `${room.name} holds ${room.capacity}` : undefined}
            min={1}
            allowDecimal={false}
            withAsterisk
          />
        )}
        <Group justify="flex-end">
          <Button variant="default" onClick={onDone}>Cancel</Button>
          <Button type="submit" loading={formState.isSubmitting}>Schedule</Button>
        </Group>
      </Stack>
    </form>
  );
};
