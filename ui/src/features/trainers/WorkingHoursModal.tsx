import { zodResolver } from '@hookform/resolvers/zod';
import { ActionIcon, Button, Group, Modal, Stack, Text } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { IconPlus, IconTrash } from '@tabler/icons-react';
import { useQueryClient } from '@tanstack/react-query';
import { useFieldArray, useForm } from 'react-hook-form';
import { Select, TimeInput } from 'react-hook-form-mantine';
import { getTrainersGetQueryKey, useTrainersSetWorkingHours } from '../../api/generated/endpoints/trainers/trainers';
import { DayOfWeek, type TrainerResponse } from '../../api/generated/model';
import {
  toWorkingHoursRequest,
  weekDays,
  workingHoursDefaults,
  workingHoursFormSchema,
  type WorkingHoursFormValues,
} from './workingHoursForm';

type Props = { opened: boolean; onClose: () => void; trainer: TrainerResponse };

export const WorkingHoursModal = ({ opened, onClose, trainer }: Props) => (
  <Modal opened={opened} onClose={onClose} title="Working hours" size="lg" centered>
    {opened && <WorkingHoursForm trainer={trainer} onDone={onClose} />}
  </Modal>
);

const WorkingHoursForm = ({ trainer, onDone }: { trainer: TrainerResponse; onDone: () => void }) => {
  const queryClient = useQueryClient();
  const { control, handleSubmit, formState } = useForm<WorkingHoursFormValues>({
    resolver: zodResolver(workingHoursFormSchema),
    defaultValues: workingHoursDefaults(trainer.workingHours),
  });
  const rows = useFieldArray({ control, name: 'hours' });
  const save = useTrainersSetWorkingHours({
    mutation: {
      onSuccess: (saved) => {
        queryClient.setQueryData(getTrainersGetQueryKey(saved.id), saved);
        notifications.show({ color: 'teal', message: 'Working hours saved' });
        onDone();
      },
    },
  });

  const submit = handleSubmit(async (values) => {
    await save.mutateAsync({ id: trainer.id, data: toWorkingHoursRequest(values) }).catch(() => undefined);
  });

  return (
    <form onSubmit={submit} noValidate>
      <Stack>
        {rows.fields.length === 0 && <Text c="dimmed">No working hours. The trainer cannot be scheduled.</Text>}
        {rows.fields.map((row, index) => (
          <Group key={row.id} align="flex-start" wrap="nowrap">
            <Select control={control} name={`hours.${index}.day`} aria-label={`Day ${index + 1}`} data={weekDays} placeholder="Day" w={160} />
            <TimeInput control={control} name={`hours.${index}.start`} aria-label={`Start ${index + 1}`} />
            <TimeInput control={control} name={`hours.${index}.end`} aria-label={`End ${index + 1}`} />
            <ActionIcon variant="subtle" color="red" mt={4} aria-label={`Remove row ${index + 1}`} onClick={() => rows.remove(index)}>
              <IconTrash size={16} />
            </ActionIcon>
          </Group>
        ))}
        <Button
          variant="light"
          leftSection={<IconPlus size={16} />}
          onClick={() => rows.append({ day: DayOfWeek.Monday, start: '09:00', end: '18:00' })}
          w="fit-content"
        >
          Add hours
        </Button>
        <Group justify="flex-end">
          <Button variant="default" onClick={onDone}>Cancel</Button>
          <Button type="submit" loading={formState.isSubmitting}>Save</Button>
        </Group>
      </Stack>
    </form>
  );
};
