import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Activity } from '../core/activity';
import { LibrarySettingsDialog } from './library-settings-dialog';
import { LibraryStore } from './library-store';

const info = {
  id: 'l1',
  name: 'Game',
  path: 'C:\\Art\\Game.ptlib',
  schemaVersion: 1,
  texelDensity: 1024,
  channels: ['baseColor', 'roughness'],
  defaultShader: 'pbr-metal-rough',
  exportTargets: [],
  projectCount: 3,
  assetCount: 12,
  indexing: false
};

const health = {
  projects: 3,
  assets: 12,
  outputs: 2,
  errors: 1,
  groups: [
    {
      kind: 'MissingReference',
      isError: true,
      count: 51,
      findings: [{ path: 'projects/crate', detail: 'asset 42 is not in the library' }]
    },
    { kind: 'StaleOutput', isError: false, count: 1, findings: [{ path: 'projects/crate', detail: 'albedo' }] }
  ]
};

const presets = [
  { id: 'gltf', name: 'glTF' },
  { id: 'godot', name: 'Godot' }
];

async function render(
  post: (path: string, init?: unknown) => Promise<unknown>,
  put: (path: string, init: { body: Record<string, unknown> }) => Promise<unknown> = () => ok(info)
) {
  const POST = vi.fn(post);
  const PUT = vi.fn(put);
  const GET = vi.fn((path: string) =>
    path === '/api/v1/export/presets' ? ok({ presets }) : ok({ path: 'C:\\Art\\Game.ptlib\\.ptcache', bytes: 2048 })
  );
  const report = vi.fn();
  const state = signal<{ kind: 'ready'; info: typeof info }>({ kind: 'ready', info });
  const settled = vi.fn((next: typeof info) => state.set({ kind: 'ready', info: next }));
  TestBed.configureTestingModule({
    providers: [
      {
        provide: LibraryStore,
        useValue: { state, settled, api: () => Promise.resolve({ GET, POST, PUT }) }
      },
      { provide: Activity, useValue: { report } }
    ]
  });
  const fixture = TestBed.createComponent(LibrarySettingsDialog);
  fixture.componentRef.setInput('open', true);
  fixture.detectChanges();
  await fixture.whenStable();
  return { fixture, POST, PUT, report, settled };
}

const byId = (id: string) => document.querySelector<HTMLElement>(`[data-testid="${id}"]`);
const ok = (data: unknown) => Promise.resolve({ data, response: { ok: true } });

