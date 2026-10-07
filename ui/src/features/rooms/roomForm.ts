import { z } from 'zod';
import { RoomsCreateBody, roomsCreateBodyCapacityMax, roomsCreateBodyNameMax } from '../../api/generated/zod/rooms/rooms.zod';
import { requiredText } from '../../lib/formSchemas';

export const roomFormSchema = RoomsCreateBody.extend({
  name: requiredText('Name', roomsCreateBodyNameMax),
  capacity: z
    .number({ error: 'Capacity is required' })
    .int()
    .min(1, 'Capacity must be at least 1')
    .max(roomsCreateBodyCapacityMax, `Capacity can be at most ${roomsCreateBodyCapacityMax}`),
});

export type RoomFormValues = z.infer<typeof roomFormSchema>;
