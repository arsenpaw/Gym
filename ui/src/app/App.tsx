import { withAuthenticationRequired } from '@auth0/auth0-react';
import { Center, Loader } from '@mantine/core';
import { useState } from 'react';
import { createBrowserRouter, RouterProvider } from 'react-router';
import { Auth0ProviderWithConfig } from './Auth0ProviderWithConfig';
import { createQueryClient } from './queryClient';
import { routes } from './routes';
import { UiProviders } from './UiProviders';

const router = createBrowserRouter(routes);

const AuthenticatedRouter = withAuthenticationRequired(() => <RouterProvider router={router} />, {
  onRedirecting: () => <Center h="100vh"><Loader /></Center>,
});

export const App = () => {
  const [queryClient] = useState(createQueryClient);
  return (
    <UiProviders queryClient={queryClient}>
      <Auth0ProviderWithConfig>
        <AuthenticatedRouter />
      </Auth0ProviderWithConfig>
    </UiProviders>
  );
};
