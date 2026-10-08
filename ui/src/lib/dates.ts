import dayjs from './dayjs';

export const DATE_FORMAT = 'YYYY-MM-DD';

export const today = () => dayjs().format(DATE_FORMAT);
