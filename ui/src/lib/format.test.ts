import { formatDate, formatDateTime, formatMoney, formatTimeRange } from './format';

describe('format', () => {
  it('formats money in hryvnias', () => {
    expect(formatMoney(1200)).toMatch(/1\s?200,00\s?(₴|грн)/);
  });

  it('formats dates and times for people', () => {
    expect(formatDate('2026-03-14')).toBe('14 Mar 2026');
    expect(formatDateTime('2026-03-14T18:05:00')).toBe('14 Mar 2026, 18:05');
    expect(formatTimeRange('2026-03-14T18:00:00', '2026-03-14T19:30:00')).toBe('Sat 14 Mar, 18:00 – 19:30');
  });
});
