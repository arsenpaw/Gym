import { ActionIcon, Alert, Anchor, Badge, Breadcrumbs, Button, Card, Center, Group, Loader, SimpleGrid, Stack, Table, Text, Title, Tooltip } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { modals } from '@mantine/modals';
import { notifications } from '@mantine/notifications';
import { IconLogin2, IconPencil, IconTicket, IconX } from '@tabler/icons-react';
import { useQueryClient } from '@tanstack/react-query';
import { DataTable } from 'mantine-datatable';
import { Link, useParams } from 'react-router';
import {
  useClientsCancelMembership,
  useClientsCheckIn,
  useClientsGet,
  useClientsListVisits,
} from '../../api/generated/endpoints/clients/clients';
import type { MembershipResponse } from '../../api/generated/model';
import { problemMessage } from '../../api/problem';
import { QueryErrorAlert } from '../../api/QueryErrorAlert';
import { formatDate, formatDateTime, formatMoney } from '../../lib/format';
import { ClientFormModal } from './ClientFormModal';
import { ClientMessagesCard } from './ClientMessagesCard';
import { MembershipBadge } from './MembershipBadge';
import { membershipRowStatus, type MembershipRowStatus } from './membershipStatus';
import { PurchaseMembershipModal } from './PurchaseMembershipModal';
import { refreshClient } from './refreshClient';

const statusColors: Record<MembershipRowStatus, string> = { Active: 'teal', Upcoming: 'blue', Expired: 'gray', Cancelled: 'red' };

