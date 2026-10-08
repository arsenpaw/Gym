import { Alert } from '@mantine/core';
import { IconAlertTriangle } from '@tabler/icons-react';
import { problemMessage } from './problem';

export const QueryErrorAlert = ({ title, error }: { title: string; error: unknown }) => (
  <Alert color="red" variant="light" title={title} icon={<IconAlertTriangle size={18} />}>
    {problemMessage(error)}
  </Alert>
);
