import { useAuth0 } from '@auth0/auth0-react';
import { Button, Center, Group, Stack, Text, Title } from '@mantine/core';

export const AuthErrorPage = ({ error }: { error: Error }) => {
  const { loginWithRedirect, logout } = useAuth0();

  return (
    <Center h="100vh">
      <Stack align="center" gap="xs" maw={480} ta="center">
        <Title order={2}>Sign-in failed</Title>
        <Text c="dimmed">{error.message}</Text>
        <Group mt="md">
          <Button onClick={() => loginWithRedirect()}>Try again</Button>
          <Button variant="default" onClick={() => logout({ logoutParams: { returnTo: window.location.origin } })}>Sign out</Button>
        </Group>
      </Stack>
    </Center>
  );
};
