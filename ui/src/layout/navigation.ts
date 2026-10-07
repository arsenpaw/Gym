import {
  IconBell,
  IconCalendarEvent,
  IconChartBar,
  IconDoor,
  IconId,
  IconLogin2,
  IconTicket,
  IconUsers,
  type Icon,
} from '@tabler/icons-react';
import { Role } from '../auth/roles';

export type NavItem = { label: string; to: string; icon: Icon; roles: readonly Role[] };

const staff = [Role.Admin, Role.Receptionist] as const;

export const navItems: readonly NavItem[] = [
  { label: 'Check-in', to: '/check-in', icon: IconLogin2, roles: staff },
  { label: 'Clients', to: '/clients', icon: IconUsers, roles: staff },
  { label: 'Schedule', to: '/schedule', icon: IconCalendarEvent, roles: [Role.Admin, Role.Receptionist, Role.Trainer] },
  { label: 'Trainers', to: '/trainers', icon: IconId, roles: staff },
  { label: 'Rooms', to: '/rooms', icon: IconDoor, roles: [Role.Admin, Role.Receptionist, Role.Trainer] },
  { label: 'Membership plans', to: '/plans', icon: IconTicket, roles: staff },
  { label: 'Notifications', to: '/notifications', icon: IconBell, roles: [Role.Admin] },
  { label: 'Reports', to: '/reports', icon: IconChartBar, roles: [Role.Admin] },
];
