import { screen, waitFor } from '@testing-library/react';
import { HttpResponse, http } from 'msw';
import {
  getClientMessagesListMockHandler,
  getClientMessagesPreviewMockHandler,
  getClientMessagesSendMockHandler,
} from '../../api/generated/endpoints/client-messages/client-messages.msw';
import type { ClientMessagePreviewResponse, ClientMessageRequest, NotificationResponse } from '../../api/generated/model';
import dayjs from '../../lib/dayjs';
import { signInAs } from '../../test/auth';
import { ids } from '../../test/fixtures';
import { renderPage } from '../../test/render';
import { server } from '../../test/server';
import { ClientMessagesCard } from './ClientMessagesCard';

const previews: Record<string, ClientMessagePreviewResponse> = {
  ExpiryReminder: { template: 'ExpiryReminder', recipient: 'olena@example.com', subject: 'Your membership expires soon', html: '<p>Dear Olena, your membership ends in 4 days</p>' },
  Promotion: { template: 'Promotion', recipient: 'olena@example.com', subject: '10% off your next membership', html: '<p>10% OFF</p>' },
};

const message = (overrides: Partial<NotificationResponse> = {}): NotificationResponse => ({
  id: ids.notification,
  clientId: ids.client,
  membershipId: null,
  type: 'Promotion',
  channel: 'Email',
  recipient: 'olena@example.com',
  subject: '10% off your next membership',
  message: 'Dear Olena, this month only: get 10% off any membership.',
  status: 'Sent',
  createdAt: dayjs().format(),
  sentAt: dayjs().format(),
  failureReason: null,
  ...overrides,
});

const previewHandler = getClientMessagesPreviewMockHandler(({ request }) => previews[new URL(request.url).searchParams.get('Template') ?? '']);

const renderCard = () => renderPage(<ClientMessagesCard clientId={ids.client} />);

describe('ClientMessagesCard', () => {
  beforeEach(() => signInAs('Receptionist'));

  it('previews the reminder and switches to the promotion', async () => {
    server.use(previewHandler, getClientMessagesListMockHandler([]));
    const { user } = renderCard();

    expect(await screen.findByText('Your membership expires soon')).toBeInTheDocument();
    expect(screen.getByText('olena@example.com')).toBeInTheDocument();
    expect(screen.getByTitle('Email preview')).toHaveAttribute('srcdoc', previews.ExpiryReminder.html);

    await user.click(screen.getByRole('radio', { name: 'Promotion' }));

    expect(await screen.findByText('10% off your next membership')).toBeInTheDocument();
    expect(screen.getByTitle('Email preview')).toHaveAttribute('srcdoc', previews.Promotion.html);
  });

  it('explains why the email cannot be sent and disables Send', async () => {
    server.use(
      http.get('*/api/clients/:id/messages/preview', () =>
        HttpResponse.json({ title: 'Invalid request', detail: 'The client has no active membership to remind about.' }, { status: 400 }),
      ),
      getClientMessagesListMockHandler([]),
    );
    renderCard();

    expect(await screen.findByText('The client has no active membership to remind about.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Send email' })).toBeDisabled();
  });

  it('asks for confirmation, sends the selected template and refreshes the history', async () => {
    let sent: ClientMessageRequest | undefined;
    let listLoads = 0;
    server.use(
      previewHandler,
      getClientMessagesListMockHandler(() => {
        listLoads += 1;
        return listLoads === 1 ? [] : [message()];
      }),
      getClientMessagesSendMockHandler(async ({ request }) => {
        sent = (await request.json()) as ClientMessageRequest;
        return message();
      }),
    );
    const { user } = renderCard();

    await user.click(await screen.findByRole('radio', { name: 'Promotion' }));
    await screen.findByText('10% off your next membership');
    await user.click(screen.getByRole('button', { name: 'Send email' }));
    expect(await screen.findByText('Send this email to olena@example.com?')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Send' }));

    expect(await screen.findByText('Email sent')).toBeInTheDocument();
    expect(sent).toEqual({ template: 'Promotion' });
    await waitFor(() => expect(listLoads).toBe(2));
  });

  it('shows the reason when the email could not be delivered', async () => {
    server.use(
      previewHandler,
      getClientMessagesListMockHandler([]),
      getClientMessagesSendMockHandler(message({ status: 'Failed', sentAt: null, failureReason: 'SendGrid rejected the email (401 Unauthorized)' })),
    );
    const { user } = renderCard();

    await user.click(await screen.findByRole('button', { name: 'Send email' }));
    await user.click(await screen.findByRole('button', { name: 'Send' }));

    expect(await screen.findByText('SendGrid rejected the email (401 Unauthorized)')).toBeInTheDocument();
  });

  it('lists the messages already sent to the client', async () => {
    server.use(
      previewHandler,
      getClientMessagesListMockHandler([message({ type: 'MembershipExpiring', subject: 'Your membership expires soon', status: 'Failed', sentAt: null })]),
    );
    renderCard();

    expect(await screen.findByText('Expiry notice')).toBeInTheDocument();
    expect(screen.getByText('Failed')).toBeInTheDocument();
  });
});
