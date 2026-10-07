import dayjs from './dayjs';

export const DATE_FORMAT = 'YYYY-MM-DD';

export const parseIsoDate = (input: string): string | null => {
  const value = input.trim();
  return /^\d{4}-\d{2}-\d{2}$/.test(value) && dayjs(value).format(DATE_FORMAT) === value ? value : null;
};

export const today = () => dayjs().format(DATE_FORMAT);
