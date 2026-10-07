import { Badge } from '@mantine/core';
import type { ActiveMembershipResponse } from '../../api/generated/model';
import { formatDate } from '../../lib/format';
import { activeMembershipState } from './membershipStatus';

export const MembershipBadge = ({ membership }: { membership: ActiveMembershipResponse | null }) => {
  const state = activeMembershipState(membership);
  if (state.kind === 'none') return <Badge color="gray" variant="light">No membership</Badge>;
  if (state.kind === 'expiring') {
    return (
      <Badge color="orange" variant="light">
        {state.daysLeft === 0 ? 'Expires today' : `Expires in ${state.daysLeft} ${state.daysLeft === 1 ? 'day' : 'days'}`}
      </Badge>
    );
  }
  return <Badge color="teal" variant="light">Active until {formatDate(state.endsOn)}</Badge>;
};
