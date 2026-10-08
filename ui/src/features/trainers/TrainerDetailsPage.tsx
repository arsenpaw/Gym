import {
  ActionIcon,
  Alert,
  Anchor,
  Badge,
  Breadcrumbs,
  Button,
  Card,
  Center,
  Group,
  Loader,
  Select,
  SimpleGrid,
  Stack,
  Table,
  Text,
  TextInput,
  Title,
  Tooltip,
} from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { modals } from '@mantine/modals';
import { notifications } from '@mantine/notifications';
import { IconClock, IconPencil, IconPlayerPause, IconPlayerPlay, IconUserMinus } from '@tabler/icons-react';
import { useQueryClient } from '@tanstack/react-query';
import { DataTable } from 'mantine-datatable';
import { useState } from 'react';
import { Link, useParams } from 'react-router';
import { useClientsList } from '../../api/generated/endpoints/clients/clients';
import {
  getTrainersGetQueryKey,
  getTrainersListQueryKey,
  useTrainersActivate,
  useTrainersAssignClient,
  useTrainersDeactivate,
  useTrainersGet,
  useTrainersLinkIdentity,
  useTrainersUnassignClient,
} from '../../api/generated/endpoints/trainers/trainers';
import { problemMessage } from '../../api/problem';
import { Role, hasAnyRole } from '../../auth/roles';
import { useRoles } from '../../auth/useRoles';
import { formatDate } from '../../lib/format';
import { TrainerFormModal } from './TrainerFormModal';
import { sortByWeekDay } from './workingHoursForm';
import { WorkingHoursModal } from './WorkingHoursModal';

