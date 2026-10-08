import { BarChart } from '@mantine/charts';
import { Group, Paper, Select, SimpleGrid, Stack } from '@mantine/core';
import { DataTable } from 'mantine-datatable';
import { useState } from 'react';
import { QueryErrorAlert } from '../../api/QueryErrorAlert';
import { useReportsRevenue } from '../../api/generated/endpoints/reports/reports';
import dayjs from '../../lib/dayjs';
import { formatDate, formatMoney } from '../../lib/format';
import { StatCard } from './StatCard';

const currentYear = dayjs().year();
const years = Array.from({ length: 6 }, (_, i) => String(currentYear - i));
const months = Array.from({ length: 12 }, (_, i) => ({ value: String(i + 1), label: dayjs().month(i).format('MMMM') }));

export const RevenueReport = () => {
  const [year, setYear] = useState(String(currentYear));
  const [month, setMonth] = useState<string | null>(null);
  const report = useReportsRevenue({ Year: Number(year), ...(month ? { Month: Number(month) } : {}) });
  const breakdown = report.data?.breakdown ?? [];
  const chartData = breakdown.map((bucket) => ({
    label: month ? dayjs(bucket.from).format('D') : dayjs(bucket.from).format('MMM'),
    total: bucket.total,
  }));
  const total = report.data?.total ?? 0;
  const payments = report.data?.paymentCount ?? 0;

  return (
    <Stack>
      <Group>
        <Select label="Year" data={years} value={year} onChange={(value) => setYear(value ?? String(currentYear))} allowDeselect={false} w={120} />
        <Select label="Month" placeholder="Whole year" data={months} value={month} onChange={setMonth} clearable w={180} />
      </Group>
      {report.isError ? (
        <QueryErrorAlert title="Could not load the report" error={report.error} />
      ) : (
        <>
          <SimpleGrid cols={{ base: 1, sm: 3 }}>
            <StatCard label="Revenue" value={formatMoney(total)} />
            <StatCard label="Payments" value={payments} />
            <StatCard label="Average payment" value={formatMoney(payments ? total / payments : 0)} />
          </SimpleGrid>
          <Paper withBorder p="md" radius="md">
            <BarChart
              h={300}
              data={chartData}
              dataKey="label"
              series={[{ name: 'total', label: 'Revenue', color: 'teal.6' }]}
              valueFormatter={formatMoney}
              tickLine="y"
            />
          </Paper>
          <DataTable
            withTableBorder
            borderRadius="md"
            striped
            minHeight={150}
            idAccessor="from"
            fetching={report.isFetching}
            records={breakdown}
            columns={[
              { accessor: 'from', title: month ? 'Day' : 'Month', render: (b) => (month ? formatDate(b.from) : dayjs(b.from).format('MMMM YYYY')) },
              { accessor: 'paymentCount', title: 'Payments', textAlign: 'right' },
              { accessor: 'total', title: 'Revenue', textAlign: 'right', render: (b) => formatMoney(b.total) },
            ]}
          />
        </>
      )}
    </Stack>
  );
};
