import { HttpResponse, http as mswHttp } from 'msw';
import { server } from '../test/server';
import { http } from './http';
import { problemMessage } from './problem';

describe('http', () => {
  it('returns the JSON body', async () => {
    server.use(mswHttp.get('*/api/rooms', () => HttpResponse.json([{ id: 'r1' }])));

    await expect(http({ url: '/api/rooms', method: 'GET' })).resolves.toEqual([{ id: 'r1' }]);
  });

  it('rejects a web page instead of handing it to the screen as data', async () => {
    server.use(mswHttp.get('*/api/rooms', () => HttpResponse.html('<!doctype html><html></html>')));

    const error = await http({ url: '/api/rooms', method: 'GET' }).catch((e: unknown) => e);

    expect(problemMessage(error)).toBe('The server sent an unexpected response. Please try again later.');
  });
});
