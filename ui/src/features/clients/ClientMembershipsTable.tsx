import { ActionIcon, Badge, Tooltip } from '@mantine/core';
import { modals } from '@mantine/modals';
import { IconX } from '@tabler/icons-react';
import { useQueryClient } from '@tanstack/react-query';
import { DataTable } from 'mantine-datatable';
import { useClientsCancelMembership } from '../../api/generated/endpoints/clients/clients';
import type { MembershipResponse } from '../../api/generated/model';
import { formatDate, formatMoney } from '../../lib/format';
import { membershipRowStatus, type MembershipRowStatus } from './membershipStatus';
import { refreshClient } from './refreshClient';

const statusColors: Record<MembershipRowStatus, string> = { Active: 'teal', Upcoming: 'blue', Expired: 'gray', Cancelled: 'red' };

export const ClientMembershipsTable = ({ clientId, memberships }: { clientId: string; memberships: MembershipResponse[] }) => {
  const queryClient = useQueryClient();
  const cancelMembership = useClientsCancelMembership({
    mutation: { onSuccess: () => refreshClient(queryClient, clientId) },
  });
  const confirmCancel = (membership: MembershipResponse) =>
    modals.openConfirmModal({
      title: `Cancel ${membership.planName}?`,
      children: 'The client will not be able to check in with this membership any more.',
      labels: { confirm: 'Cancel membership', cancel: 'Keep it' },
      confirmProps: { color: 'red' },
      onConfirm: () => cancelMembership.mutate({ id: clientId, membershipId: membership.id }),
    });

  return (
    <DataTable
      withTableBorder
      borderRadius="md"
      minHeight={120}
      records={memberships}
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
  );
};
