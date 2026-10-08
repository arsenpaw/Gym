import dayjs from './dayjs';
import { today } from './dates';

describe('today', () => {
  it('is the local date as YYYY-MM-DD, the value of a native date input', () => {
    expect(today()).toBe(dayjs().format('YYYY-MM-DD'));
    expect(today()).toMatch(/^\d{4}-\d{2}-\d{2}$/);
  });
});
