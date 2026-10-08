import { useAuth0, withAuthenticationRequired } from '@auth0/auth0-react';
import { Center, Loader } from '@mantine/core';
import { useState } from 'react';
import { createBrowserRouter, RouterProvider } from 'react-router';
import { Auth0ProviderWithConfig } from './Auth0ProviderWithConfig';
import { AuthErrorPage } from './AuthErrorPage';
import { createQueryClient } from './queryClient';
import { routes } from './routes';
import { UiProviders } from './UiProviders';

const AuthenticatedRouter = withAuthenticationRequired(
  () => {
    const [router] = useState(() => createBrowserRouter(routes));
    return <RouterProvider router={router} />;
  },
  { onRedirecting: () => <Center h="100vh"><Loader /></Center> },
);

const AuthGate = () => {
  const { error } = useAuth0();
  return error ? <AuthErrorPage error={error} /> : <AuthenticatedRouter />;
};

export const App = () => {
  const [queryClient] = useState(createQueryClient);
  return (
    <UiProviders queryClient={queryClient}>
      <Auth0ProviderWithConfig>
        <AuthGate />
      </Auth0ProviderWithConfig>
    </UiProviders>
  );
};
