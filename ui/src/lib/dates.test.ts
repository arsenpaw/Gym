import { parseIsoDate } from './dates';

describe('parseIsoDate', () => {
  it('accepts only complete, real YYYY-MM-DD dates', () => {
    expect(parseIsoDate('1995-03-14')).toBe('1995-03-14');
    expect(parseIsoDate(' 1995-03-14 ')).toBe('1995-03-14');
  });

  it('never turns partial or impossible input into another date', () => {
    expect(parseIsoDate('2026-10-0')).toBeNull();
    expect(parseIsoDate('2026-10-00')).toBeNull();
    expect(parseIsoDate('2026-02-30')).toBeNull();
    expect(parseIsoDate('14.03.1995')).toBeNull();
  });
});
