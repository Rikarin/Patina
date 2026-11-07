import { TestBed } from '@angular/core/testing';
import { ApiError, createPatinaClient } from '@patina/api-client';
import { API_CLIENT_FACTORY, SERVER_CONNECTION, SystemStatus } from './system-status';

const system = {
  version: '1.2.3',
  apiVersion: 1,
  library: '/games/Ember.ptlib',
  host: { os: 'macOS', architecture: 'Arm64', runtime: '.NET 10' },
  worker: { connected: true, version: '1.2.3' },
  textureSides: [256, 512, 1024, 2048],
  defaultTextureSide: 2048
};

function statusWith(answer: () => Promise<Response>): SystemStatus {
  TestBed.configureTestingModule({
    providers: [
      { provide: SERVER_CONNECTION, useValue: () => Promise.resolve({ baseUrl: 'http://server', token: null }) },
      {
        provide: API_CLIENT_FACTORY,
        useValue: (connection: { baseUrl: string; token: string | null }) => createPatinaClient(connection, answer)
      }
    ]
  });
  return TestBed.inject(SystemStatus);
}

describe('SystemStatus', () => {
  it('starts out connecting', () => {
    const status = statusWith(() => new Promise(() => undefined));

    expect(status.state()).toEqual({ kind: 'connecting' });
  });

  it('offers no sizes until the Server lists them, then the ones it lists', async () => {
    const status = statusWith(() => Promise.resolve(Response.json(system)));

    expect(status.textureSides()).toEqual([]);
    expect(status.defaultTextureSide()).toBeNull();

    await vi.waitFor(() => expect(status.state().kind).toBe('online'));

    expect(status.textureSides()).toEqual([256, 512, 1024, 2048]);
    expect(status.defaultTextureSide()).toBe(2048);
  });

  it('reports what the Server says', async () => {
    const status = statusWith(() => Promise.resolve(Response.json(system)));

    await vi.waitFor(() => expect(status.state().kind).toBe('online'));

    expect(status.state()).toEqual({ kind: 'online', system });
  });

  it('reports a Server that cannot be reached, with the reason', async () => {
    const status = statusWith(() => Promise.reject(new TypeError('connection refused')));

    await vi.waitFor(() => expect(status.state().kind).toBe('offline'));

    expect(status.state()).toEqual({ kind: 'offline', reason: 'connection refused' });
  });

  it('reports a Server that refuses the call', async () => {
    const status = statusWith(() => Promise.resolve(new Response(null, { status: 401 })));

    await vi.waitFor(() => expect(status.state().kind).toBe('offline'));

    expect(status.state()).toMatchObject({ reason: new ApiError(401, '401').message });
  });
});
