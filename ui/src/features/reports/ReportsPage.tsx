import { Stack, Tabs, Title } from '@mantine/core';
import { useSearchParams } from 'react-router';
import { ClientActivityReport } from './ClientActivityReport';
import { LoadReport } from './LoadReport';
import { RevenueReport } from './RevenueReport';

const tabs = ['activity', 'revenue', 'load'] as const;
type Tab = (typeof tabs)[number];

export const ReportsPage = () => {
  const [params, setParams] = useSearchParams();
  const requested = params.get('tab');
  const tab: Tab = tabs.includes(requested as Tab) ? (requested as Tab) : 'activity';

  return (
    <Stack>
      <Title order={2}>Reports</Title>
      <Tabs value={tab} onChange={(value) => setParams({ tab: value ?? 'activity' })} keepMounted={false}>
        <Tabs.List>
          <Tabs.Tab value="activity">Client activity</Tabs.Tab>
          <Tabs.Tab value="revenue">Revenue</Tabs.Tab>
          <Tabs.Tab value="load">Trainer and room load</Tabs.Tab>
        </Tabs.List>
        <Tabs.Panel value="activity" pt="md"><ClientActivityReport /></Tabs.Panel>
        <Tabs.Panel value="revenue" pt="md"><RevenueReport /></Tabs.Panel>
        <Tabs.Panel value="load" pt="md"><LoadReport /></Tabs.Panel>
      </Tabs>
    </Stack>
  );
};
