import { z } from 'zod';
import { SessionType, type ScheduleSessionRequest } from '../../api/generated/model';
import { sessionsScheduleBodyCapacityMax, sessionsScheduleBodyTitleMax } from '../../api/generated/zod/sessions/sessions.zod';
import dayjs from '../../lib/dayjs';
import { requiredText } from '../../lib/formSchemas';

export const durations = [
  { value: '30', label: '30 min' },
  { value: '45', label: '45 min' },
  { value: '60', label: '1 h' },
  { value: '90', label: '1 h 30 min' },
  { value: '120', label: '2 h' },
  { value: '180', label: '3 h' },
  { value: '240', label: '4 h' },
];

const timePattern = /^\d{2}:\d{2}$/;

export const sessionFormSchema = z
  .object({
    title: requiredText('Title', sessionsScheduleBodyTitleMax),
    type: z.enum(SessionType),
    trainerId: z.string().nullable().refine((value) => Boolean(value), 'Choose a trainer'),
    roomId: z.string().nullable().refine((value) => Boolean(value), 'Choose a room'),
    date: z.iso.date().nullable().refine((value) => Boolean(value), 'Choose a date'),
    startTime: z.string().regex(timePattern, 'Enter a start time'),
    durationMinutes: z.enum(durations.map((d) => d.value) as [string, ...string[]], { error: 'Choose a duration' }),
    capacity: z.union([z.literal(''), z.number()]),
  })
  .superRefine((values, ctx) => {
    if (!dayjs(`${values.date}T${values.startTime}`).isAfter(dayjs())) {
      ctx.addIssue({ code: 'custom', path: ['startTime'], message: 'The session must start in the future' });
    }
    const capacity = values.capacity;
    if (values.type === SessionType.Group && (capacity === '' || !Number.isInteger(capacity) || capacity < 1 || capacity > sessionsScheduleBodyCapacityMax)) {
      ctx.addIssue({ code: 'custom', path: ['capacity'], message: `Capacity must be a whole number from 1 to ${sessionsScheduleBodyCapacityMax}` });
    }
  });

export type SessionFormValues = z.infer<typeof sessionFormSchema>;

export const toScheduleRequest = (values: SessionFormValues): ScheduleSessionRequest => {
  const start = dayjs(`${values.date}T${values.startTime}`);
  return {
    title: values.title,
    type: values.type,
    trainerId: values.trainerId,
    roomId: values.roomId,
    start: start.format(),
    end: start.add(Number(values.durationMinutes), 'minute').format(),
    capacity: values.type === SessionType.Individual ? 1 : Number(values.capacity),
  };
};
