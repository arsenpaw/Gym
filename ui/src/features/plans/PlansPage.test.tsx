import { screen, waitFor, within } from '@testing-library/react';
import { HttpResponse, http } from 'msw';
import {
  getMembershipPlansCreateMockHandler,
  getMembershipPlansDeactivateMockHandler,
  getMembershipPlansListMockHandler,
} from '../../api/generated/endpoints/membership-plans/membership-plans.msw';
import type { MembershipPlanRequest, MembershipPlanResponse } from '../../api/generated/model';
import { formatMoney } from '../../lib/format';
import { signInAs } from '../../test/auth';
import { renderPage } from '../../test/render';
import { server } from '../../test/server';
import { text } from '../../test/text';
import { PlansPage } from './PlansPage';

const monthly: MembershipPlanResponse = { id: '11111111-1111-4111-8111-111111111111', name: 'Monthly', price: 1200, validityDays: 30, visitLimit: null, isActive: true };

describe('PlansPage', () => {
  it('lists plans with price and unlimited visits, read-only for receptionists', async () => {
    signInAs('Receptionist');
    server.use(getMembershipPlansListMockHandler([monthly]));

    renderPage(<PlansPage />);

    expect(await screen.findByText('Monthly')).toBeInTheDocument();
    expect(screen.getByText(text(formatMoney(1200)))).toBeInTheDocument();
    expect(screen.getByText('Unlimited')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'New plan' })).not.toBeInTheDocument();
  });

  it('asks for inactive plans when the switch is on', async () => {
    signInAs('Admin');
    const requests: string[] = [];
    server.use(
      getMembershipPlansListMockHandler(({ request }) => {
        requests.push(new URL(request.url).search);
        return [monthly];
      }),
    );
    const { user } = renderPage(<PlansPage />);

    await user.click(await screen.findByRole('switch', { name: 'Show inactive' }));

    await waitFor(() => expect(requests).toContain('?includeInactive=true'));
  });

  it('creates an unlimited plan with a null visit limit', async () => {
    signInAs('Admin');
    let sent: MembershipPlanRequest | undefined;
    server.use(
      getMembershipPlansListMockHandler([]),
      getMembershipPlansCreateMockHandler(async ({ request }) => {
        sent = (await request.json()) as MembershipPlanRequest;
        return { ...monthly, ...sent, visitLimit: null };
      }),
    );
    const { user } = renderPage(<PlansPage />);

    await user.click(await screen.findByRole('button', { name: 'New plan' }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Save' }));
    expect(await within(dialog).findByText('Name is required')).toBeInTheDocument();
    expect(within(dialog).getByText('Price is required')).toBeInTheDocument();

    await user.type(within(dialog).getByLabelText(/Name/), 'Monthly');
    await user.type(within(dialog).getByLabelText(/Price/), '1200');
    await user.click(within(dialog).getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(sent).toEqual({ name: 'Monthly', price: 1200, validityDays: 30, visitLimit: null }));
    expect(await screen.findByText('Plan created')).toBeInTheDocument();
  });

  it('asks for confirmation before deactivating a plan', async () => {
    signInAs('Admin');
    let deactivated = false;
    server.use(
      getMembershipPlansListMockHandler([monthly]),
      getMembershipPlansDeactivateMockHandler(() => {
        deactivated = true;
      }),
    );
    const { user } = renderPage(<PlansPage />);

    await user.click(await screen.findByRole('button', { name: 'Deactivate Monthly' }));
    await user.click(await screen.findByRole('button', { name: 'Deactivate' }));

    await waitFor(() => expect(deactivated).toBe(true));
  });

  it('sends one request when Save is double-clicked', async () => {
    signInAs('Admin');
    let posts = 0;
    server.use(
      getMembershipPlansListMockHandler([]),
      http.post('*/api/membership-plans', async () => {
        posts += 1;
        await new Promise((resolve) => setTimeout(resolve, 50));
        return HttpResponse.json(monthly, { status: 201 });
      }),
    );
    const { user } = renderPage(<PlansPage />);

    await user.click(await screen.findByRole('button', { name: 'New plan' }));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByLabelText(/Name/), 'Monthly');
    await user.type(within(dialog).getByLabelText(/Price/), '1200');
    await user.dblClick(within(dialog).getByRole('button', { name: 'Save' }));

    expect(await screen.findByText('Plan created')).toBeInTheDocument();
    expect(posts).toBe(1);
  });
});
