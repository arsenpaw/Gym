import { Avatar, Button, Card, Flex, Group, Stack, Text, Title } from '@mantine/core';
import { IconCake, IconCalendarPlus, IconLogin2, IconMail, IconPencil, IconPhone, IconTicket } from '@tabler/icons-react';
import type { ReactNode } from 'react';
import type { ClientDetailsResponse } from '../../api/generated/model';
import { formatDate } from '../../lib/format';
import { MembershipBadge } from './MembershipBadge';

type Props = {
  client: ClientDetailsResponse;
  checkingIn: boolean;
  onEdit: () => void;
  onSell: () => void;
  onCheckIn: () => void;
};

const Detail = ({ icon, label, children }: { icon: ReactNode; label: string; children: ReactNode }) => (
  <Group gap={6} wrap="nowrap" aria-label={label}>
    <Text c="dimmed" display="flex">{icon}</Text>
    <Text size="sm">{children}</Text>
  </Group>
);

export const ClientHeader = ({ client, checkingIn, onEdit, onSell, onCheckIn }: Props) => {
  const membership = client.activeMembership;
  return (
    <Card withBorder radius="md" padding="lg">
      <Group justify="space-between" align="flex-start" gap="lg">
        <Group align="flex-start" wrap="nowrap" gap="md">
          <Avatar name={client.fullName} color="initials" size="lg" radius="xl" />
          <Stack gap={8}>
            <Group gap="sm">
              <Title order={2}>{client.fullName}</Title>
              <MembershipBadge membership={membership} />
            </Group>
            <Flex wrap="wrap" columnGap="lg" rowGap={6}>
              <Detail icon={<IconMail size={16} />} label="Email">{client.email}</Detail>
              {client.phone && <Detail icon={<IconPhone size={16} />} label="Phone">{client.phone}</Detail>}
              <Detail icon={<IconCake size={16} />} label="Date of birth">
                {formatDate(client.dateOfBirth)} · {client.age} years
              </Detail>
              <Detail icon={<IconCalendarPlus size={16} />} label="Registered">Since {formatDate(client.registeredAt)}</Detail>
            </Flex>
            <Text size="sm" c={membership ? undefined : 'dimmed'}>
              {membership
                ? `${membership.planName} · valid ${formatDate(membership.startsOn)} – ${formatDate(membership.endsOn)} · ${
                    membership.visitsLeft === null ? 'unlimited visits' : `${membership.visitsLeft} visits left`
                  }`
                : 'No active membership. Sell one to let the client check in.'}
            </Text>
          </Stack>
        </Group>
        <Group gap="sm">
          <Button variant="default" leftSection={<IconPencil size={16} />} onClick={onEdit}>Edit</Button>
          <Button variant="light" leftSection={<IconTicket size={16} />} onClick={onSell}>Sell membership</Button>
          <Button leftSection={<IconLogin2 size={16} />} loading={checkingIn} onClick={onCheckIn}>Check in</Button>
        </Group>
      </Group>
    </Card>
  );
};
