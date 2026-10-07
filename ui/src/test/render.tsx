import { render } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactElement } from 'react';
import { createMemoryRouter, RouterProvider, type RouteObject } from 'react-router';
import { createQueryClient } from '../app/queryClient';
import { UiProviders } from '../app/UiProviders';

export const renderRoute = (routes: RouteObject[], initialPath: string) => {
  const queryClient = createQueryClient();
  queryClient.setDefaultOptions({ queries: { retry: false, refetchOnWindowFocus: false } });
  const router = createMemoryRouter(routes, { initialEntries: [initialPath] });
  const user = userEvent.setup();
  const view = render(
    <UiProviders queryClient={queryClient} env="test">
      <RouterProvider router={router} />
    </UiProviders>,
  );
  return { ...view, user, router, queryClient };
};

export const renderPage = (element: ReactElement, path = '/') => renderRoute([{ path: '*', element }], path);
