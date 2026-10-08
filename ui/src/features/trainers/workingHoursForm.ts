import { z } from 'zod';
import { DayOfWeek, type WorkingHoursRequest, type WorkingHoursResponse } from '../../api/generated/model';

export const weekDays: DayOfWeek[] = [
  DayOfWeek.Monday,
  DayOfWeek.Tuesday,
  DayOfWeek.Wednesday,
  DayOfWeek.Thursday,
  DayOfWeek.Friday,
  DayOfWeek.Saturday,
  DayOfWeek.Sunday,
];

const time = z.string().regex(/^\d{2}:\d{2}$/, 'Enter a time');

export const workingHoursFormSchema = z.object({
  hours: z.array(
    z
      .object({
        day: z.enum(DayOfWeek, { error: 'Choose a day' }).nullable().refine((value) => Boolean(value), 'Choose a day'),
        start: time,
        end: time,
      })
      .refine((row) => row.end > row.start, { message: 'End must be after start', path: ['end'] }),
  ),
});

export type WorkingHoursFormValues = z.infer<typeof workingHoursFormSchema>;

export const workingHoursDefaults = (hours: WorkingHoursResponse[]): WorkingHoursFormValues => ({
  hours: hours.map((row) => ({ day: row.day, start: row.start.slice(0, 5), end: row.end.slice(0, 5) })),
});

export const toWorkingHoursRequest = (values: WorkingHoursFormValues): WorkingHoursRequest[] =>
  values.hours.map((row) => ({ day: row.day, start: `${row.start}:00`, end: `${row.end}:00` }));

export const sortByWeekDay = (hours: WorkingHoursResponse[]) =>
  hours.toSorted((a, b) => weekDays.indexOf(a.day) - weekDays.indexOf(b.day) || a.start.localeCompare(b.start));
