import { ApiError, createPatinaClient, unwrap } from './client';

describe('api-client', () => {
  it('sends the bearer token on every request', async () => {
    const seen: Request[] = [];
    const client = createPatinaClient({ baseUrl: 'http://127.0.0.1:5190', token: 't0ken' }, request => {
      seen.push(request as Request);
      return Promise.resolve(Response.json({ version: '1' }));
    });

    await client.GET('/api/v1/system');

    expect(seen[0]?.url).toBe('http://127.0.0.1:5190/api/v1/system');
    expect(seen[0]?.headers.get('Authorization')).toBe('Bearer t0ken');
  });

  it('sends no token when the Server wants none', async () => {
    const seen: Request[] = [];
    const client = createPatinaClient({ baseUrl: 'http://127.0.0.1:5190', token: null }, request => {
      seen.push(request as Request);
      return Promise.resolve(Response.json({ version: '1' }));
    });

    await client.GET('/api/v1/system');

    expect(seen[0]?.headers.has('Authorization')).toBe(false);
  });

  it('turns ProblemDetails into an ApiError with the reason', () => {
    const response = new Response(null, { status: 422 });
    expect(() => unwrap({ response, error: { detail: 'Nope.', errors: ['x'] } })).toThrow(ApiError);
    try {
      unwrap({ response, error: { detail: 'Nope.', errors: ['x'] } });
    } catch (error) {
      expect((error as ApiError).status).toBe(422);
      expect((error as ApiError).message).toBe('Nope.');
      expect((error as ApiError).errors).toEqual(['x']);
    }
  });

  it("keeps a refusal's code and the values its reason was written from", () => {
    const response = new Response(null, { status: 422 });
    const refused = (() => {
      try {
        unwrap({
          response,
          error: { detail: 'Only a paint layer.', code: 'stroke-not-paint-layer', values: { layer: 'Rust' } }
        });
      } catch (error) {
        return error as ApiError;
      }

      return null;
    })();
    expect(refused?.code).toBe('stroke-not-paint-layer');
    expect(refused?.values).toEqual({ layer: 'Rust' });
    expect(refused?.message).toBe('Only a paint layer.');
  });

  it('rejects a call the Server refuses, so a caller that does not unwrap still sees the failure', async () => {
    const client = createPatinaClient({ baseUrl: 'http://127.0.0.1:5190', token: null }, () =>
      Promise.resolve(Response.json({ title: 'Conflict', detail: 'No Worker.' }, { status: 503 }))
    );

    await expect(client.GET('/api/v1/system')).rejects.toThrow('No Worker.');
  });
});
