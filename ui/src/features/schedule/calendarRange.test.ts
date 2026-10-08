import dayjs from '../../lib/dayjs';
import { rangeFromCalendar, weekRange } from './calendarRange';

describe('calendar ranges', () => {
  it('starts the week on Monday', () => {
    const { from, to } = weekRange(new Date('2026-10-11T12:00:00'));
    expect(dayjs(from).format('YYYY-MM-DD ddd')).toBe('2026-10-05 Mon');
    expect(dayjs(to).diff(dayjs(from), 'day')).toBe(7);
  });

  it('turns the days react-big-calendar shows into a half-open range', () => {
    const week = Array.from({ length: 7 }, (_, i) => dayjs('2026-10-05').add(i, 'day').toDate());
    expect(rangeFromCalendar(week)).toEqual({ from: dayjs('2026-10-05').format(), to: dayjs('2026-10-12').format() });
    expect(rangeFromCalendar({ start: dayjs('2026-09-28').toDate(), end: dayjs('2026-11-08').toDate() })).toEqual({
      from: dayjs('2026-09-28').format(),
      to: dayjs('2026-11-09').format(),
    });
  });
});
