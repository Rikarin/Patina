import { TestBed } from '@angular/core/testing';
import { type BrushPresetInfo, createPatinaClient } from '@patina/api-client';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';
import { GRAPH_SOCKET } from '../graph/graph-store';
import { BrushStore } from './brush-store';
import { NewBrush, PaintStore } from './paint-store';

const Chalk: BrushPresetInfo = {
  name: 'Chalk',
  source: 'starter:Chalk',
  origin: 'shipped',
  preset: { tool: 'eraser', brush: { ...NewBrush, radius: 20, flowJitter: 0.6 }, colour: '#112233', favourite: false }
};

function setup() {
  const puts: { path: string; body: unknown }[] = [];
  const fetcher = (async (input: Request) => {
    const { pathname: path } = new URL(input.url);
    if (input.method === 'PUT') {
      puts.push({ path, body: await input.json() });
      return Response.json(path.endsWith('/brushes') ? { ...Chalk, source: 'projects/crate/Chalk.ptbrush' } : {});
    }

    if (path.endsWith('/library/brushes')) {
      return Response.json({ items: [Chalk] });
    }

    return Response.json({});
  }) as unknown as typeof fetch;
  TestBed.configureTestingModule({
    providers: [
      { provide: SERVER_CONNECTION, useValue: () => Promise.resolve({ baseUrl: 'http://127.0.0.1:1', token: 't' }) },
      { provide: API_CLIENT_FACTORY, useValue: (c: never) => createPatinaClient(c, fetcher) },
      { provide: GRAPH_SOCKET, useValue: () => ({ close: () => undefined }) }
    ]
  });
  return { brushes: TestBed.inject(BrushStore), paint: TestBed.inject(PaintStore), puts };
}

describe('BrushStore', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('takes up how a project left its painting, and writes it back only once it changes and rests', async () => {
    vi.useFakeTimers();
    const { brushes, paint, puts } = setup();
    await brushes.adopt('crate', {
      recentColours: ['#aa0000'],
      palette: ['#00bb00'],
      brush: { ...NewBrush, radius: 64, aspect: 3 },
      symmetry: { kind: 'radial', axis: 'y', centre: 'model', offset: 0, count: 5 }
    });
    TestBed.tick();
    await vi.advanceTimersByTimeAsync(2000);

    expect(paint.radius()).toBe(64);
    expect(paint.brush().aspect).toBe(3);
    expect(paint.palette()).toEqual(['#00bb00']);
    expect(paint.symmetry()?.count).toBe(5);
    expect(puts).toHaveLength(0);

    paint.keepColour('#123456');
    paint.radius.set(70);
    TestBed.tick();
    await vi.advanceTimersByTimeAsync(100);
    paint.radius.set(80);
    TestBed.tick();
    expect(puts).toHaveLength(0);
    await vi.advanceTimersByTimeAsync(2000);

    expect(puts).toHaveLength(1);
    const [kept] = puts;
    expect(kept?.path).toBe('/api/v1/library/project/paint');
    expect(kept?.body).toMatchObject({
      project: 'crate',
      paint: { palette: ['#00bb00', '#123456'], brush: { radius: 80, aspect: 3 }, recentColours: ['#aa0000'] }
    });
  });

  it('takes up a preset whole: its tool, its brush and size, and its colour', async () => {
    const { brushes, paint } = setup();
    await brushes.adopt('crate', null);
    const [chalk] = brushes.presets();
    brushes.use(chalk ?? Chalk);

    expect(paint.tool()).toBe('eraser');
    expect(paint.radius()).toBe(20);
    expect(paint.brush().flowJitter).toBe(0.6);
    expect(paint.colour()).toBe('#112233');
    expect(brushes.current()?.name).toBe('Chalk');
  });

  it('opens a preset before any texturing document without wiping the colours, palette and symmetry kept (#222)', async () => {
    vi.useFakeTimers();
    const { brushes, paint, puts } = setup();
    const kept = {
      recentColours: ['#aa0000'],
      palette: ['#00bb00'],
      brush: { ...NewBrush, radius: 64 },
      symmetry: { kind: 'mirror' as const, axis: 'x' as const, centre: 'model' as const, offset: 0, count: 2 }
    };
    await brushes.useFile('crate', 'starter:Chalk', kept);
    TestBed.tick();
    await vi.advanceTimersByTimeAsync(2000);

    expect(paint.radius()).toBe(20);
    expect(puts).toHaveLength(1);
    expect(puts[0]?.body).toMatchObject({
      project: 'crate',
      paint: { recentColours: ['#aa0000'], palette: ['#00bb00'], brush: { radius: 20 }, symmetry: kept.symmetry }
    });

    // The texturing document that opens next takes up what the project holds, which the preset left alone.
    await brushes.adopt('crate', kept);
    expect(paint.palette()).toEqual(['#00bb00']);
  });

  it('favours a shipped preset through the Server, and follows the copy it keeps', async () => {
    const { brushes, puts } = setup();
    await brushes.adopt('crate', null);
    brushes.use(Chalk);
    await brushes.toggleFavourite(Chalk);

    expect(puts.at(-1)).toMatchObject({
      path: '/api/v1/library/brushes',
      body: { project: 'crate', source: 'starter:Chalk', preset: { favourite: true } }
    });
    expect(brushes.chosen()).toBe('projects/crate/Chalk.ptbrush');
  });
});
