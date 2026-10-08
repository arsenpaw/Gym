import { Alert, Button, Card, Group, Select, Stack, Text, Title } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { IconCheck, IconLogin2, IconSearch } from '@tabler/icons-react';
import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { Link } from 'react-router';
import { QueryErrorAlert } from '../../api/QueryErrorAlert';
import { useClientsCheckIn, useClientsList } from '../../api/generated/endpoints/clients/clients';
import type { VisitResponse } from '../../api/generated/model';
import { formatTime } from '../../lib/format';
import { MembershipBadge } from '../clients/MembershipBadge';
import { refreshClient } from '../clients/refreshClient';

export const CheckInPage = () => {
  const queryClient = useQueryClient();
  const clients = useClientsList();
  const [clientId, setClientId] = useState<string | null>(null);
  const [lastVisit, setLastVisit] = useState<VisitResponse | null>(null);
  const client = clients.data?.find((c) => c.id === clientId);
  const checkIn = useClientsCheckIn({
    mutation: {
      meta: { errorTitle: 'Check-in failed' },
      onSuccess: async (visit) => {
        setLastVisit(visit);
        notifications.show({ color: 'teal', message: 'Checked in' });
        await refreshClient(queryClient, visit.clientId);
      },
    },
  });

  return (
    <Stack maw={680}>
      <Title order={2}>Check-in</Title>
      <Select
        label="Client"
        placeholder="Start typing a name or phone"
        size="lg"
        searchable
        clearable
        leftSection={<IconSearch size={18} />}
        nothingFoundMessage="No clients found"
        data={(clients.data ?? []).map((c) => ({ value: c.id, label: `${c.fullName} · ${c.phone}` }))}
        value={clientId}
        onChange={(value) => {
          setClientId(value);
          setLastVisit(null);
        }}
      />
      {clients.isError && <QueryErrorAlert title="Could not load clients" error={clients.error} />}
      {client && (
        <Card withBorder radius="md" padding="lg">
          <Group justify="space-between" align="flex-start">
            <div>
              <Text fw={600} size="lg">{client.fullName}</Text>
              <Text c="dimmed" size="sm">{client.phone} · {client.age} years</Text>
            </div>
            <MembershipBadge membership={client.activeMembership} />
          </Group>
          {client.activeMembership ? (
            <Text mt="sm">
              {client.activeMembership.planName} · visits left: {client.activeMembership.visitsLeft ?? 'unlimited'}
            </Text>
          ) : (
            <Alert color="orange" mt="md">No active membership. Sell one on the client's profile before check-in.</Alert>
          )}
          {lastVisit?.clientId === client.id && (
            <Alert color="teal" mt="md" icon={<IconCheck size={18} />}>Checked in at {formatTime(lastVisit.checkedInAt)}</Alert>
          )}
          <Group mt="lg">
            <Button size="md" leftSection={<IconLogin2 size={18} />} loading={checkIn.isPending} onClick={() => checkIn.mutate({ id: client.id })}>
              Check in
            </Button>
            <Button variant="subtle" component={Link} to={`/clients/${client.id}`}>Open profile</Button>
          </Group>
        </Card>
      )}
    </Stack>
  );
};
