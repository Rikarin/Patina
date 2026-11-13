import { TestBed } from '@angular/core/testing';
import { type ViewportHighlight, createPatinaClient } from '@patina/api-client';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';
import { IsolationStore } from './isolation-store';
import { DefaultCamera } from './orbit';

function setup() {
  const marks: ViewportHighlight[] = [];
  const fetcher = (async (input: Request) => {
    if (input.method === 'PUT' && new URL(input.url).pathname.endsWith('/viewport/highlight')) {
      const mark = (await input.json()) as ViewportHighlight;
      marks.push(mark);
      return Response.json({ mesh: '/a/crate.glb', camera: DefaultCamera, highlight: mark });
    }

    return new Response(null, { status: 404 });
  }) as unknown as typeof fetch;
  TestBed.configureTestingModule({
    providers: [
      { provide: SERVER_CONNECTION, useValue: () => Promise.resolve({ baseUrl: 'http://127.0.0.1:1', token: 't' }) },
      { provide: API_CLIENT_FACTORY, useValue: (c: never) => createPatinaClient(c, fetcher) }
    ]
  });
  return { store: TestBed.inject(IsolationStore), marks };
}

describe('IsolationStore marks', () => {
  it('marks what is pointed at on the model, once for each change, and takes the mark off (#136)', async () => {
    const { store, marks } = setup();

    await store.mark({ parts: [], materials: ['Wood'] });
    await store.mark({ parts: [], materials: ['Wood'] });
    await store.unmark();

    expect(marks).toEqual([
      { parts: [], materials: ['Wood'] },
      { parts: [], materials: [] }
    ]);
    expect(store.marked()).toEqual({ parts: [], materials: [] });
  });

  it('sends only where a pointer run down a list stopped, when it outruns the Server', async () => {
    const { store, marks } = setup();

    void store.mark({ parts: ['lid'], materials: [] });
    void store.mark({ parts: ['body'], materials: [] });
    await store.mark({ parts: ['handle'], materials: [] });

    expect(marks.at(-1)).toEqual({ parts: ['handle'], materials: [] });
    expect(marks.length).toBeLessThanOrEqual(2);
  });
});
