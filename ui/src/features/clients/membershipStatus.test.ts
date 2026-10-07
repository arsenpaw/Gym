import type { MembershipResponse } from '../../api/generated/model';
import dayjs from '../../lib/dayjs';
import { activeMembershipState, membershipRowStatus } from './membershipStatus';

const today = dayjs('2026-10-07');
const active = { id: '1', planName: 'Monthly', startsOn: '2026-10-01', endsOn: '2026-10-31', visitsLeft: null };

describe('activeMembershipState', () => {
  it('is none without a membership', () => {
    expect(activeMembershipState(null, today)).toEqual({ kind: 'none' });
  });

  it('counts the days left and warns in the last week', () => {
    expect(activeMembershipState(active, today)).toMatchObject({ kind: 'active', daysLeft: 24 });
    expect(activeMembershipState({ ...active, endsOn: '2026-10-14' }, today)).toMatchObject({ kind: 'expiring', daysLeft: 7 });
    expect(activeMembershipState({ ...active, endsOn: '2026-10-07' }, today)).toMatchObject({ kind: 'expiring', daysLeft: 0 });
  });
});

describe('membershipRowStatus', () => {
  const row = (overrides: Partial<MembershipResponse>): MembershipResponse => ({
    id: '1', planId: '2', planName: 'Monthly', price: 1200, startsOn: '2026-10-01', endsOn: '2026-10-31',
    visitLimit: null, visitsUsed: 0, visitsLeft: null, purchasedAt: '2026-10-01T10:00:00+03:00', cancelledAt: null, isActive: true,
    ...overrides,
  });

  it('tells cancelled, expired, upcoming and active memberships apart', () => {
    expect(membershipRowStatus(row({ cancelledAt: '2026-10-02T10:00:00+03:00' }), today)).toBe('Cancelled');
    expect(membershipRowStatus(row({ endsOn: '2026-10-06' }), today)).toBe('Expired');
    expect(membershipRowStatus(row({ startsOn: '2026-10-08' }), today)).toBe('Upcoming');
    expect(membershipRowStatus(row({ endsOn: '2026-10-07' }), today)).toBe('Active');
  });
});
