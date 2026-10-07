import { Button, Center, Stack, Text, Title } from '@mantine/core';
import { Link } from 'react-router';

const StatusPage = ({ title, message }: { title: string; message: string }) => (
  <Center h="60vh">
    <Stack align="center" gap="xs">
      <Title order={2}>{title}</Title>
      <Text c="dimmed">{message}</Text>
      <Button component={Link} to="/" variant="light" mt="md">Go to start page</Button>
    </Stack>
  </Center>
);

export const ForbiddenPage = () => <StatusPage title="No access" message="Your role does not allow this page. Ask an administrator if you need it." />;
export const NotFoundPage = () => <StatusPage title="Page not found" message="This page does not exist." />;