describe('LibrarySettingsDialog', () => {
  it('shows the settings no other view shows, and the cache’s size (#192)', async () => {
    const { fixture } = await render(() => ok(null));
    await new Promise(resolve => setTimeout(resolve)); // the cache is read once the dialog is open
    await fixture.whenStable();

    expect((byId('library-settings-name') as HTMLInputElement).value).toBe('Game');
    expect(byId('library-settings-channels')?.textContent.trim()).toBe('baseColor, roughness');
    const targets = [...document.querySelectorAll<HTMLElement>('[data-testid="library-settings-target"]')];
    expect(targets.map(target => target.textContent.trim())).toEqual(['glTF', 'Godot']);
    expect(byId('library-settings-cache-size')?.textContent.trim()).toBe('2.0 KB');
  });

  it('renames the library and keeps its texel density in library.yaml (#192)', async () => {
    const { fixture, PUT, settled } = await render(
      () => ok(null),
      (_path, init) => ok({ ...info, ...init.body })
    );
    const name = byId('library-settings-name') as HTMLInputElement;

    name.value = '  Ember ';
    name.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    await fixture.componentInstance.keep('Ember', 512);
    fixture.detectChanges();

    expect(PUT).toHaveBeenCalledWith('/api/v1/library/settings', { body: { name: 'Ember', texelDensity: 1024 } });
    expect(PUT).toHaveBeenCalledWith('/api/v1/library/settings', { body: { name: 'Ember', texelDensity: 512 } });
    expect(settled).toHaveBeenLastCalledWith(expect.objectContaining({ name: 'Ember', texelDensity: 512 }));
  });

  it('a blank name keeps the one the library has', async () => {
    const { fixture, PUT } = await render(() => ok(null));
    const name = byId('library-settings-name') as HTMLInputElement;

    name.value = '   ';
    name.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    expect(PUT).not.toHaveBeenCalled();
    expect(name.value).toBe('Game');
  });

  it('chooses the presets a build of the whole library writes', async () => {
    const { fixture, PUT } = await render(
      () => ok(null),
      (_path, init) => ok({ ...info, exportTargets: init.body['presets'] })
    );
    await new Promise(resolve => setTimeout(resolve)); // the presets are read once the dialog is open
    await fixture.whenStable();

    document.querySelectorAll<HTMLElement>('[data-testid="library-settings-target"] [role="checkbox"]')[1]?.click();
    await fixture.whenStable();

    expect(PUT).toHaveBeenCalledWith('/api/v1/library/export/targets', { body: { presets: ['godot'] } });
  });

  it('checks the library and lists what it found, errors first, with how many more there are', async () => {
    const { fixture, POST, report } = await render(() => ok(health));

    byId('library-settings-check-run')?.click();
    await fixture.whenStable();

    expect(POST).toHaveBeenCalledWith('/api/v1/library/check');
    expect(byId('library-settings-check-summary')?.textContent.trim()).toBe(
      'Checked 3 projects, 12 materials and 2 outputs: 1 problems to put right.'
    );
    const missing = byId('library-settings-finding-MissingReference')?.textContent ?? '';
    expect(missing).toContain('Missing materials, 51');
    expect(missing).toContain('and 50 more');
    expect(report).toHaveBeenCalledWith(expect.objectContaining({ tone: 'warning', source: 'Library' }));
  });

  it('removes the kept versions the check found nothing uses, then checks again (#292)', async () => {
    const unused = {
      ...health,
      errors: 0,
      groups: [
        { kind: 'UnusedRevision', isError: false, count: 2, findings: [{ path: '.revisions/ab/abcd', detail: '' }] }
      ]
    };
    let checks = 0;
    const { fixture, POST } = await render(path =>
      path === '/api/v1/library/check'
        ? ok(checks++ === 0 ? unused : { ...health, errors: 0, groups: [] })
        : ok({ removed: 2, bytes: 2048 })
    );

    byId('library-settings-check-run')?.click();
    await fixture.whenStable();
    expect(byId('library-settings-finding-MissingReference')).toBeNull();
    byId('library-settings-purge-revisions')?.click();
    await fixture.whenStable();

    expect(POST).toHaveBeenCalledWith('/api/v1/library/revisions/purge');
    expect(byId('library-settings-tidied')?.textContent.trim()).toBe('Removed 2 kept versions nothing used, 2.0 KB.');
    expect(byId('library-settings-purge-revisions')).toBeNull();
  });

  it('prunes for the days chosen and cleans, saying what each freed', async () => {
    const { fixture, POST } = await render(path =>
      ok(
        path.endsWith('/prune')
          ? { removed: 4, bytes: 3 * 1024 * 1024 }
          : { removed: 0, bytes: 0, pictures: 0, pictureBytes: 0, bin: null }
      )
    );

    byId('library-settings-prune')?.click();
    await fixture.whenStable();
    expect(POST).toHaveBeenCalledWith('/api/v1/library/cache/prune', { body: { days: 30 } });
    expect(byId('library-settings-tidied')?.textContent.trim()).toBe('Pruned 4 cache entries, 3.0 MB.');

    byId('library-settings-clean')?.click();
    await fixture.whenStable();
    expect(byId('library-settings-tidied')?.textContent.trim()).toBe('There was nothing to clean.');
  });

  it('says how many pictures nothing uses went to the bin (#162)', async () => {
    const { fixture } = await render(() => ok({ removed: 1, bytes: 1024, pictures: 3, pictureBytes: 2048, bin: 'b1' }));

    byId('library-settings-clean')?.click();
    await fixture.whenStable();

    expect(byId('library-settings-tidied')?.textContent.trim()).toBe(
      'Removed 1 copies of moved paint, 1.0 KB. Moved 3 pictures nothing uses to the bin, 2.0 KB.'
    );
  });

  it('says why when the engine refuses', async () => {
    const { fixture } = await render(() => Promise.reject(new Error('A bake or build is running.')));

    byId('library-settings-prune')?.click();
    await fixture.whenStable();

    expect(byId('library-settings-failure')?.textContent.trim()).toBe('A bake or build is running.');
  });
});
