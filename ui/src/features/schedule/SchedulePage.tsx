import { Badge, Button, Group, Paper, Select, Stack, Text, Title } from '@mantine/core';
import { IconPlus } from '@tabler/icons-react';
import { useMemo, useState } from 'react';
import { Calendar, Views, dayjsLocalizer, type EventProps, type View } from 'react-big-calendar';
import { useRoomsList } from '../../api/generated/endpoints/rooms/rooms';
import { useSessionsList, useSessionsMine } from '../../api/generated/endpoints/sessions/sessions';
import { useTrainersList } from '../../api/generated/endpoints/trainers/trainers';
import type { SessionSummaryResponse } from '../../api/generated/model';
import { Role, hasAnyRole } from '../../auth/roles';
import { useRoles } from '../../auth/useRoles';
import dayjs from '../../lib/dayjs';
import { rangeFromCalendar, weekRange, type CalendarRange } from './calendarRange';
import { ScheduleSessionModal, type SessionSlot } from './ScheduleSessionModal';
import { SessionDrawer } from './SessionDrawer';

const localizer = dayjsLocalizer(dayjs);

type SessionEvent = { title: string; start: Date; end: Date; resource: SessionSummaryResponse & { roomName: string } };

const eventColor = (session: SessionSummaryResponse) => {
  if (session.status === 'Cancelled') return 'var(--mantine-color-gray-5)';
  return session.type === 'Group' ? 'var(--mantine-color-teal-6)' : 'var(--mantine-color-violet-6)';
};

const SessionEventContent = ({ event }: EventProps<SessionEvent>) => (
  <Stack gap={0}>
    <Text size="xs" fw={600} td={event.resource.status === 'Cancelled' ? 'line-through' : undefined}>{event.title}</Text>
    <Text size="xs">{event.resource.roomName} · {event.resource.activeBookingCount}/{event.resource.capacity}</Text>
  </Stack>
);

export const SchedulePage = () => {
  const { roles } = useRoles();
  const isStaff = hasAnyRole(roles, [Role.Admin, Role.Receptionist]);
  const isTrainerOnly = !isStaff && hasAnyRole(roles, [Role.Trainer]);
  const [range, setRange] = useState<CalendarRange>(() => weekRange(new Date()));
  const [view, setView] = useState<View>(Views.WEEK);
  const [trainerFilter, setTrainerFilter] = useState<string | null>(null);
  const [roomFilter, setRoomFilter] = useState<string | null>(null);
  const [selectedSession, setSelectedSession] = useState<string | null>(null);
  const [newSession, setNewSession] = useState<SessionSlot | null>(null);
  const period = { From: range.from, To: range.to };
  const all = useSessionsList(period, { query: { enabled: isStaff } });
  const mine = useSessionsMine(period, { query: { enabled: isTrainerOnly } });
  const sessions = isStaff ? all : mine;
  const trainers = useTrainersList({ includeInactive: true }, { query: { enabled: isStaff } });
  const rooms = useRoomsList({ includeInactive: true });
  const roomNames = useMemo(() => new Map((rooms.data ?? []).map((r) => [r.id, r.name])), [rooms.data]);
  const events = useMemo<SessionEvent[]>(
    () =>
      (sessions.data ?? [])
        .filter((s) => (!trainerFilter || s.trainerId === trainerFilter) && (!roomFilter || s.roomId === roomFilter))
        .map((s) => ({
          title: s.title,
          start: new Date(s.start),
          end: new Date(s.end),
          resource: { ...s, roomName: roomNames.get(s.roomId) ?? 'Room' },
        })),
    [sessions.data, trainerFilter, roomFilter, roomNames],
  );

  return (
    <Stack>
      <Group justify="space-between">
        <Group>
          <Title order={2}>{isTrainerOnly ? 'My schedule' : 'Schedule'}</Title>
          <Badge color="teal" variant="dot">Group</Badge>
          <Badge color="violet" variant="dot">Individual</Badge>
        </Group>
        {isStaff && (
          <Group>
            <Select
              aria-label="Filter by trainer"
              placeholder="All trainers"
              clearable
              searchable
              data={(trainers.data ?? []).map((t) => ({ value: t.id, label: t.fullName }))}
              value={trainerFilter}
              onChange={setTrainerFilter}
            />
            <Select
              aria-label="Filter by room"
              placeholder="All rooms"
              clearable
              data={(rooms.data ?? []).map((r) => ({ value: r.id, label: r.name }))}
              value={roomFilter}
              onChange={setRoomFilter}
            />
            <Button leftSection={<IconPlus size={16} />} onClick={() => setNewSession({ date: '', startTime: '' })}>New session</Button>
          </Group>
        )}
      </Group>
      <Paper withBorder p="md" radius="md">
        <Calendar
          localizer={localizer}
          events={events}
          view={view}
          onView={setView}
          views={[Views.MONTH, Views.WEEK, Views.DAY, Views.AGENDA]}
          onRangeChange={(r) => setRange(rangeFromCalendar(r))}
          style={{ height: 720 }}
          step={30}
          timeslots={2}
          scrollToTime={dayjs().hour(7).minute(0).toDate()}
          components={{ event: SessionEventContent }}
          eventPropGetter={(event) => ({ style: { backgroundColor: eventColor(event.resource), border: 'none' } })}
          onSelectEvent={(event) => setSelectedSession(event.resource.id)}
          selectable={isStaff}
          onSelectSlot={(slot) => setNewSession({ date: dayjs(slot.start).format('YYYY-MM-DD'), startTime: dayjs(slot.start).format('HH:mm') })}
        />
      </Paper>
      <ScheduleSessionModal slot={newSession} onClose={() => setNewSession(null)} />
      <SessionDrawer sessionId={selectedSession} onClose={() => setSelectedSession(null)} />
    </Stack>
  );
};
