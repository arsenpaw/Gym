import { Alert, Anchor, Breadcrumbs, Center, Loader, Stack, Tabs, Text } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
import { IconLogin2, IconMail, IconTicket } from '@tabler/icons-react';
import { keepPreviousData, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router';
import { useClientsCheckIn, useClientsGet, useClientsListVisits } from '../../api/generated/endpoints/clients/clients';
import { problemMessage } from '../../api/problem';
import { QueryErrorAlert } from '../../api/QueryErrorAlert';
import { ClientFormModal } from './ClientFormModal';
import { ClientHeader } from './ClientHeader';
import { ClientMembershipsTable } from './ClientMembershipsTable';
import { ClientMessagesPanel } from './ClientMessagesPanel';
import { ClientVisitsTable, visitPageSizes } from './ClientVisitsTable';
import { PurchaseMembershipModal } from './PurchaseMembershipModal';
import { refreshClient } from './refreshClient';

const tabs = ['memberships', 'visits', 'messages'] as const;
type Tab = (typeof tabs)[number];

const withCount = (label: string, count: number | undefined) => (count === undefined ? label : `${label} (${count})`);

export const ClientDetailsPage = () => {
  const { clientId = '' } = useParams();
  const queryClient = useQueryClient();
  const [params, setParams] = useSearchParams();
  const requested = params.get('tab');
  const tab: Tab = tabs.includes(requested as Tab) ? (requested as Tab) : 'memberships';
  const [visitPage, setVisitPage] = useState(1);
  const [visitPageSize, setVisitPageSize] = useState(visitPageSizes[0]);
  const client = useClientsGet(clientId);
  const visits = useClientsListVisits(
    clientId,
    { Page: visitPage, PageSize: visitPageSize },
    { query: { placeholderData: keepPreviousData } },
  );
  const [editOpened, edit] = useDisclosure(false);
  const [sellOpened, sell] = useDisclosure(false);
  const checkIn = useClientsCheckIn({
    mutation: {
      meta: { errorTitle: 'Check-in failed' },
      onSuccess: async () => {
        setVisitPage(1);
        await refreshClient(queryClient, clientId);
        notifications.show({ color: 'teal', message: 'Visit recorded' });
      },
    },
  });

  if (client.isPending) return <Center h={300}><Loader /></Center>;
  if (client.isError) return <Alert color="red" title="Could not load the client">{problemMessage(client.error)}</Alert>;

  const data = client.data;

  return (
    <Stack>
      <Breadcrumbs>
        <Anchor component={Link} to="/clients">Clients</Anchor>
        <Text>{data.fullName}</Text>
      </Breadcrumbs>
      <ClientHeader
        client={data}
        checkingIn={checkIn.isPending}
        onEdit={edit.open}
        onSell={sell.open}
        onCheckIn={() => checkIn.mutate({ id: data.id })}
      />
      <Tabs value={tab} onChange={(value) => setParams({ tab: value ?? 'memberships' }, { replace: true })} keepMounted={false}>
        <Tabs.List mb="md">
          <Tabs.Tab value="memberships" leftSection={<IconTicket size={16} />}>
            {withCount('Memberships', data.memberships.length)}
          </Tabs.Tab>
          <Tabs.Tab value="visits" leftSection={<IconLogin2 size={16} />}>
            {withCount('Visits', visits.data?.totalCount)}
          </Tabs.Tab>
          <Tabs.Tab value="messages" leftSection={<IconMail size={16} />}>Messages</Tabs.Tab>
        </Tabs.List>
        <Tabs.Panel value="memberships">
          <ClientMembershipsTable clientId={data.id} memberships={data.memberships} />
        </Tabs.Panel>
        <Tabs.Panel value="visits">
          {visits.isError ? (
            <QueryErrorAlert title="Could not load visits" error={visits.error} />
          ) : (
            <ClientVisitsTable
              visits={visits.data}
              fetching={visits.isFetching}
              memberships={data.memberships}
              page={visitPage}
              pageSize={visitPageSize}
              onPageChange={setVisitPage}
              onPageSizeChange={(size) => {
                setVisitPageSize(size);
                setVisitPage(1);
              }}
            />
          )}
        </Tabs.Panel>
        <Tabs.Panel value="messages">
          <ClientMessagesPanel clientId={data.id} />
        </Tabs.Panel>
      </Tabs>
      <ClientFormModal opened={editOpened} onClose={edit.close} client={data} />
      <PurchaseMembershipModal opened={sellOpened} onClose={sell.close} clientId={data.id} />
    </Stack>
  );
};
