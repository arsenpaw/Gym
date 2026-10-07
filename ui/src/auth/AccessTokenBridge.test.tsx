import { render, screen, waitFor } from '@testing-library/react';
import { HttpResponse, http } from 'msw';
import { useEffect, useState } from 'react';
import { httpClient } from '../api/http';
import { fakeAccessToken, signInAs, testAuth } from '../test/auth';
import { server } from '../test/server';
import { AccessTokenBridge } from './AccessTokenBridge';

const Ping = () => {
  const [result, setResult] = useState('pending');
  useEffect(() => {
    httpClient.get('/api/ping').then(
      () => setResult('ok'),
      () => setResult('failed'),
    );
  }, []);
  return <span>{result}</span>;
};

describe('AccessTokenBridge', () => {
  it('sends the Auth0 access token on the very first request', async () => {
    signInAs('Admin');
    let authorization: string | null = null;
    server.use(
      http.get('*/api/ping', ({ request }) => {
        authorization = request.headers.get('Authorization');
        return HttpResponse.json({});
      }),
    );

    render(<AccessTokenBridge><Ping /></AccessTokenBridge>);

    expect(await screen.findByText('ok')).toBeInTheDocument();
    expect(authorization).toBe(`Bearer ${fakeAccessToken(['Admin'])}`);
  });

  it('sends the user to Auth0 login when the session can no longer be renewed', async () => {
    signInAs('Admin');
    testAuth.tokenError = Object.assign(new Error('Login required'), { error: 'login_required' });

    render(<AccessTokenBridge><Ping /></AccessTokenBridge>);

    expect(await screen.findByText('failed')).toBeInTheDocument();
    await waitFor(() => expect(testAuth.loginWithRedirect).toHaveBeenCalledTimes(1));
  });
});
