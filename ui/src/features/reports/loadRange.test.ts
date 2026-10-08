import { loadRangeError } from './loadRange';

describe('loadRangeError', () => {
  it('allows up to 93 days including both ends', () => {
    expect(loadRangeError('2026-01-01', '2026-04-03')).toBeNull();
    expect(loadRangeError('2026-01-01', '2026-04-04')).toBe('Choose at most 93 days');
  });

  it('waits for both ends of the range', () => {
    expect(loadRangeError('2026-01-01', null)).toBeNull();
  });
});
