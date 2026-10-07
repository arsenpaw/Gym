import type {
  ClientDetailsResponse,
  ClientSummaryResponse,
  MembershipPlanResponse,
  RoomResponse,
  SessionResponse,
  TrainerResponse,
  TrainerSummaryResponse,
} from '../api/generated/model';
import dayjs from '../lib/dayjs';

export const ids = {
  client: '11111111-1111-4111-8111-111111111111',
  otherClient: '11111111-1111-4111-8111-222222222222',
  plan: '22222222-2222-4222-8222-222222222222',
  membership: '33333333-3333-4333-8333-333333333333',
  trainer: '44444444-4444-4444-8444-444444444444',
  room: '55555555-5555-4555-8555-555555555555',
  session: '66666666-6666-4666-8666-666666666666',
  notification: '77777777-7777-4777-8777-777777777777',
};

const inDays = (days: number) => dayjs().add(days, 'day').format('YYYY-MM-DD');

export const clientSummary = (overrides: Partial<ClientSummaryResponse> = {}): ClientSummaryResponse => ({
  id: ids.client,
  fullName: 'Shevchenko Olena',
  age: 31,
  phone: '+380671234567',
  email: 'olena@example.com',
  activeMembership: { id: ids.membership, planName: 'Monthly', startsOn: inDays(-5), endsOn: inDays(25), visitsLeft: null },
  ...overrides,
});

export const clientDetails = (overrides: Partial<ClientDetailsResponse> = {}): ClientDetailsResponse => ({
  id: ids.client,
  firstName: 'Olena',
  lastName: 'Shevchenko',
  middleName: null,
  fullName: 'Shevchenko Olena',
  dateOfBirth: '1995-03-14',
  age: 31,
  phone: '+380671234567',
  email: 'olena@example.com',
  registeredAt: dayjs().subtract(1, 'month').format(),
  activeMembership: { id: ids.membership, planName: 'Monthly', startsOn: inDays(-5), endsOn: inDays(25), visitsLeft: null },
  memberships: [
    {
      id: ids.membership,
      planId: ids.plan,
      planName: 'Monthly',
      price: 1200,
      startsOn: inDays(-5),
      endsOn: inDays(25),
      visitLimit: null,
      visitsUsed: 3,
      visitsLeft: null,
      purchasedAt: dayjs().subtract(5, 'day').format(),
      cancelledAt: null,
      isActive: true,
    },
  ],
  ...overrides,
});

export const plan = (overrides: Partial<MembershipPlanResponse> = {}): MembershipPlanResponse => ({
  id: ids.plan,
  name: 'Monthly',
  price: 1200,
  validityDays: 30,
  visitLimit: null,
  isActive: true,
  ...overrides,
});

export const room = (overrides: Partial<RoomResponse> = {}): RoomResponse => ({
  id: ids.room,
  name: 'Yoga studio',
  capacity: 20,
  isActive: true,
  ...overrides,
});

export const trainerSummary = (overrides: Partial<TrainerSummaryResponse> = {}): TrainerSummaryResponse => ({
  id: ids.trainer,
  firstName: 'Taras',
  lastName: 'Bondar',
  middleName: null,
  fullName: 'Bondar Taras',
  phone: '+380501112233',
  email: 'taras@example.com',
  specialization: 'Yoga',
  isActive: true,
  ...overrides,
});

export const trainerDetails = (overrides: Partial<TrainerResponse> = {}): TrainerResponse => ({
  ...trainerSummary(),
  identityUserId: null,
  workingHours: [{ day: 'Monday', start: '09:00:00', end: '17:00:00' }],
  clients: [],
  ...overrides,
});

export const session = (overrides: Partial<SessionResponse> = {}): SessionResponse => {
  const start = dayjs().add(1, 'day').hour(18).minute(0).second(0).millisecond(0);
  return {
    id: ids.session,
    title: 'Evening yoga',
    type: 'Group',
    trainerId: ids.trainer,
    roomId: ids.room,
    start: start.format(),
    end: start.add(1, 'hour').format(),
    capacity: 12,
    status: 'Scheduled',
    activeBookingCount: 1,
    bookings: [{ clientId: ids.client, bookedAt: dayjs().format(), cancelledAt: null }],
    ...overrides,
  };
};