export const TrainerDetailsPage = () => {
  const { trainerId = '' } = useParams();
  const queryClient = useQueryClient();
  const { roles } = useRoles();
  const isAdmin = hasAnyRole(roles, [Role.Admin]);
  const trainer = useTrainersGet(trainerId);
  const clients = useClientsList({ query: { enabled: isAdmin } });
  const [editOpened, edit] = useDisclosure(false);
  const [hoursOpened, hours] = useDisclosure(false);
  const [clientToAssign, setClientToAssign] = useState<string | null>(null);
  const [identity, setIdentity] = useState<string | null>(null);
  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: getTrainersGetQueryKey(trainerId) }),
      queryClient.invalidateQueries({ queryKey: getTrainersListQueryKey() }),
    ]);
  const activate = useTrainersActivate({ mutation: { onSuccess: refresh } });
  const deactivate = useTrainersDeactivate({ mutation: { onSuccess: refresh } });
  const assign = useTrainersAssignClient({
    mutation: {
      onSuccess: async () => {
        setClientToAssign(null);
        await refresh();
      },
    },
  });
  const unassign = useTrainersUnassignClient({ mutation: { onSuccess: refresh } });
  const linkIdentity = useTrainersLinkIdentity({
    mutation: {
      onSuccess: async () => {
        setIdentity(null);
        await refresh();
        notifications.show({ color: 'teal', message: 'Login linked' });
      },
    },
  });

  if (trainer.isPending) return <Center h={300}><Loader /></Center>;
  if (trainer.isError) return <Alert color="red" title="Could not load the trainer">{problemMessage(trainer.error)}</Alert>;

  const data = trainer.data;
  const assigned = new Set(data.clients.map((c) => c.clientId));
  const confirmDeactivate = () =>
    modals.openConfirmModal({
      title: `Deactivate ${data.fullName}?`,
      children: 'An inactive trainer cannot run new sessions.',
      labels: { confirm: 'Deactivate', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: () => deactivate.mutate({ id: data.id }),
    });
  const identityValue = identity ?? data.identityUserId ?? '';

  return (
    <Stack>
      <Breadcrumbs>
        <Anchor component={Link} to="/trainers">Trainers</Anchor>
        <Text>{data.fullName}</Text>
      </Breadcrumbs>
      <Group justify="space-between">
        <Group>
          <Title order={2}>{data.fullName}</Title>
          <Badge variant="light">{data.specialization}</Badge>
          {!data.isActive && <Badge color="gray" variant="light">Inactive</Badge>}
        </Group>
        {isAdmin && (
          <Group>
            <Button variant="default" leftSection={<IconPencil size={16} />} onClick={edit.open}>Edit</Button>
            {data.isActive ? (
              <Button variant="light" color="red" leftSection={<IconPlayerPause size={16} />} onClick={confirmDeactivate}>Deactivate</Button>
            ) : (
              <Button variant="light" leftSection={<IconPlayerPlay size={16} />} onClick={() => activate.mutate({ id: data.id })}>Activate</Button>
            )}
          </Group>
        )}
      </Group>
      <SimpleGrid cols={{ base: 1, md: 2 }}>
        <Card withBorder radius="md">
          <Title order={4} mb="sm">Contacts</Title>
          <Table variant="vertical" layout="fixed">
            <Table.Tbody>
              <Table.Tr><Table.Th w={140}>Phone</Table.Th><Table.Td>{data.phone}</Table.Td></Table.Tr>
              <Table.Tr><Table.Th>Email</Table.Th><Table.Td>{data.email ?? '—'}</Table.Td></Table.Tr>
              <Table.Tr><Table.Th>Login</Table.Th><Table.Td>{data.identityUserId ?? 'Not linked'}</Table.Td></Table.Tr>
            </Table.Tbody>
          </Table>
          {isAdmin && (
            <Group mt="md" align="flex-end">
              <TextInput
                label="Auth0 user id"
                description="Lets the trainer see their own schedule"
                placeholder="auth0|…"
                value={identityValue}
                onChange={(e) => setIdentity(e.currentTarget.value)}
                style={{ flex: 1 }}
              />
              <Button
                variant="light"
                loading={linkIdentity.isPending}
                disabled={identityValue.trim() === '' || identityValue.trim() === data.identityUserId}
                onClick={() => linkIdentity.mutate({ id: data.id, data: { identityUserId: identityValue.trim() } })}
              >
                Link login
              </Button>
            </Group>
          )}
        </Card>
        <Card withBorder radius="md">
          <Group justify="space-between" mb="sm">
            <Title order={4}>Working hours</Title>
            {isAdmin && <Button size="xs" variant="light" leftSection={<IconClock size={14} />} onClick={hours.open}>Edit hours</Button>}
          </Group>
          {data.workingHours.length === 0 ? (
            <Text c="dimmed">No working hours yet.</Text>
          ) : (
            <Table>
              <Table.Tbody>
                {sortByWeekDay(data.workingHours).map((row) => (
                  <Table.Tr key={`${row.day}-${row.start}`}>
                    <Table.Td>{row.day}</Table.Td>
                    <Table.Td>{row.start.slice(0, 5)} – {row.end.slice(0, 5)}</Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          )}
        </Card>
      </SimpleGrid>
      <Card withBorder radius="md">
        <Group justify="space-between" mb="sm" align="flex-end">
          <Title order={4}>Clients</Title>
          {isAdmin && (
            <Group align="flex-end">
              <Select
                aria-label="Client to assign"
                placeholder="Find a client"
                searchable
                data={(clients.data ?? []).filter((c) => !assigned.has(c.id)).map((c) => ({ value: c.id, label: `${c.fullName} · ${c.phone}` }))}
                value={clientToAssign}
                onChange={setClientToAssign}
                w={320}
              />
              <Button
                disabled={!clientToAssign}
                loading={assign.isPending}
                onClick={() => clientToAssign && assign.mutate({ id: data.id, clientId: clientToAssign })}
              >
                Assign
              </Button>
            </Group>
          )}
        </Group>
        <DataTable
          minHeight={120}
          idAccessor="clientId"
          records={data.clients}
          noRecordsText="No clients assigned"
          columns={[
            {
              accessor: 'fullName',
              title: 'Client',
              render: (c) => <Anchor component={Link} to={`/clients/${c.clientId}`}>{c.fullName ?? 'Unknown client'}</Anchor>,
            },
            { accessor: 'assignedAt', title: 'Assigned', render: (c) => formatDate(c.assignedAt) },
            {
              accessor: 'actions',
              title: '',
              textAlign: 'right',
              hidden: !isAdmin,
              render: (c) => (
                <Tooltip label="Unassign">
                  <ActionIcon variant="subtle" color="red" aria-label={`Unassign ${c.fullName ?? c.clientId}`} onClick={() => unassign.mutate({ id: data.id, clientId: c.clientId })}>
                    <IconUserMinus size={16} />
                  </ActionIcon>
                </Tooltip>
              ),
            },
          ]}
        />
      </Card>
      <TrainerFormModal opened={editOpened} onClose={edit.close} trainer={data} />
      <WorkingHoursModal opened={hoursOpened} onClose={hours.close} trainer={data} />
    </Stack>
  );
};
