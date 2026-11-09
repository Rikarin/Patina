import { TestBed } from '@angular/core/testing';
import { createPatinaClient } from '@patina/api-client';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';
import { LibraryThumbnails, ThumbnailBatch } from './thumbnails';

/** A stand-in for the Server's hash: two sources that look alike, as a project and its document do, share one. */
const hash = (source: string) =>
  source
    .replace(/\/$/, '')
    .replace(/[^0-9a-f]/g, '0')
    .padEnd(64, 'a')
    .slice(0, 64);

/** A Server that draws every source but `blank`, and counts what it is asked. */
function setup() {
  const batches: string[][] = [];
  const previews: string[] = [];
  const answer = async (input: RequestInfo | URL): Promise<Response> => {
    const request = input as Request;
    const path = new URL(request.url).pathname;
    if (path.endsWith('/library/thumbnails')) {
      const { sources } = (await request.json()) as { sources: string[] };
      batches.push(sources);
      return Response.json({
        items: sources.map(source => ({ source, hash: source === 'blank' ? null : hash(source) }))
      });
    }

    if (path.includes('/previews/')) {
      previews.push(path);
      return new Response(new Blob(['png'], { type: 'image/png' }));
    }

    return Response.json({ projectCount: 0, assetCount: 0, indexing: false, id: 'x' });
  };
  TestBed.configureTestingModule({
    providers: [
      { provide: SERVER_CONNECTION, useValue: () => Promise.resolve({ baseUrl: 'http://server', token: null }) },
      {
        provide: API_CLIENT_FACTORY,
        useValue: (connection: { baseUrl: string; token: string | null }) => createPatinaClient(connection, answer)
      }
    ]
  });
  vi.stubGlobal(
    'URL',
    Object.assign(URL, { createObjectURL: () => `blob:${String(previews.length)}`, revokeObjectURL: () => undefined })
  );
  return { thumbnails: TestBed.inject(LibraryThumbnails), batches, previews };
}

describe('LibraryThumbnails', () => {
  it('asks a few at a time, in order, and each source once', async () => {
    const { thumbnails, batches } = setup();
    const sources = Array.from({ length: ThumbnailBatch + 3 }, (_, at) => `projects/p${String(at)}`);

    thumbnails.want(sources);
    thumbnails.want(sources.slice(0, 4));
    await vi.waitFor(() => expect(thumbnails.url(sources.at(-1) ?? '')).toBeDefined());

    expect(batches).toEqual([sources.slice(0, ThumbnailBatch), sources.slice(ThumbnailBatch)]);
  });

  it('knows a source with nothing to show, and fetches a picture two sources share once', async () => {
    const { thumbnails, previews } = setup();

    thumbnails.want(['blank', 'projects/a', 'projects/a/']);
    await vi.waitFor(() => expect(thumbnails.url('projects/a/')).toBeDefined());

    expect(thumbnails.url('blank')).toBeNull();
    expect(thumbnails.url('projects/a')).toMatch(/^blob:/);
    expect(previews).toHaveLength(1); // both hash the same
  });

  it('asks again for what it holds when told the library moved', async () => {
    const { thumbnails, batches } = setup();
    thumbnails.want(['projects/a']);
    await vi.waitFor(() => expect(thumbnails.url('projects/a')).toBeDefined());

    thumbnails.forget();
    await vi.waitFor(() => expect(batches).toHaveLength(2));

    expect(batches[1]).toEqual(['projects/a']);
    expect(thumbnails.url('projects/a')).toMatch(/^blob:/); // kept on screen while it is asked again
  });
});
