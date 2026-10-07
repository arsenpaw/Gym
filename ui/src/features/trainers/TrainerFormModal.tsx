import { zodResolver } from '@hookform/resolvers/zod';
import { Button, Group, Modal, SimpleGrid, Stack } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { TextInput } from 'react-hook-form-mantine';
import {
  getTrainersGetQueryKey,
  getTrainersListQueryKey,
  useTrainersHire,
  useTrainersUpdateProfile,
} from '../../api/generated/endpoints/trainers/trainers';
import type { TrainerResponse } from '../../api/generated/model';
import { toTrainerRequest, trainerFormDefaults, trainerFormSchema, type TrainerFormValues } from './trainerForm';

type Props = { opened: boolean; onClose: () => void; trainer?: TrainerResponse; onSaved?: (trainer: TrainerResponse) => void };

export const TrainerFormModal = ({ opened, onClose, trainer, onSaved }: Props) => (
  <Modal opened={opened} onClose={onClose} title={trainer ? 'Edit trainer' : 'Hire trainer'} size="lg" centered>
    {opened && <TrainerForm trainer={trainer} onDone={onClose} onSaved={onSaved} />}
  </Modal>
);

const TrainerForm = ({ trainer, onDone, onSaved }: { trainer?: TrainerResponse; onDone: () => void; onSaved?: Props['onSaved'] }) => {
  const queryClient = useQueryClient();
  const { control, handleSubmit, formState } = useForm<TrainerFormValues>({
    resolver: zodResolver(trainerFormSchema),
    defaultValues: trainerFormDefaults(trainer),
  });
  const onSuccess = async (saved: TrainerResponse) => {
    queryClient.setQueryData(getTrainersGetQueryKey(saved.id), saved);
    await queryClient.invalidateQueries({ queryKey: getTrainersListQueryKey() });
    notifications.show({ color: 'teal', message: trainer ? 'Trainer updated' : 'Trainer hired' });
    onDone();
    onSaved?.(saved);
  };
  const hire = useTrainersHire({ mutation: { onSuccess } });
  const update = useTrainersUpdateProfile({ mutation: { onSuccess } });

  const submit = handleSubmit(async (values) => {
    const data = toTrainerRequest(values);
    await (trainer ? update.mutateAsync({ id: trainer.id, data }) : hire.mutateAsync({ data })).catch(() => undefined);
  });

  return (
    <form onSubmit={submit} noValidate>
      <Stack>
        <SimpleGrid cols={{ base: 1, sm: 3 }}>
          <TextInput control={control} name="lastName" label="Last name" withAsterisk data-autofocus />
          <TextInput control={control} name="firstName" label="First name" withAsterisk />
          <TextInput control={control} name="middleName" label="Middle name" />
        </SimpleGrid>
        <TextInput control={control} name="specialization" label="Specialization" placeholder="Yoga, CrossFit, boxing…" withAsterisk />
        <SimpleGrid cols={{ base: 1, sm: 2 }}>
          <TextInput control={control} name="phone" label="Phone" withAsterisk />
          <TextInput control={control} name="email" label="Email" />
        </SimpleGrid>
        <Group justify="flex-end">
          <Button variant="default" onClick={onDone}>Cancel</Button>
          <Button type="submit" loading={formState.isSubmitting}>Save</Button>
        </Group>
      </Stack>
    </form>
  );
};
