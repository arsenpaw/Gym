import { screen, waitFor } from '@testing-library/react';
import { getClientsListMockHandler } from '../../api/generated/endpoints/clients/clients.msw';
import {
  getNotificationsListMockHandler,
  getNotificationsRetryMockHandler,
  getNotificationsRunMockHandler,
} from '../../api/generated/endpoints/notifications/notifications.msw';
import type { NotificationResponse } from '../../api/generated/model';
import dayjs from '../../lib/dayjs';
import { signInAs } from '../../test/auth';
import { clientSummary, ids } from '../../test/fixtures';
import { renderPage } from '../../test/render';
import { server } from '../../test/server';
import { NotificationsPage } from './NotificationsPage';

const failed: NotificationResponse = {
  id: ids.notification,
  clientId: ids.client,
  membershipId: ids.membership,
  type: 'MembershipExpiring',
  channel: 'Email',
  recipient: 'olena@example.com',
  message: 'Your membership ends in 7 days.',
  status: 'Failed',
  createdAt: dayjs().format(),
  sentAt: null,
  failureReason: 'Mailbox unavailable',
};

describe('NotificationsPage', () => {
  beforeEach(() => signInAs('Admin'));

  it('filters by status and retries a failed notification', async () => {
    const searches: string[] = [];
    let retried = '';
    server.use(
      getClientsListMockHandler([clientSummary()]),
      getNotificationsListMockHandler(({ request }) => {
        searches.push(new URL(request.url).search);
        return [failed];
      }),
      getNotificationsRetryMockHandler(({ params }) => {
        retried = String(params.id);
      }),
    );
    const { user } = renderPage(<NotificationsPage />);

    expect(await screen.findByText('Shevchenko Olena')).toBeInTheDocument();
    await user.click(screen.getByRole('radio', { name: 'Failed' }));
    await waitFor(() => expect(searches).toContain('?Status=Failed'));
    await user.click(screen.getByRole('button', { name: 'Retry notification to olena@example.com' }));

    await waitFor(() => expect(retried).toBe(ids.notification));
  });

  it('runs the job now and reports the counts', async () => {
    server.use(
      getClientsListMockHandler([]),
      getNotificationsListMockHandler([]),
      getNotificationsRunMockHandler({ created: 3, sent: 2, failed: 1 }),
    );
    const { user } = renderPage(<NotificationsPage />);

    await user.click(await screen.findByRole('button', { name: 'Run now' }));

    expect(await screen.findByText('Created 3, sent 2, failed 1')).toBeInTheDocument();
  });
});
