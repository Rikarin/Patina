import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FileChooser } from '../library/file-chooser';
import { LibraryStore } from '../library/library-store';
import { PreferencesDialog } from './preferences-dialog';

const autosave = { intervalSeconds: 60, idleSeconds: 5, keep: 3, folder: null as string | null };

async function render(chosen: string | null) {
  const PUT = vi.fn((_path: string, init: { body: { autosave: typeof autosave; cacheBudget?: number | null } }) =>
    Promise.resolve({ data: init.body, response: { ok: true } })
  );
  const GET = vi.fn((path: string) =>
    Promise.resolve(
      path === '/api/v1/preferences'
        ? { data: { autosave }, response: { ok: true } }
        : { data: { path: 'C:\\Game\\.ptcache', bytes: 0 }, response: { ok: true } }
    )
  );
  const folder = vi.fn(() => Promise.resolve(chosen));
  TestBed.configureTestingModule({
    providers: [
      {
        provide: LibraryStore,
        useValue: { state: signal({ kind: 'none' }), api: () => Promise.resolve({ GET, PUT }) }
      },
      { provide: FileChooser, useValue: { folder } }
    ]
  });
  const fixture = TestBed.createComponent(PreferencesDialog);
  fixture.componentRef.setInput('open', true);
  fixture.detectChanges();
  await vi.waitFor(() => {
    fixture.detectChanges();
    expect(byId('preferences-autosave-folder')).not.toBeNull();
  });
  return { fixture, PUT, folder };
}

const byId = (id: string) => document.querySelector<HTMLElement>(`[data-testid="${id}"]`);

describe('PreferencesDialog', () => {
  it('keeps autosaves in a folder chosen for them, and in the cache again on request (#162)', async () => {
    const { fixture, PUT } = await render('D:\\Autosaves');
    expect(byId('preferences-autosave-folder')?.textContent.trim()).toBe("The library's cache");

    byId('preferences-autosave-choose')?.click();
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(byId('preferences-autosave-folder')?.textContent.trim()).toBe('D:\\Autosaves');
    });
    expect(PUT).toHaveBeenLastCalledWith('/api/v1/preferences', {
      body: { autosave: { ...autosave, folder: 'D:\\Autosaves' } }
    });

    byId('preferences-autosave-cache')?.click();
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(byId('preferences-autosave-folder')?.textContent.trim()).toBe("The library's cache");
    });
    expect(PUT).toHaveBeenLastCalledWith('/api/v1/preferences', { body: { autosave } });
  });

  it('holds each library’s cache to a budget, and lets it grow again (#189)', async () => {
    const { fixture, PUT } = await render(null);
    expect((byId('preferences-cache-unlimited') as HTMLButtonElement).disabled).toBe(true);

    const input = byId('preferences-cache-budget')?.querySelector('input') as HTMLInputElement;
    input.value = '20';
    input.dispatchEvent(new Event('input'));
    input.dispatchEvent(new Event('change'));
    input.dispatchEvent(new Event('blur'));
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(PUT).toHaveBeenLastCalledWith('/api/v1/preferences', {
        body: { autosave, cacheBudget: 20 * 1024 * 1024 * 1024 }
      });
    });

    await vi.waitFor(() => {
      fixture.detectChanges();
      expect((byId('preferences-cache-unlimited') as HTMLButtonElement).disabled).toBe(false);
    });
    byId('preferences-cache-unlimited')?.click();
    await vi.waitFor(() =>
      expect(PUT).toHaveBeenLastCalledWith('/api/v1/preferences', { body: { autosave, cacheBudget: null } })
    );
  });

  it('changes nothing when no folder is chosen', async () => {
    const { fixture, PUT, folder } = await render(null);

    byId('preferences-autosave-choose')?.click();
    await fixture.whenStable();

    expect(folder).toHaveBeenCalled();
    expect(PUT).not.toHaveBeenCalled();
  });
});
