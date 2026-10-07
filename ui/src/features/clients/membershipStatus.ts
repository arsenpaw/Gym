import type { ActiveMembershipResponse, MembershipResponse } from '../../api/generated/model';
import dayjs from '../../lib/dayjs';

export const EXPIRY_WARNING_DAYS = 7;

export type ActiveMembershipState =
  | { kind: 'none' }
  | { kind: 'active' | 'expiring'; daysLeft: number; endsOn: string; planName: string };

export const activeMembershipState = (
  membership: ActiveMembershipResponse | null,
  today = dayjs(),
): ActiveMembershipState => {
  if (!membership) return { kind: 'none' };
  const daysLeft = dayjs(membership.endsOn).diff(today.startOf('day'), 'day');
  return {
    kind: daysLeft <= EXPIRY_WARNING_DAYS ? 'expiring' : 'active',
    daysLeft,
    endsOn: membership.endsOn,
    planName: membership.planName,
  };
};

export type MembershipRowStatus = 'Active' | 'Upcoming' | 'Expired' | 'Cancelled';

export const membershipRowStatus = (membership: MembershipResponse, today = dayjs()): MembershipRowStatus => {
  if (membership.cancelledAt) return 'Cancelled';
  if (dayjs(membership.endsOn).isBefore(today, 'day')) return 'Expired';
  if (dayjs(membership.startsOn).isAfter(today, 'day')) return 'Upcoming';
  return 'Active';
};
