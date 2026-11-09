import { TestBed } from '@angular/core/testing';
import { DefaultPreferences, Preferences, readPreferences } from './preferences';

/** A media query a test can change, as a system switching to dark does. */
function mediaQuery(dark: boolean) {
  const listeners = new Set<(event: MediaQueryListEvent) => void>();
  const query = {
    matches: dark,
    addEventListener: (_type: string, listener: (event: MediaQueryListEvent) => void) => listeners.add(listener),
    removeEventListener: (_type: string, listener: (event: MediaQueryListEvent) => void) => listeners.delete(listener),
    change(next: boolean) {
      query.matches = next;
      listeners.forEach(listener => listener({ matches: next } as MediaQueryListEvent));
    }
  };
  return query;
}

describe('Preferences', () => {
  beforeEach(() => {
    const held = new Map<string, string>();
    vi.stubGlobal('localStorage', {
      getItem: (key: string) => held.get(key) ?? null,
      setItem: (key: string, value: string) => held.set(key, value),
      removeItem: (key: string) => held.delete(key)
    });
    document.documentElement.classList.add('dark');
  });

  afterEach(() => vi.unstubAllGlobals());

  it('reads a file a person may have edited, keeping only what it knows', () => {
    expect(readPreferences(null)).toEqual(DefaultPreferences);
    expect(readPreferences([1, 2])).toEqual(DefaultPreferences);
    expect(readPreferences({ theme: 'purple', libraryView: 'grid', stats: 'yes', extra: 1 })).toEqual({
      ...DefaultPreferences,
      libraryView: 'grid'
    });
  });

  it('starts with the defaults and keeps a change in the desktop app’s file', () => {
    const written: unknown[] = [];
    vi.stubGlobal('patina', {
      readPreferences: () => Promise.resolve(null),
      writePreferences: (settings: unknown) => {
        written.push(settings);
        return Promise.resolve();
      }
    });
    const preferences = TestBed.inject(Preferences);
    expect(preferences.all()).toEqual(DefaultPreferences);

    preferences.set({ libraryView: 'grid', libraryTab: 'assets' });

    expect(preferences.libraryView()).toBe('grid');
    expect(written).toEqual([{ ...DefaultPreferences, libraryView: 'grid', libraryTab: 'assets' }]);
  });

  it('takes what the desktop app’s file holds, and falls back on what it cannot use', async () => {
    vi.stubGlobal('patina', {
      readPreferences: () => Promise.resolve({ theme: 'light', shelfView: 'huge' }),
      writePreferences: () => Promise.resolve()
    });
    const preferences = TestBed.inject(Preferences);
    await Promise.resolve();
    await Promise.resolve();

    expect(preferences.theme()).toBe('light');
    expect(preferences.shelfView()).toBe('list');
  });

  it('puts the theme on the page, and follows the system for As the System', () => {
    const query = mediaQuery(false);
    vi.stubGlobal('matchMedia', () => query);
    const preferences = TestBed.inject(Preferences);
    TestBed.tick();
    expect(document.documentElement.classList.contains('dark')).toBe(true);

    preferences.set({ theme: 'light' });
    TestBed.tick();
    expect(document.documentElement.classList.contains('dark')).toBe(false);

    preferences.set({ theme: 'system' });
    TestBed.tick();
    expect(document.documentElement.classList.contains('dark')).toBe(false);
    query.change(true);
    TestBed.tick();
    expect(document.documentElement.classList.contains('dark')).toBe(true);
  });
});
