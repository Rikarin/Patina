import { TestBed } from '@angular/core/testing';
import { type JobState, createPatinaClient } from '@patina/api-client';
import { BakeStore } from '../bake/bake-store';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';
import { GRAPH_SOCKET, type GraphSocket } from '../graph/graph-store';
import { LibrarySelection } from '../library/library-selection';
import { ExportPane } from './export-pane';
import { ExportStore } from './export-store';

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

function setup(routes: Record<string, () => Response> = {}) {
  const all: Record<string, () => Response> = {
    'GET jobs': () => Response.json({ jobs: [], sequence: 0 }),
    'GET export/presets': () =>
      Response.json({ presets: [{ id: 'unreal', name: 'Unreal Engine', files: [] }], problems: [] }),
    'POST builds': () => Response.json(build, { status: 202 }),
    'GET builds/b1/report': () => Response.json(report),
    'GET builds/b1/report/projects': () => Response.json({ total: 1, items: [project], offset: 0, limit: 50 }),
    ...routes
  };
  const answer = (input: RequestInfo | URL) => {
    const request = input as Request;
    const key = `${request.method} ${new URL(request.url).pathname.replace('/api/v1/', '')}`;
    return Promise.resolve(all[key]?.() ?? Response.json({ detail: `no route for ${key}` }, { status: 500 }));
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
      'unreal: An output it reads could not be built.'
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
});
