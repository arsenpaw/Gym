import type { RouteObject } from 'react-router';
import { RequireRole } from '../auth/RequireRole';
import { Role } from '../auth/roles';
import { AppLayout } from '../layout/AppLayout';
import { ForbiddenPage, NotFoundPage } from '../pages/StatusPages';
import { HomeRedirect } from './HomeRedirect';

const staff = [Role.Admin, Role.Receptionist] as const;
const everyone = [Role.Admin, Role.Receptionist, Role.Trainer] as const;
const admins = [Role.Admin] as const;

export const routes: RouteObject[] = [
  {
    element: <AppLayout />,
    children: [
      { index: true, element: <HomeRedirect /> },
      {
        element: <RequireRole allowed={staff} />,
        children: [
          { path: 'plans', lazy: async () => ({ Component: (await import('../features/plans/PlansPage')).PlansPage }) },
        ],
      },
      {
        element: <RequireRole allowed={everyone} />,
        children: [],
      },
      {
        element: <RequireRole allowed={admins} />,
        children: [],
      },
      { path: 'forbidden', element: <ForbiddenPage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
];
