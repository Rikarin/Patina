import { TestBed } from '@angular/core/testing';
import { type JobState, createPatinaClient } from '@patina/api-client';
import { BakeStore } from '../bake/bake-store';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';
import { GRAPH_SOCKET, type GraphSocket } from '../graph/graph-store';
import { LibrarySelection } from '../library/library-selection';
import { ExportPane } from './export-pane';
import { ExportStore, requestFor, settingsFor } from './export-store';

const build = {
  id: 'b1',
  key: 'build',
  state: 'running',
  progress: 0.5,
  attempts: 0,
  created: '2026-11-05T00:00:00Z',
  build: { presets: ['unreal'] },
  cancelRequested: false
} as JobState;

const report = { projects: 1, built: 1, reused: 0, files: 2, failed: 2, folder: '/r' };

const project = {
  id: 'p',
  name: 'Crate',
  path: 'crate',
  outputs: [
    {
      name: 'crate.vxlayers',
      tool: 'layer-stack',
      key: 'k',
      status: 'failed',
      seconds: 0,
      error: 'A stroke is missing.'
    }
  ],
  exports: [
    {
      preset: 'unreal',
      status: 'failed',
      folder: null,
      files: [],
      skipped: ['T_{project}_E'],
      warnings: ['T_{project}_D: no opacity map'],
      error: 'An output it reads could not be built.'
    }
  ]
};

const unreal = {
  id: 'unreal',
  name: 'Unreal Engine',
  engine: 'Unreal Engine',
  normal: 'y-down',
  origin: 'factory',
  revision: 'r',
  padding: { mode: 'infinite', distance: 16, colour: [] },
  dither: false,
  size: null,
  filter: 'box',
  files: [
    {
      template: 'T_{project}_D',
      format: 'png8',
      compression: 'none',
      space: 'srgb',
      mips: false,
      maps: ['baseColor', 'opacity'],
      lanes: 4,
      quality: 'balanced'
    },
    {
      template: 'T_{project}_R',
      format: 'png8',
      compression: 'none',
      space: 'linear',
      mips: false,
      maps: ['roughness'],
      lanes: 1,
      quality: 'balanced'
    }
  ]
};

const formats = [
  { id: 'png8', label: 'PNG 8-bit', extension: '.png', lanes: [1, 3, 4], linear: false, container: false },
  { id: 'jpeg', label: 'JPEG 8-bit', extension: '.jpg', lanes: [1, 3], linear: false, container: false },
  { id: 'tga', label: 'TGA 8-bit', extension: '.tga', lanes: [1, 3, 4], linear: false, container: false }
];

function setup(routes: Record<string, (request: Request) => Response> = {}) {
  const all: Record<string, (request: Request) => Response> = {
    'GET jobs': () => Response.json({ jobs: [], sequence: 0 }),
    'GET export/presets': () =>
      Response.json({
        presets: [unreal],
        problems: [],
        formats,
        paddings: ['none', 'infinite', 'transparent', 'colour', 'diffusion'],
        filters: ['box', 'bilinear', 'nearest']
      }),
    'POST builds': () => Response.json(build, { status: 202 }),
    'GET builds/b1/report': () => Response.json(report),
    'GET builds/b1/report/projects': () => Response.json({ total: 1, items: [project], offset: 0, limit: 50 }),
    ...routes
  };
  const answer = (input: RequestInfo | URL) => {
    const request = input as Request;
    const key = `${request.method} ${new URL(request.url).pathname.replace('/api/v1/', '')}`;
    return Promise.resolve(all[key]?.(request) ?? Response.json({ detail: `no route for ${key}` }, { status: 500 }));
  };
  TestBed.configureTestingModule({
    imports: [ExportPane],
    providers: [
      { provide: SERVER_CONNECTION, useValue: () => Promise.resolve({ baseUrl: 'http://server', token: 'secret' }) },
      {
        provide: API_CLIENT_FACTORY,
        useValue: (connection: { baseUrl: string; token: string | null }) => createPatinaClient(connection, answer)
      },
      {
        provide: GRAPH_SOCKET,
        useValue: () => ({ onopen: null, onmessage: null, onclose: null, close: vi.fn() }) as unknown as GraphSocket
      }
    ]
  });
  const fixture = TestBed.createComponent(ExportPane);
  const page = fixture.nativeElement as HTMLElement;
  const text = (id: string) =>
    [...page.querySelectorAll(`[data-testid="${id}"]`)].map(element => element.textContent.trim());
  return {
    fixture,
    text,
    store: TestBed.inject(ExportStore),
    jobs: TestBed.inject(BakeStore),
    selection: TestBed.inject(LibrarySelection)
  };
}

async function settled(fixture: ReturnType<typeof setup>['fixture']): Promise<void> {
  await new Promise(resolve => setTimeout(resolve));
  await fixture.whenStable();
}

