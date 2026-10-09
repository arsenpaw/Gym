import { Group, SimpleGrid, Stack } from '@mantine/core';
import { DatePickerInput, type DatesRangeValue } from '@mantine/dates';
import { DataTable, type DataTableSortStatus } from 'mantine-datatable';
import { useMemo, useState } from 'react';
import { QueryErrorAlert } from '../../api/QueryErrorAlert';
import { useReportsClientActivity } from '../../api/generated/endpoints/reports/reports';
import type { ClientActivityItem } from '../../api/generated/model';
import dayjs from '../../lib/dayjs';
import { DATE_FORMAT } from '../../lib/dates';
import { formatDate, formatDateTime } from '../../lib/format';
import { StatCard } from './StatCard';

const lastThirtyDays = (): DatesRangeValue<string> => [dayjs().subtract(29, 'day').format(DATE_FORMAT), dayjs().format(DATE_FORMAT)];

export const ClientActivityReport = () => {
  const [range, setRange] = useState<DatesRangeValue<string>>(lastThirtyDays);
  const [sort, setSort] = useState<DataTableSortStatus<ClientActivityItem>>({ columnAccessor: 'visitCount', direction: 'desc' });
  const [from, to] = range;
  const report = useReportsClientActivity({ From: from ?? undefined, To: to ?? undefined }, { query: { enabled: Boolean(from && to) } });
  const clients = useMemo(() => report.data?.clients ?? [], [report.data]);
  const records = useMemo(() => {
    const key = sort.columnAccessor as keyof ClientActivityItem;
    const sorted = clients.toSorted((a, b) => String(a[key] ?? '').localeCompare(String(b[key] ?? ''), undefined, { numeric: true }));
    return sort.direction === 'desc' ? sorted.toReversed() : sorted;
  }, [clients, sort]);

  return (
    <Stack>
      <Group>
        <DatePickerInput type="range" label="Period" value={range} onChange={setRange} valueFormat="D MMM YYYY" w={320} allowSingleDateInRange />
      </Group>
      {report.isError ? (
        <QueryErrorAlert title="Could not load the report" error={report.error} />
      ) : (
        <>
          <SimpleGrid cols={{ base: 2, md: 4 }}>
            <StatCard label="Clients" value={clients.length} />
            <StatCard label="Visits" value={clients.reduce((sum, c) => sum + c.visitCount, 0)} />
            <StatCard label="With membership" value={clients.filter((c) => c.activeMembership).length} />
            <StatCard label="No visits" value={clients.filter((c) => c.visitCount === 0).length} />
          </SimpleGrid>
          <DataTable
            withTableBorder
            borderRadius="md"
            striped
            minHeight={200}
            idAccessor="clientId"
            fetching={report.isFetching}
            records={records}
            sortStatus={sort}
            onSortStatusChange={setSort}
            noRecordsText="No clients"
            columns={[
              { accessor: 'fullName', title: 'Client', sortable: true },
              { accessor: 'age', title: 'Age', textAlign: 'right', sortable: true },
              { accessor: 'email', title: 'Email' },
              { accessor: 'phone', title: 'Phone', render: (c) => c.phone ?? '—' },
              {
                accessor: 'activeMembership',
                title: 'Membership',
                render: (c) => (c.activeMembership ? `${c.activeMembership.planName}, until ${formatDate(c.activeMembership.endsOn)}` : '—'),
              },
              { accessor: 'visitCount', title: 'Visits', textAlign: 'right', sortable: true },
              { accessor: 'lastVisitAt', title: 'Last visit', sortable: true, render: (c) => (c.lastVisitAt ? formatDateTime(c.lastVisitAt) : 'Never') },
            ]}
          />
        </>
      )}
    </Stack>
  );
};
