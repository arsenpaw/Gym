import { Button, Group, Stack, TextInput, Title } from '@mantine/core';
import { useDebouncedValue, useDisclosure } from '@mantine/hooks';
import { IconSearch, IconUserPlus } from '@tabler/icons-react';
import { DataTable } from 'mantine-datatable';
import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router';
import { QueryErrorAlert } from '../../api/QueryErrorAlert';
import { useClientsList } from '../../api/generated/endpoints/clients/clients';
import { ClientFormModal } from './ClientFormModal';
import { MembershipBadge } from './MembershipBadge';

export const ClientsPage = () => {
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const [query] = useDebouncedValue(search.trim().toLowerCase(), 200);
  const [formOpened, form] = useDisclosure(false);
  const clients = useClientsList();
  const records = useMemo(
    () =>
      (clients.data ?? []).filter(
        (client) =>
          query === '' ||
          client.fullName.toLowerCase().includes(query) ||
          client.phone.replace(/\s/g, '').includes(query.replace(/\s/g, '')) ||
          (client.email ?? '').toLowerCase().includes(query),
      ),
    [clients.data, query],
  );

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>Clients</Title>
        <Button leftSection={<IconUserPlus size={16} />} onClick={form.open}>Register client</Button>
      </Group>
      <TextInput
        placeholder="Search by name, phone or email"
        leftSection={<IconSearch size={16} />}
        value={search}
        onChange={(e) => setSearch(e.currentTarget.value)}
        aria-label="Search clients"
      />
      {clients.isError ? (
        <QueryErrorAlert title="Could not load clients" error={clients.error} />
      ) : (
        <DataTable
          withTableBorder
          borderRadius="md"
          striped
          highlightOnHover
          minHeight={200}
          fetching={clients.isFetching}
          records={records}
          noRecordsText={query ? 'No clients match your search' : 'No clients yet'}
          onRowClick={({ record }) => navigate(`/clients/${record.id}`)}
          columns={[
            { accessor: 'fullName', title: 'Name' },
            { accessor: 'age', title: 'Age', textAlign: 'right' },
            { accessor: 'phone', title: 'Phone' },
            { accessor: 'email', title: 'Email', render: (client) => client.email ?? '—' },
            {
              accessor: 'activeMembership',
              title: 'Membership',
              render: (client) => (
                <Group gap="xs">
                  {client.activeMembership?.planName}
                  <MembershipBadge membership={client.activeMembership} />
                </Group>
              ),
            },
          ]}
        />
      )}
      <ClientFormModal opened={formOpened} onClose={form.close} onSaved={(client) => navigate(`/clients/${client.id}`)} />
    </Stack>
  );
};
