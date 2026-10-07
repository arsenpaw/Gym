import { AxiosError, AxiosHeaders } from 'axios';
import { problemMessage } from './problem';

const responseError = (status: number, data: unknown) =>
  new AxiosError('failed', 'ERR_BAD_RESPONSE', undefined, undefined, {
    status,
    statusText: '',
    data,
    headers: {},
    config: { headers: new AxiosHeaders() },
  });

describe('problemMessage', () => {
  it('uses the problem detail the API sends', () => {
    expect(problemMessage(responseError(400, { title: 'Invalid request', detail: 'The session is full.' }))).toBe('The session is full.');
  });

  it('uses the detail of a conflict, such as a duplicate phone', () => {
    expect(problemMessage(responseError(409, { title: 'Conflict', detail: 'A client with this phone already exists.' }))).toBe(
      'A client with this phone already exists.',
    );
  });

  it('joins model validation errors', () => {
    const data = { title: 'One or more validation errors occurred.', errors: { Name: ['The Name field is required.'], Capacity: ['Too big.'] } };
    expect(problemMessage(responseError(400, data))).toBe('The Name field is required. Too big.');
  });

  it('falls back to a message for the status when there is no detail', () => {
    expect(problemMessage(responseError(403, ''))).toBe('You do not have permission to do this.');
  });

  it('explains a network failure', () => {
    expect(problemMessage(new AxiosError('Network Error', 'ERR_NETWORK'))).toBe('The server cannot be reached. Check your connection.');
  });

  it('has a generic message for anything else', () => {
    expect(problemMessage(new Error('boom'))).toBe('Something went wrong. Please try again.');
  });
});
