import { TestBed } from '@angular/core/testing';
import type { BakeOptions, ServerPreferences } from '@patina/api-client';
import { LibraryStore } from '../library/library-store';
import { EnginePreferences, sameOptions } from './engine-preferences';

describe('EnginePreferences (#189)', () => {
  const autosave = { intervalSeconds: 60, idleSeconds: 5, keep: 3 };
  const bake = { resolution: 1024, maps: ['normal'] } as unknown as BakeOptions;

  function engine(kept: ServerPreferences, refuse = false) {
    let file = kept;
    const GET = vi.fn(() => Promise.resolve({ data: file, response: { ok: true } }));
    const PUT = vi.fn((_path: string, { body }: { body: ServerPreferences }) => {
      if (refuse) {
        return Promise.resolve({ error: { detail: 'No.' }, response: { ok: false, status: 400 } });
      }

      file = body;
      return Promise.resolve({ data: file, response: { ok: true } });
    });
    TestBed.configureTestingModule({
      providers: [{ provide: LibraryStore, useValue: { api: () => Promise.resolve({ GET, PUT }) } }]
    });
    return { preferences: TestBed.inject(EnginePreferences), GET, PUT, file: () => file };
  }

  it('sends every setting it holds with a change, so autosave never drops the bake default', async () => {
    const { preferences, PUT, file } = engine({ autosave, bake });

    expect(await preferences.change({ autosave: { ...autosave, keep: 5 } })).toBe(true);

    expect(PUT).toHaveBeenCalledTimes(1);
    expect(file()).toEqual({ autosave: { ...autosave, keep: 5 }, bake });
    expect(preferences.bake()).toEqual(bake);
  });

  it('keeps bake options as the default, and goes back to Patina’s own', async () => {
    const { preferences, file } = engine({ autosave });
    await preferences.read();
    expect(preferences.bake()).toBeNull();

    await preferences.makeBakeDefault(bake);
    expect(file().bake).toEqual(bake);
    expect(preferences.bake()).toEqual(bake);

    await preferences.makeBakeDefault(null);
    expect(preferences.bake()).toBeNull();
    expect(file()).toEqual({ autosave, bake: null });
  });

  it('puts the settings back and says why when the engine refuses them', async () => {
    const { preferences } = engine({ autosave }, true);
    await preferences.read();

    expect(await preferences.makeBakeDefault(bake)).toBe(false);

    expect(preferences.bake()).toBeNull();
    expect(preferences.failure()).not.toBeNull();
  });
});

describe('sameOptions (#189)', () => {
  it('finds options the same whatever order their fields came in, and different when a value differs', () => {
    const a = { resolution: 1024, rays: { front: 0.1, back: 0.2 }, maps: ['normal', 'ao'] };
    const b = { maps: ['normal', 'ao'], rays: { back: 0.2, front: 0.1 }, resolution: 1024 };
    expect(sameOptions(a as unknown as BakeOptions, b as unknown as BakeOptions)).toBe(true);
    expect(sameOptions(a as unknown as BakeOptions, { ...b, maps: ['ao', 'normal'] } as unknown as BakeOptions)).toBe(
      false
    );
  });
});
