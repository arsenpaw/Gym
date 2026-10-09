import { Auth0Provider } from "@auth0/auth0-react";
import type { ReactNode } from "react";
import { AccessTokenBridge } from "../auth/AccessTokenBridge";

export const Auth0ProviderWithConfig = ({
  children,
}: {
  children: ReactNode;
}) => (
  <Auth0Provider
    domain={import.meta.env.VITE_AUTH0_DOMAIN}
    clientId={import.meta.env.VITE_AUTH0_CLIENT_ID}
    authorizationParams={{
      redirect_uri: window.location.origin,
      audience: import.meta.env.VITE_AUTH0_AUDIENCE,
      scope: "openid profile email",
    }}
    cacheLocation="memory"
    useRefreshTokens
  >
    <AccessTokenBridge>{children}</AccessTokenBridge>
  </Auth0Provider>
);
