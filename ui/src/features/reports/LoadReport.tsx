import { BarChart } from '@mantine/charts';
import { Accordion, Group, Paper, SimpleGrid, Stack, Text, Title } from '@mantine/core';
import { DatePickerInput, type DatesRangeValue } from '@mantine/dates';
import { DataTable } from 'mantine-datatable';
import { useMemo, useState } from 'react';
import { useReportsLoad } from '../../api/generated/endpoints/reports/reports';
import dayjs from '../../lib/dayjs';
import { DATE_FORMAT } from '../../lib/dates';
import { loadRangeError } from './loadRange';

const palette = ['teal.6', 'blue.6', 'violet.6', 'orange.6', 'pink.6', 'cyan.6', 'lime.6', 'grape.6', 'indigo.6', 'red.6'];

const nextSevenDays = (): DatesRangeValue<string> => [dayjs().format(DATE_FORMAT), dayjs().add(6, 'day').format(DATE_FORMAT)];

export const LoadReport = () => {
  const [range, setRange] = useState<DatesRangeValue<string>>(nextSevenDays);
  const [from, to] = range;
  const error = loadRangeError(from, to);
  const report = useReportsLoad({ From: from ?? undefined, To: to ?? undefined }, { query: { enabled: Boolean(from && to) && !error } });
  const days = useMemo(() => report.data?.days ?? [], [report.data]);
  const { roomSeries, trainerSeries, roomData, trainerData } = useMemo(() => {
    const rooms = new Map(days.flatMap((d) => d.rooms.map((r) => [r.roomId, r.roomName] as const)));
    const trainers = new Map(days.flatMap((d) => d.trainers.map((t) => [t.trainerId, t.trainerName] as const)));
    return {
      roomSeries: [...rooms].map(([id, name], i) => ({ name: id, label: name, color: palette[i % palette.length] })),
      trainerSeries: [...trainers].map(([id, name], i) => ({ name: id, label: name, color: palette[i % palette.length] })),
      roomData: days.map((d) => ({ day: dayjs(d.date).format('ddd D'), ...Object.fromEntries(d.rooms.map((r) => [r.roomId, r.utilizationPercent])) })),
      trainerData: days.map((d) => ({ day: dayjs(d.date).format('ddd D'), ...Object.fromEntries(d.trainers.map((t) => [t.trainerId, t.bookedHours])) })),
    };
  }, [days]);

  return (
    <Stack>
      <Group>
        <DatePickerInput type="range" label="Period" value={range} onChange={setRange} valueFormat="D MMM YYYY" w={320} error={error} allowSingleDateInRange />
      </Group>
      <SimpleGrid cols={{ base: 1, lg: 2 }}>
        <Paper withBorder p="md" radius="md">
          <Title order={5} mb="sm">Room utilization, %</Title>
          <BarChart h={260} data={roomData} dataKey="day" series={roomSeries} withLegend />
        </Paper>
        <Paper withBorder p="md" radius="md">
          <Title order={5} mb="sm">Trainer hours</Title>
          <BarChart h={260} data={trainerData} dataKey="day" series={trainerSeries} withLegend />
        </Paper>
      </SimpleGrid>
      <Accordion variant="separated" multiple>
        {days.map((day) => (
          <Accordion.Item key={day.date} value={day.date}>
            <Accordion.Control>
              <Group justify="space-between" pr="md">
                <Text fw={500}>{dayjs(day.date).format('dddd, D MMMM')}</Text>
                <Text size="sm" c="dimmed">{day.rooms.reduce((sum, r) => sum + r.sessions, 0)} sessions</Text>
              </Group>
            </Accordion.Control>
            <Accordion.Panel>
              <SimpleGrid cols={{ base: 1, md: 2 }}>
                <DataTable
                  idAccessor="trainerId"
                  records={day.trainers}
                  noRecordsText="No sessions"
                  minHeight={80}
                  columns={[
                    { accessor: 'trainerName', title: 'Trainer' },
                    { accessor: 'sessions', title: 'Sessions', textAlign: 'right' },
                    { accessor: 'bookedHours', title: 'Hours', textAlign: 'right' },
                    { accessor: 'clientsBooked', title: 'Clients', textAlign: 'right' },
                  ]}
                />
                <DataTable
                  idAccessor="roomId"
                  records={day.rooms}
                  noRecordsText="No sessions"
                  minHeight={80}
                  columns={[
                    { accessor: 'roomName', title: 'Room' },
                    { accessor: 'occupiedHours', title: 'Hours', textAlign: 'right' },
                    { accessor: 'bookedPlaces', title: 'Booked', textAlign: 'right', render: (r) => `${r.bookedPlaces} / ${r.totalPlaces}` },
                    { accessor: 'utilizationPercent', title: 'Load', textAlign: 'right', render: (r) => `${Math.round(r.utilizationPercent)}%` },
                  ]}
                />
              </SimpleGrid>
            </Accordion.Panel>
          </Accordion.Item>
        ))}
      </Accordion>
    </Stack>
  );
};
