import { zodResolver } from '@hookform/resolvers/zod';
import { Button, Group, Modal, SimpleGrid, Stack } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { TextInput } from 'react-hook-form-mantine';
import {
  getClientsGetQueryKey,
  getClientsListQueryKey,
  useClientsRegister,
  useClientsUpdate,
} from '../../api/generated/endpoints/clients/clients';
import type { ClientDetailsResponse } from '../../api/generated/model';
import { today } from '../../lib/dates';
import { clientFormDefaults, clientFormSchema, toClientRequest, type ClientFormValues } from './clientForm';

type Props = {
  opened: boolean;
  onClose: () => void;
  client?: ClientDetailsResponse;
  onSaved?: (client: ClientDetailsResponse) => void;
};

export const ClientFormModal = ({ opened, onClose, client, onSaved }: Props) => (
  <Modal opened={opened} onClose={onClose} title={client ? 'Edit client' : 'Register client'} size="lg" centered>
    {opened && <ClientForm client={client} onDone={onClose} onSaved={onSaved} />}
  </Modal>
);

const ClientForm = ({ client, onDone, onSaved }: { client?: ClientDetailsResponse; onDone: () => void; onSaved?: Props['onSaved'] }) => {
  const queryClient = useQueryClient();
  const { control, handleSubmit, formState } = useForm<ClientFormValues>({
    resolver: zodResolver(clientFormSchema),
    defaultValues: clientFormDefaults(client),
  });
  const onSuccess = async (saved: ClientDetailsResponse) => {
    queryClient.setQueryData(getClientsGetQueryKey(saved.id), saved);
    await queryClient.invalidateQueries({ queryKey: getClientsListQueryKey() });
    notifications.show({ color: 'teal', message: client ? 'Client updated' : 'Client registered' });
    onDone();
    onSaved?.(saved);
  };
  const register = useClientsRegister({ mutation: { onSuccess } });
  const update = useClientsUpdate({ mutation: { onSuccess } });

  const submit = handleSubmit(async (values) => {
    const data = toClientRequest(values);
    await (client ? update.mutateAsync({ id: client.id, data }) : register.mutateAsync({ data })).catch(() => undefined);
  });

  return (
    <form onSubmit={submit} noValidate>
      <Stack>
        <SimpleGrid cols={{ base: 1, sm: 3 }}>
          <TextInput control={control} name="lastName" label="Last name" withAsterisk data-autofocus />
          <TextInput control={control} name="firstName" label="First name" withAsterisk />
          <TextInput control={control} name="middleName" label="Middle name" />
        </SimpleGrid>
        <SimpleGrid cols={{ base: 1, sm: 2 }}>
          <TextInput control={control} name="dateOfBirth" type="date" label="Date of birth" max={today()} withAsterisk />
          <TextInput control={control} name="phone" label="Phone" placeholder="+380 67 123 4567" withAsterisk />
        </SimpleGrid>
        <TextInput control={control} name="email" label="Email" description="Expiry reminders go here; without email they go by SMS" />
        <Group justify="flex-end">
          <Button variant="default" onClick={onDone}>Cancel</Button>
          <Button type="submit" loading={formState.isSubmitting}>Save</Button>
        </Group>
      </Stack>
    </form>
  );
};