export const ClientDetailsPage = () => {
  const { clientId = '' } = useParams();
  const queryClient = useQueryClient();
  const client = useClientsGet(clientId);
  const visits = useClientsListVisits(clientId);
  const [editOpened, edit] = useDisclosure(false);
  const [sellOpened, sell] = useDisclosure(false);
  const checkIn = useClientsCheckIn({
    mutation: {
      meta: { errorTitle: 'Check-in failed' },
      onSuccess: async () => {
        await refreshClient(queryClient, clientId);
        notifications.show({ color: 'teal', message: 'Visit recorded' });
      },
    },
  });
  const cancelMembership = useClientsCancelMembership({
    mutation: { onSuccess: () => refreshClient(queryClient, clientId) },
  });

  if (client.isPending) return <Center h={300}><Loader /></Center>;
  if (client.isError) return <Alert color="red" title="Could not load the client">{problemMessage(client.error)}</Alert>;

  const data = client.data;
  const planNames = new Map(data.memberships.map((m) => [m.id, m.planName]));
  const confirmCancel = (membership: MembershipResponse) =>
    modals.openConfirmModal({
      title: `Cancel ${membership.planName}?`,
      children: 'The client will not be able to check in with this membership any more.',
      labels: { confirm: 'Cancel membership', cancel: 'Keep it' },
      confirmProps: { color: 'red' },
      onConfirm: () => cancelMembership.mutate({ id: data.id, membershipId: membership.id }),
    });

  return (
    <Stack>
      <Breadcrumbs>
        <Anchor component={Link} to="/clients">Clients</Anchor>
        <Text>{data.fullName}</Text>
      </Breadcrumbs>
      <Group justify="space-between">
        <Group>
          <Title order={2}>{data.fullName}</Title>
          <MembershipBadge membership={data.activeMembership} />
        </Group>
        <Group>
          <Button variant="default" leftSection={<IconPencil size={16} />} onClick={edit.open}>Edit</Button>
          <Button variant="light" leftSection={<IconTicket size={16} />} onClick={sell.open}>Sell membership</Button>
          <Button leftSection={<IconLogin2 size={16} />} loading={checkIn.isPending} onClick={() => checkIn.mutate({ id: data.id })}>Check in</Button>
        </Group>
      </Group>
      <SimpleGrid cols={{ base: 1, md: 2 }}>
        <Card withBorder radius="md">
          <Title order={4} mb="sm">Profile</Title>
          <Table variant="vertical" layout="fixed">
            <Table.Tbody>
              <Table.Tr><Table.Th w={160}>Date of birth</Table.Th><Table.Td>{formatDate(data.dateOfBirth)} ({data.age} years)</Table.Td></Table.Tr>
              <Table.Tr><Table.Th>Phone</Table.Th><Table.Td>{data.phone}</Table.Td></Table.Tr>
              <Table.Tr><Table.Th>Email</Table.Th><Table.Td>{data.email ?? '—'}</Table.Td></Table.Tr>
              <Table.Tr><Table.Th>Registered</Table.Th><Table.Td>{formatDate(data.registeredAt)}</Table.Td></Table.Tr>
            </Table.Tbody>
          </Table>
        </Card>
        <Card withBorder radius="md">
          <Title order={4} mb="sm">Current membership</Title>
          {data.activeMembership ? (
            <Table variant="vertical" layout="fixed">
              <Table.Tbody>
                <Table.Tr><Table.Th w={160}>Plan</Table.Th><Table.Td>{data.activeMembership.planName}</Table.Td></Table.Tr>
                <Table.Tr><Table.Th>Valid</Table.Th><Table.Td>{formatDate(data.activeMembership.startsOn)} – {formatDate(data.activeMembership.endsOn)}</Table.Td></Table.Tr>
                <Table.Tr><Table.Th>Visits left</Table.Th><Table.Td>{data.activeMembership.visitsLeft ?? 'Unlimited'}</Table.Td></Table.Tr>
              </Table.Tbody>
            </Table>
          ) : (
            <Text c="dimmed">No active membership. Sell one to let the client check in.</Text>
          )}
        </Card>
      </SimpleGrid>
      <Card withBorder radius="md">
        <Title order={4} mb="sm">Memberships</Title>
        <DataTable
          minHeight={120}
          records={data.memberships}
          noRecordsText="No memberships yet"
          columns={[
            { accessor: 'planName', title: 'Plan' },
            { accessor: 'startsOn', title: 'Valid', render: (m) => `${formatDate(m.startsOn)} – ${formatDate(m.endsOn)}` },
            { accessor: 'price', title: 'Price', textAlign: 'right', render: (m) => formatMoney(m.price) },
            { accessor: 'visitsUsed', title: 'Visits', render: (m) => (m.visitLimit ? `${m.visitsUsed} / ${m.visitLimit}` : `${m.visitsUsed} / unlimited`) },
            {
              accessor: 'status',
              title: 'Status',
              render: (m) => {
                const status = membershipRowStatus(m);
                return <Badge variant="light" color={statusColors[status]}>{status}</Badge>;
              },
            },
            {
              accessor: 'actions',
              title: '',
              textAlign: 'right',
              render: (m) =>
                membershipRowStatus(m) === 'Active' || membershipRowStatus(m) === 'Upcoming' ? (
                  <Tooltip label="Cancel membership">
                    <ActionIcon variant="subtle" color="red" aria-label={`Cancel ${m.planName}`} onClick={() => confirmCancel(m)}>
                      <IconX size={16} />
                    </ActionIcon>
                  </Tooltip>
                ) : null,
            },
          ]}
        />
      </Card>
      <Card withBorder radius="md">
        <Title order={4} mb="sm">Visits</Title>
        {visits.isError ? (
          <QueryErrorAlert title="Could not load visits" error={visits.error} />
        ) : (
          <DataTable
            minHeight={120}
            fetching={visits.isFetching}
            records={visits.data ?? []}
            noRecordsText="No visits yet"
            columns={[
              { accessor: 'checkedInAt', title: 'Checked in', render: (v) => formatDateTime(v.checkedInAt) },
              { accessor: 'membershipId', title: 'Membership', render: (v) => planNames.get(v.membershipId) ?? '—' },
            ]}
          />
        )}
      </Card>
      <ClientMessagesCard clientId={data.id} />
      <ClientFormModal opened={editOpened} onClose={edit.close} client={data} />
      <PurchaseMembershipModal opened={sellOpened} onClose={sell.close} clientId={data.id} />
    </Stack>
  );
};
