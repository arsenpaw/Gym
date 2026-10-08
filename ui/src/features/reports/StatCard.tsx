import { Paper, Text } from '@mantine/core';

export const StatCard = ({ label, value }: { label: string; value: string | number }) => (
  <Paper withBorder p="md" radius="md">
    <Text size="xs" c="dimmed" tt="uppercase" fw={700}>{label}</Text>
    <Text fw={700} size="xl" mt={4}>{value}</Text>
  </Paper>
);
