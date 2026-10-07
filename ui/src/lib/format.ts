import dayjs from './dayjs';

export const CURRENCY = 'UAH';

const money = new Intl.NumberFormat('uk-UA', { style: 'currency', currency: CURRENCY });

export const formatMoney = (amount: number) => money.format(amount);
export const formatDate = (value: string) => dayjs(value).format('D MMM YYYY');
export const formatDateTime = (value: string) => dayjs(value).format('D MMM YYYY, HH:mm');
export const formatTime = (value: string) => dayjs(value).format('HH:mm');
export const formatTimeRange = (start: string, end: string) =>
  `${dayjs(start).format('ddd D MMM, HH:mm')} – ${dayjs(end).format('HH:mm')}`;
