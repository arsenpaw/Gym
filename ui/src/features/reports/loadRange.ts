import dayjs from '../../lib/dayjs';

export const MAX_LOAD_DAYS = 93;

export const loadRangeError = (from: string | null, to: string | null): string | null => {
  if (!from || !to) return null;
  return dayjs(to).diff(dayjs(from), 'day') + 1 > MAX_LOAD_DAYS ? `Choose at most ${MAX_LOAD_DAYS} days` : null;
};
