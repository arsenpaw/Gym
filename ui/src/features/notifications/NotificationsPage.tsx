import { ActionIcon, Badge, Button, Group, SegmentedControl, Stack, Text, Title, Tooltip } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { IconPlayerPlay, IconRefresh } from '@tabler/icons-react';
import { useQueryClient } from '@tanstack/react-query';
import { DataTable } from 'mantine-datatable';
import { useState } from 'react';
import { useClientsList } from '../../api/generated/endpoints/clients/clients';
import {
  getNotificationsListQueryKey,
  useNotificationsList,
  useNotificationsRetry,
  useNotificationsRun,
} from '../../api/generated/endpoints/notifications/notifications';
import { formatDateTime } from '../../lib/format';

const statusFilters = ['All', 'Pending', 'Sent', 'Failed'] as const;
type StatusFilter = (typeof statusFilters)[number];

const statusColors: Record<string, string> = { Pending: 'yellow', Sent: 'teal', Failed: 'red' };

export const NotificationsPage = () => {
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<StatusFilter>('All');
  const list = useNotificationsList(status === 'All' ? undefined : { Status: status });
  const clients = useClientsList();
  const clientNames = new Map((clients.data ?? []).map((c) => [c.id, c.fullName]));
  const refresh = () => queryClient.invalidateQueries({ queryKey: getNotificationsListQueryKey() });
  const run = useNotificationsRun({
    mutation: {
      meta: { errorTitle: 'The run failed' },
      onSuccess: async (result) => {
        notifications.show({
          color: result.failed > 0 ? 'orange' : 'teal',
          title: 'Run finished',
          message: `Created ${result.created}, sent ${result.sent}, failed ${result.failed}`,
        });
        await refresh();
      },
    },
  });
  const retry = useNotificationsRetry({
    mutation: {
      onSuccess: async () => {
        notifications.show({ color: 'teal', message: 'It will be sent again on the next run' });
        await refresh();
      },
    },
  });

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>Expiry notifications</Title>
        <Button leftSection={<IconPlayerPlay size={16} />} loading={run.isPending} onClick={() => run.mutate()}>Run now</Button>
      </Group>
      <Text c="dimmed" size="sm">Clients are reminded automatically every morning before their membership ends.</Text>
      <SegmentedControl
        w="fit-content"
        data={[...statusFilters]}
        value={status}
        onChange={(value) => setStatus(value as StatusFilter)}
        aria-label="Status"
      />
      <DataTable
        withTableBorder
        borderRadius="md"
        striped
        minHeight={200}
        fetching={list.isFetching}
        records={list.data ?? []}
        noRecordsText="No notifications"
        columns={[
          { accessor: 'createdAt', title: 'Created', render: (n) => formatDateTime(n.createdAt) },
          { accessor: 'clientId', title: 'Client', render: (n) => clientNames.get(n.clientId) ?? '—' },
          { accessor: 'channel', title: 'Channel', render: (n) => <Badge variant="outline">{n.channel}</Badge> },
          { accessor: 'recipient', title: 'Recipient' },
          { accessor: 'message', title: 'Message', ellipsis: true, width: 320 },
          {
            accessor: 'status',
            title: 'Status',
            render: (n) => (
              <Tooltip label={n.failureReason} disabled={!n.failureReason}>
                <Badge variant="light" color={statusColors[n.status] ?? 'gray'}>{n.status}</Badge>
              </Tooltip>
            ),
          },
          { accessor: 'sentAt', title: 'Sent', render: (n) => (n.sentAt ? formatDateTime(n.sentAt) : '—') },
          {
            accessor: 'actions',
            title: '',
            textAlign: 'right',
            render: (n) =>
              n.status === 'Failed' ? (
                <Tooltip label="Retry">
                  <ActionIcon variant="subtle" aria-label={`Retry notification to ${n.recipient}`} onClick={() => retry.mutate({ id: n.id })}>
                    <IconRefresh size={16} />
                  </ActionIcon>
                </Tooltip>
              ) : null,
          },
        ]}
      />
    </Stack>
  );
};
