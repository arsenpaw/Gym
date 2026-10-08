import { ActionIcon, Badge, Button, Group, Stack, Switch, Title, Tooltip } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { modals } from '@mantine/modals';
import { IconPencil, IconPlayerPause, IconPlayerPlay, IconPlus } from '@tabler/icons-react';
import { useQueryClient } from '@tanstack/react-query';
import { DataTable } from 'mantine-datatable';
import { useState } from 'react';
import { QueryErrorAlert } from '../../api/QueryErrorAlert';
import {
  getMembershipPlansListQueryKey,
  useMembershipPlansActivate,
  useMembershipPlansDeactivate,
  useMembershipPlansList,
} from '../../api/generated/endpoints/membership-plans/membership-plans';
import type { MembershipPlanResponse } from '../../api/generated/model';
import { Role, hasAnyRole } from '../../auth/roles';
import { useRoles } from '../../auth/useRoles';
import { formatMoney } from '../../lib/format';
import { PlanFormModal } from './PlanFormModal';

export const PlansPage = () => {
  const { roles } = useRoles();
  const canEdit = hasAnyRole(roles, [Role.Admin]);
  const [includeInactive, setIncludeInactive] = useState(false);
  const [editing, setEditing] = useState<MembershipPlanResponse | undefined>();
  const [formOpened, form] = useDisclosure(false);
  const queryClient = useQueryClient();
  const plans = useMembershipPlansList({ includeInactive });
  const refresh = { mutation: { onSuccess: () => queryClient.invalidateQueries({ queryKey: getMembershipPlansListQueryKey() }) } };
  const activate = useMembershipPlansActivate(refresh);
  const deactivate = useMembershipPlansDeactivate(refresh);

  const openForm = (plan?: MembershipPlanResponse) => {
    setEditing(plan);
    form.open();
  };
  const confirmDeactivate = (plan: MembershipPlanResponse) =>
    modals.openConfirmModal({
      title: `Stop selling ${plan.name}?`,
      children: 'Clients who already bought it keep their memberships.',
      labels: { confirm: 'Deactivate', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: () => deactivate.mutate({ id: plan.id }),
    });

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>Membership plans</Title>
        <Group>
          <Switch label="Show inactive" checked={includeInactive} onChange={(e) => setIncludeInactive(e.currentTarget.checked)} />
          {canEdit && <Button leftSection={<IconPlus size={16} />} onClick={() => openForm()}>New plan</Button>}
        </Group>
      </Group>
      {plans.isError ? (
        <QueryErrorAlert title="Could not load plans" error={plans.error} />
      ) : (
        <DataTable
          withTableBorder
          borderRadius="md"
          striped
          highlightOnHover
          minHeight={200}
          fetching={plans.isFetching}
          records={plans.data ?? []}
          noRecordsText="No plans yet"
          columns={[
            { accessor: 'name', title: 'Name' },
            { accessor: 'price', title: 'Price', textAlign: 'right', render: (plan) => formatMoney(plan.price) },
            { accessor: 'validityDays', title: 'Valid for', render: (plan) => `${plan.validityDays} days` },
            { accessor: 'visitLimit', title: 'Visits', render: (plan) => plan.visitLimit ?? 'Unlimited' },
            {
              accessor: 'isActive',
              title: 'Status',
              render: (plan) => <Badge color={plan.isActive ? 'teal' : 'gray'} variant="light">{plan.isActive ? 'On sale' : 'Inactive'}</Badge>,
            },
            {
              accessor: 'actions',
              title: '',
              textAlign: 'right',
              hidden: !canEdit,
              render: (plan) => (
                <Group gap={4} justify="flex-end" wrap="nowrap">
                  <Tooltip label="Edit"><ActionIcon variant="subtle" aria-label={`Edit ${plan.name}`} onClick={() => openForm(plan)}><IconPencil size={16} /></ActionIcon></Tooltip>
                  {plan.isActive ? (
                    <Tooltip label="Deactivate"><ActionIcon variant="subtle" color="red" aria-label={`Deactivate ${plan.name}`} onClick={() => confirmDeactivate(plan)}><IconPlayerPause size={16} /></ActionIcon></Tooltip>
                  ) : (
                    <Tooltip label="Activate"><ActionIcon variant="subtle" aria-label={`Activate ${plan.name}`} onClick={() => activate.mutate({ id: plan.id })}><IconPlayerPlay size={16} /></ActionIcon></Tooltip>
                  )}
                </Group>
              ),
            },
          ]}
        />
      )}
      <PlanFormModal opened={formOpened} onClose={form.close} plan={editing} />
    </Stack>
  );
};
