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
  const paint = {
    addGraphLayer: vi.fn(),
    addGraphLayerOnRegion: vi.fn(),
    layers: signal([]),
    material: signal<unknown>(null),
    tool: signal('brush'),
    state: signal({})
  };
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

  it('takes a material preset up for the brush, at the values it keeps (#311)', async () => {
    const { using, paint, asked } = setup();

    await using.paintWith(redRust);

    expect(asked).toEqual([{ project: 'crate', source: redRust.source }]);
    expect(paint.material()).toEqual({
      name: 'Red Rust',
      graph: 'Materials/rust',
      source: redRust.source,
      parameters: { redness: '0.9' }
    });
    expect(using.failure()).toBeNull();
  });

  it('fills a region of the id map with a material preset at its values (#311)', async () => {
    const { using, paint } = setup();

    expect(using.takesRegion(redRust)).toBe(true);
    await using.useOnRegion(redRust, [1, 0, 0]);

    expect(paint.addGraphLayerOnRegion).toHaveBeenCalledWith('Red Rust', 'Materials/rust', [1, 0, 0], {
      redness: '0.9'
    });
    expect(using.failure()).toBeNull();
  });
});
