import dayjs from '../../lib/dayjs';

export type CalendarRange = { from: string; to: string };

export const weekRange = (date: Date): CalendarRange => {
  const start = dayjs(date).startOf('week');
  return { from: start.format(), to: start.add(7, 'day').format() };
};

export const rangeFromCalendar = (range: Date[] | { start: Date; end: Date }): CalendarRange => {
  const [first, last] = Array.isArray(range) ? [range[0], range[range.length - 1]] : [range.start, range.end];
  return { from: dayjs(first).startOf('day').format(), to: dayjs(last).add(1, 'day').startOf('day').format() };
};