describe('ExportPane', () => {
  it('says why a build failed, and what a preset left out', async () => {
    const { fixture, text, store, jobs } = setup();
    await store.start({ presets: ['unreal'] });
    jobs.track({ ...build, state: 'done', progress: 1, error: "2 of this build's steps failed." });
    await settled(fixture);

    expect(text('export-report-failed')).toEqual(['2']);
    expect(text('export-report-error')).toEqual([
      'crate.vxlayers: A stroke is missing.',
      'Unreal Engine: An output it reads could not be built.'
    ]);
    expect(text('export-report-skipped')).toEqual(['Left out, no map for it: T_{project}_E']);
    expect(text('export-build-error')).toEqual(["2 of this build's steps failed."]);
    expect(
      (fixture.nativeElement as HTMLElement).querySelector('[data-testid="export-build-error"]')?.getAttribute('color')
    ).toBe('error');
  });

  it.each(['cancelled', 'failed'])('reads what a %s build finished', async state => {
    const { fixture, text, store, jobs } = setup();
    await store.start({ presets: ['unreal'] });
    jobs.track({ ...build, state });
    await settled(fixture);

    expect(text('export-report-built')).toEqual(['1']);
  });

  it('stays quiet when a build refused at the start has no report', async () => {
    const { fixture, text, store, jobs } = setup({
      'GET builds/b1/report': () => Response.json({ detail: 'no report' }, { status: 404 })
    });
    await store.start({ presets: ['unreal'] });
    jobs.track({ ...build, state: 'failed', error: 'There is no preset.' });
    await settled(fixture);

    expect(text('export-error')).toEqual([]);
    expect(text('export-build-error')).toEqual(['There is no preset.']);
  });

  it('forgets a preview when the selection changes, and when it opens again', async () => {
    const { fixture, store, selection } = setup();
    await settled(fixture);
    store.preview.set({ projects: 3 } as never);

    selection.select({ kind: 'asset', asset: { id: 'a', name: 'Rust' } } as never);
    await settled(fixture);

    expect(store.preview()).toBeNull();
  });

  it('asks before exporting the whole library, and only then starts', async () => {
    const bodies: unknown[] = [];
    const { fixture, text } = setup({
      'POST builds': request => {
        void request.json().then(body => bodies.push(body));
        return Response.json(build, { status: 202 });
      }
    });
    await settled(fixture);
    const page = fixture.nativeElement as HTMLElement;

    expect(text('export-scope')).toEqual(['Every project in the library']);
    (page.querySelector('[data-testid="export-start"]') as HTMLButtonElement).click();
    await settled(fixture);
    expect(text('export-confirm')[0]).toContain('Export every project in the library through Unreal Engine?');
    expect(bodies).toEqual([]);

    (page.querySelector('[data-testid="export-confirm-yes"]') as HTMLButtonElement).click();
    await settled(fixture);
    expect(bodies).toEqual([{ presets: ['unreal'] }]);
  });

  it('lists the files of the preset in the formats that hold their lanes', async () => {
    const { fixture } = setup();
    await settled(fixture);
    const page = fixture.nativeElement as HTMLElement;

    const rows = [...page.querySelectorAll('[data-testid="export-file"]')].map(row => row.textContent.trim());
    expect(rows[0]).toContain('T_{project}_D');
    expect(rows[0]).toContain('Base Color, Opacity');
    expect(rows[1]).toContain('Roughness');
    expect(page.querySelector('[data-testid="export-preset-info"]')?.textContent).toContain('DirectX (Y−)');
  });
});

describe('requestFor', () => {
  const settings = settingsFor(unreal);

  it('sends no options when nothing differs from the preset, so the preset keeps its key', () => {
    expect(requestFor(settings, unreal as never, {})).toEqual({ presets: ['unreal'] });
  });

  it('sends only what differs: size, padding, a file turned off, another format, the sets', () => {
    const request = requestFor(
      {
        ...settings,
        size: 2048,
        filter: 'bilinear',
        padding: 'colour',
        distance: 4,
        colour: [1, 0, 0, 1],
        dither: true,
        folder: '/out',
        files: {
          unreal: {
            'T_{project}_D': { enabled: false, format: 'png8' },
            'T_{project}_R': { enabled: true, format: 'tga' }
          }
        }
      },
      unreal,
      { projects: ['p'], sets: ['Body'] }
    );

    expect(request).toEqual({
      presets: ['unreal'],
      projects: ['p'],
      folder: '/out',
      options: {
        size: 2048,
        filter: 'bilinear',
        padding: { mode: 'colour', distance: 4, colour: [1, 0, 0, 1] },
        dither: true,
        files: [
          { template: 'T_{project}_D', enabled: false },
          { template: 'T_{project}_R', enabled: true, format: 'tga' }
        ],
        sets: ['Body']
      }
    });
  });
});
