import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { type MaterialInfo, createPatinaClient } from '@patina/api-client';
import { LibraryStore } from '../library/library-store';
import { ProjectStore } from '../library/project-store';
import { MaterialUse } from './material-use';
import { PaintStore } from './paint-store';

const redRust: MaterialInfo = {
  name: 'Red Rust',
  kind: 'material-preset',
  source: 'projects/crate/red-rust.ptpreset',
  role: 'material',
  origin: 'project',
  channels: []
};

function setup() {
  const asked: unknown[] = [];
  const fetcher = (async (input: Request) => {
    asked.push(await input.json());
    return Response.json({ graph: 'Materials/rust', parameters: { redness: '0.9' } });
  }) as unknown as typeof fetch;
  const client = createPatinaClient({ baseUrl: 'http://server', token: 't' }, fetcher);
  const paint = { addGraphLayer: vi.fn(), layers: signal([]), material: signal(null), tool: signal('brush') };
  TestBed.configureTestingModule({
    providers: [
      { provide: LibraryStore, useValue: { api: () => Promise.resolve(client) } },
      { provide: ProjectStore, useValue: { current: signal({ path: 'crate' }) } },
      { provide: PaintStore, useValue: paint }
    ]
  });
  return { using: TestBed.inject(MaterialUse), paint, asked };
}

describe('MaterialUse', () => {
  it('fills a layer with a material preset at the values it keeps (#124)', async () => {
    const { using, paint, asked } = setup();

    await using.use(redRust);

    expect(asked).toEqual([{ project: 'crate', source: redRust.source }]);
    expect(paint.addGraphLayer).toHaveBeenCalledWith('Red Rust', 'Materials/rust', { redness: '0.9' });
    expect(using.failure()).toBeNull();
  });

  it('takes no material preset up for the brush, and says why', async () => {
    const { using, paint, asked } = setup();

    await using.paintWith(redRust);

    expect(asked).toEqual([]);
    expect(paint.material()).toBeNull();
    expect(using.failure()).toContain('Red Rust');
  });
});
