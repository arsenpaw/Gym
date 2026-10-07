import { Badge, Button, Group, Stack, Switch, Title } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { IconPlus } from '@tabler/icons-react';
import { DataTable } from 'mantine-datatable';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import { useTrainersList } from '../../api/generated/endpoints/trainers/trainers';
import { Role, hasAnyRole } from '../../auth/roles';
import { useRoles } from '../../auth/useRoles';
import { TrainerFormModal } from './TrainerFormModal';

export const TrainersPage = () => {
  const navigate = useNavigate();
  const { roles } = useRoles();
  const canEdit = hasAnyRole(roles, [Role.Admin]);
  const [includeInactive, setIncludeInactive] = useState(false);
  const [formOpened, form] = useDisclosure(false);
  const trainers = useTrainersList({ includeInactive });

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>Trainers</Title>
        <Group>
          <Switch label="Show inactive" checked={includeInactive} onChange={(e) => setIncludeInactive(e.currentTarget.checked)} />
          {canEdit && <Button leftSection={<IconPlus size={16} />} onClick={form.open}>Hire trainer</Button>}
        </Group>
      </Group>
      <DataTable
        withTableBorder
        borderRadius="md"
        striped
        highlightOnHover
        minHeight={200}
        fetching={trainers.isFetching}
        records={trainers.data ?? []}
        noRecordsText="No trainers yet"
        onRowClick={({ record }) => navigate(`/trainers/${record.id}`)}
        columns={[
          { accessor: 'fullName', title: 'Name' },
          { accessor: 'specialization', title: 'Specialization' },
          { accessor: 'phone', title: 'Phone' },
          { accessor: 'email', title: 'Email', render: (t) => t.email ?? '—' },
          {
            accessor: 'isActive',
            title: 'Status',
            render: (t) => <Badge color={t.isActive ? 'teal' : 'gray'} variant="light">{t.isActive ? 'Active' : 'Inactive'}</Badge>,
          },
        ]}
      />
      <TrainerFormModal opened={formOpened} onClose={form.close} onSaved={(trainer) => navigate(`/trainers/${trainer.id}`)} />
    </Stack>
  );
};
