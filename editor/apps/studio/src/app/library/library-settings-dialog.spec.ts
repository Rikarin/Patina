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

async function render(post: (path: string, init?: unknown) => Promise<unknown>) {
  const POST = vi.fn(post);
  const GET = vi.fn(() =>
    Promise.resolve({ data: { path: 'C:\\Art\\Game.ptlib\\.ptcache', bytes: 2048 }, response: { ok: true } })
  );
  const report = vi.fn();
  TestBed.configureTestingModule({
    providers: [
      {
        provide: LibraryStore,
        useValue: { state: signal({ kind: 'ready', info }), api: () => Promise.resolve({ GET, POST }) }
      },
      { provide: Activity, useValue: { report } }
    ]
  });
  const fixture = TestBed.createComponent(LibrarySettingsDialog);
  fixture.componentRef.setInput('open', true);
  fixture.detectChanges();
  await fixture.whenStable();
  return { fixture, POST, report };
}

const byId = (id: string) => document.querySelector<HTMLElement>(`[data-testid="${id}"]`);
const ok = (data: unknown) => Promise.resolve({ data, response: { ok: true } });

describe('LibrarySettingsDialog', () => {
  it('shows the settings no other view shows, and the cache’s size (#192)', async () => {
    const { fixture } = await render(() => ok(null));
    await new Promise(resolve => setTimeout(resolve)); // the cache is read once the dialog is open
    await fixture.whenStable();

    expect(byId('library-settings-name')?.textContent.trim()).toBe('Game');
    expect(byId('library-settings-density')?.textContent.trim()).toBe('1024 texels per metre');
    expect(byId('library-settings-channels')?.textContent.trim()).toBe('baseColor, roughness');
    expect(byId('library-settings-targets')?.textContent.trim()).toBe('None');
    expect(byId('library-settings-cache-size')?.textContent.trim()).toBe('2.0 KB');
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

  it('prunes for the days chosen and cleans, saying what each freed', async () => {
    const { fixture, POST } = await render(path =>
      ok(path.endsWith('/prune') ? { removed: 4, bytes: 3 * 1024 * 1024 } : { removed: 0, bytes: 0 })
    );

    byId('library-settings-prune')?.click();
    await fixture.whenStable();
    expect(POST).toHaveBeenCalledWith('/api/v1/library/cache/prune', { body: { days: 30 } });
    expect(byId('library-settings-tidied')?.textContent.trim()).toBe('Pruned 4 cache entries, 3.0 MB.');

    byId('library-settings-clean')?.click();
    await fixture.whenStable();
    expect(byId('library-settings-tidied')?.textContent.trim()).toBe('There were no copies of moved paint to remove.');
  });

  it('says why when the engine refuses', async () => {
    const { fixture } = await render(() => Promise.reject(new Error('A bake or build is running.')));

    byId('library-settings-prune')?.click();
    await fixture.whenStable();

    expect(byId('library-settings-failure')?.textContent.trim()).toBe('A bake or build is running.');
  });
});
