import { screen, waitFor } from '@testing-library/react';
import {
  getReportsClientActivityMockHandler,
  getReportsLoadMockHandler,
  getReportsRevenueMockHandler,
} from '../../api/generated/endpoints/reports/reports.msw';
import dayjs from '../../lib/dayjs';
import { formatMoney } from '../../lib/format';
import { signInAs } from '../../test/auth';
import { ids } from '../../test/fixtures';
import { renderRoute } from '../../test/render';
import { server } from '../../test/server';
import { text } from '../../test/text';
import { ReportsPage } from './ReportsPage';

const renderReports = (tab?: string) => renderRoute([{ path: '/reports', element: <ReportsPage /> }], tab ? `/reports?tab=${tab}` : '/reports');

describe('ReportsPage', () => {
  beforeEach(() => signInAs('Admin'));

  it('shows client activity for the last 30 days by default', async () => {
    let query: URLSearchParams | undefined;
    server.use(
      getReportsClientActivityMockHandler(({ request }) => {
        query = new URL(request.url).searchParams;
        return {
          from: query.get('From') ?? '',
          to: query.get('To') ?? '',
          clients: [
            { clientId: ids.client, fullName: 'Shevchenko Olena', age: 31, phone: '+380671234567', activeMembership: null, visitCount: 12, lastVisitAt: dayjs().format() },
            { clientId: ids.otherClient, fullName: 'Franko Ivan', age: 40, phone: '+380509998877', activeMembership: null, visitCount: 0, lastVisitAt: null },
          ],
        };
      }),
    );

    renderReports();

    expect(await screen.findByText('Shevchenko Olena')).toBeInTheDocument();
    expect(query?.get('From')).toBe(dayjs().subtract(29, 'day').format('YYYY-MM-DD'));
    expect(query?.get('To')).toBe(dayjs().format('YYYY-MM-DD'));
    expect(screen.getByText('Never')).toBeInTheDocument();
  });

  it('shows the revenue total for a chosen month', async () => {
    const searches: string[] = [];
    server.use(
      getReportsRevenueMockHandler(({ request }) => {
        const params = new URL(request.url).searchParams;
        searches.push(params.toString());
        return { year: 2026, month: params.get('Month') ? Number(params.get('Month')) : null, from: '2026-01-01', to: '2026-12-31', total: 54000, paymentCount: 45, breakdown: [] };
      }),
    );
    const { user } = renderReports('revenue');

    expect(await screen.findByText(text(formatMoney(54000)))).toBeInTheDocument();
    await user.click(screen.getByRole('combobox', { name: 'Month' }));
    await user.click(await screen.findByRole('option', { name: 'March' }));

    await waitFor(() => expect(searches).toContain(`Year=${dayjs().year()}&Month=3`));
  });

  it('shows trainer and room load per day for the next 7 days', async () => {
    let query: URLSearchParams | undefined;
    const day = dayjs().format('YYYY-MM-DD');
    server.use(
      getReportsLoadMockHandler(({ request }) => {
        query = new URL(request.url).searchParams;
        return {
          from: day,
          to: day,
          days: [
            {
              date: day,
              trainers: [{ trainerId: ids.trainer, trainerName: 'Bondar Taras', sessions: 2, bookedHours: 3, clientsBooked: 14 }],
              rooms: [{ roomId: ids.room, roomName: 'Yoga studio', sessions: 2, occupiedHours: 3, bookedPlaces: 14, totalPlaces: 24, utilizationPercent: 58.33 }],
            },
          ],
        };
      }),
    );
    const { user } = renderReports('load');

    await user.click(await screen.findByText(dayjs(day).format('dddd, D MMMM')));

    expect(await screen.findByText('Bondar Taras')).toBeInTheDocument();
    expect(screen.getByText('58%')).toBeInTheDocument();
    expect(query?.get('To')).toBe(dayjs().add(6, 'day').format('YYYY-MM-DD'));
  });
});
