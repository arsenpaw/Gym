import { useAuth0 } from '@auth0/auth0-react';
import { ActionIcon, AppShell, Avatar, Burger, Group, Menu, NavLink, Text, Title, UnstyledButton, useMantineColorScheme } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { IconBarbell, IconLogout, IconMoon, IconSun } from '@tabler/icons-react';
import { Outlet, NavLink as RouterNavLink } from 'react-router';
import { hasAnyRole } from '../auth/roles';
import { useRoles } from '../auth/useRoles';
import { navItems } from './navigation';

export const AppLayout = () => {
  const [opened, { toggle, close }] = useDisclosure();
  const { user, logout } = useAuth0();
  const { roles } = useRoles();
  const { colorScheme, toggleColorScheme } = useMantineColorScheme();
  const visible = navItems.filter((item) => hasAnyRole(roles, item.roles));

  return (
    <AppShell header={{ height: 60 }} navbar={{ width: 240, breakpoint: 'sm', collapsed: { mobile: !opened } }} padding="lg">
      <AppShell.Header>
        <Group h="100%" px="md" justify="space-between">
          <Group>
            <Burger opened={opened} onClick={toggle} hiddenFrom="sm" size="sm" aria-label="Toggle navigation" />
            <IconBarbell size={28} color="var(--mantine-color-teal-6)" />
            <Title order={3}>Fitness Club</Title>
          </Group>
          <Group>
            <ActionIcon variant="default" size="lg" aria-label="Toggle color scheme" onClick={toggleColorScheme}>
              {colorScheme === 'dark' ? <IconSun size={18} /> : <IconMoon size={18} />}
            </ActionIcon>
            <Menu position="bottom-end" withArrow>
              <Menu.Target>
                <UnstyledButton aria-label="Account menu">
                  <Avatar src={user?.picture} alt={user?.name} radius="xl" />
                </UnstyledButton>
              </Menu.Target>
              <Menu.Dropdown>
                <Menu.Label>
                  <Text size="sm" fw={500}>{user?.name}</Text>
                  <Text size="xs" c="dimmed">{roles.join(', ') || 'No role'}</Text>
                </Menu.Label>
                <Menu.Item leftSection={<IconLogout size={16} />} onClick={() => logout({ logoutParams: { returnTo: window.location.origin } })}>
                  Sign out
                </Menu.Item>
              </Menu.Dropdown>
            </Menu>
          </Group>
        </Group>
      </AppShell.Header>
      <AppShell.Navbar p="sm">
        {visible.map((item) => (
          <NavLink key={item.to} component={RouterNavLink} to={item.to} label={item.label} leftSection={<item.icon size={18} />} onClick={close} />
        ))}
      </AppShell.Navbar>
      <AppShell.Main>
        <Outlet />
      </AppShell.Main>
    </AppShell>
  );
};
