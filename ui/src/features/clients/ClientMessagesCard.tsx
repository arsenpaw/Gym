import { Alert, Badge, Button, Card, Center, Group, Loader, SegmentedControl, Stack, Table, Text, Title, Tooltip } from '@mantine/core';
import { modals } from '@mantine/modals';
import { notifications } from '@mantine/notifications';
import { IconSend } from '@tabler/icons-react';
import { useQueryClient } from '@tanstack/react-query';
import { DataTable } from 'mantine-datatable';
import { useState } from 'react';
import {
  getClientMessagesListQueryKey,
  useClientMessagesList,
  useClientMessagesPreview,
  useClientMessagesSend,
} from '../../api/generated/endpoints/client-messages/client-messages';
import { getNotificationsListQueryKey } from '../../api/generated/endpoints/notifications/notifications';
import { problemMessage } from '../../api/problem';
import { QueryErrorAlert } from '../../api/QueryErrorAlert';
import { formatDateTime } from '../../lib/format';
import { notificationStatusColors, notificationTypeLabels } from '../notifications/notificationLabels';

type Template = 'ExpiryReminder' | 'Promotion';

const templates: { value: Template; label: string }[] = [
  { value: 'ExpiryReminder', label: 'Expiry reminder' },
  { value: 'Promotion', label: 'Promotion' },
];

export const ClientMessagesCard = ({ clientId }: { clientId: string }) => {
  const queryClient = useQueryClient();
  const [template, setTemplate] = useState<Template>('ExpiryReminder');
  const preview = useClientMessagesPreview(clientId, { Template: template }, { query: { retry: false } });
  const history = useClientMessagesList(clientId);
  const send = useClientMessagesSend({
    mutation: {
      meta: { errorTitle: 'The email was not sent' },
      onSuccess: async (result) => {
        if (result.status === 'Sent') notifications.show({ color: 'teal', message: 'Email sent' });
        else notifications.show({ color: 'red', title: 'The email was not sent', message: result.failureReason ?? 'Unknown error' });
        await Promise.all([
          queryClient.invalidateQueries({ queryKey: getClientMessagesListQueryKey(clientId) }),
          queryClient.invalidateQueries({ queryKey: getNotificationsListQueryKey() }),
        ]);
      },
    },
  });

  const confirmSend = (recipient: string) =>
    modals.openConfirmModal({
      title: 'Send this email?',
      children: <Text size="sm">Send this email to {recipient}?</Text>,
      labels: { confirm: 'Send', cancel: 'Cancel' },
      onConfirm: () => send.mutate({ id: clientId, data: { template } }),
    });

  return (
    <Card withBorder radius="md">
      <Group justify="space-between" mb="sm">
        <Title order={4}>Messages</Title>
        <Group>
          <SegmentedControl data={templates} value={template} onChange={(value) => setTemplate(value as Template)} aria-label="Template" />
          <Button
            leftSection={<IconSend size={16} />}
            disabled={!preview.isSuccess}
            loading={send.isPending}
            onClick={() => preview.data && confirmSend(preview.data.recipient)}
          >
            Send email
          </Button>
        </Group>
      </Group>
      <Stack>
        {preview.isPending ? (
          <Center h={120}><Loader /></Center>
        ) : preview.isError ? (
          <Alert color="orange" title="This email can't be sent">{problemMessage(preview.error)}</Alert>
        ) : (
          <>
            <Table variant="vertical" layout="fixed">
              <Table.Tbody>
                <Table.Tr><Table.Th w={120}>To</Table.Th><Table.Td>{preview.data.recipient}</Table.Td></Table.Tr>
                <Table.Tr><Table.Th>Subject</Table.Th><Table.Td>{preview.data.subject}</Table.Td></Table.Tr>
              </Table.Tbody>
            </Table>
            <iframe
              title="Email preview"
              srcDoc={preview.data.html}
              sandbox=""
              style={{ width: '100%', height: 520, border: '1px solid var(--mantine-color-default-border)', borderRadius: 'var(--mantine-radius-md)' }}
            />
          </>
        )}
        <Title order={5}>Sent messages</Title>
        {history.isError ? (
          <QueryErrorAlert title="Could not load messages" error={history.error} />
        ) : (
          <DataTable
            minHeight={120}
            fetching={history.isFetching}
            records={history.data ?? []}
            noRecordsText="No messages yet"
            columns={[
              { accessor: 'createdAt', title: 'Created', render: (n) => formatDateTime(n.createdAt) },
              { accessor: 'type', title: 'Type', render: (n) => notificationTypeLabels[n.type] ?? n.type },
              { accessor: 'subject', title: 'Subject', ellipsis: true },
              {
                accessor: 'status',
                title: 'Status',
                render: (n) => (
                  <Tooltip label={n.failureReason} disabled={!n.failureReason}>
                    <Badge variant="light" color={notificationStatusColors[n.status] ?? 'gray'}>{n.status}</Badge>
                  </Tooltip>
                ),
              },
              { accessor: 'sentAt', title: 'Sent', render: (n) => (n.sentAt ? formatDateTime(n.sentAt) : '—') },
            ]}
          />
        )}
      </Stack>
    </Card>
  );
};
