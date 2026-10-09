import { Alert, Badge, Button, Center, Group, Loader, SegmentedControl, Stack, Text, Title, Tooltip } from '@mantine/core';
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
import { EmailPreviewFrame } from './EmailPreviewFrame';

type Template = 'ExpiryReminder' | 'Promotion';

const templates: { value: Template; label: string }[] = [
  { value: 'ExpiryReminder', label: 'Expiry reminder' },
  { value: 'Promotion', label: 'Promotion' },
];

export const ClientMessagesPanel = ({ clientId }: { clientId: string }) => {
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
    <Stack gap="lg">
      <Group justify="space-between">
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
      {preview.isPending ? (
        <Center h={200}><Loader /></Center>
      ) : preview.isError ? (
        <Alert color="orange" title="This email can't be sent">{problemMessage(preview.error)}</Alert>
      ) : (
        <Stack gap="xs">
          <Group gap="xs" wrap="nowrap">
            <Text size="sm" c="dimmed" w={64}>To</Text>
            <Text size="sm">{preview.data.recipient}</Text>
          </Group>
          <Group gap="xs" wrap="nowrap">
            <Text size="sm" c="dimmed" w={64}>Subject</Text>
            <Text size="sm" fw={600}>{preview.data.subject}</Text>
          </Group>
          <EmailPreviewFrame html={preview.data.html} />
        </Stack>
      )}
      <Stack gap="xs">
        <Title order={5}>Sent messages</Title>
        {history.isError ? (
          <QueryErrorAlert title="Could not load messages" error={history.error} />
        ) : (
          <DataTable
            withTableBorder
            borderRadius="md"
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
    </Stack>
  );
};
