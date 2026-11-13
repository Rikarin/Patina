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

  it('keeps lists of node types as strings, each once and no more than it may', () => {
    const many = Array.from({ length: 20 }, (_, index) => `Source/N${String(index)}`);
    const read = readPreferences({
      favouriteNodes: ['Colour/Blend', 7, 'Colour/Blend', '', 'Filters/Blur'],
      recentNodes: many,
      graphWires: 'straight',
      graphSnap: true
    });
    expect(read.favouriteNodes).toEqual(['Colour/Blend', 'Filters/Blur']);
    expect(read.recentNodes).toEqual(many.slice(0, 8));
    expect(read.graphWires).toBe('straight');
    expect(read.graphSnap).toBe(true);
    expect(readPreferences({ favouriteNodes: 'Colour/Blend', graphWires: 'wavy' })).toEqual(DefaultPreferences);
  });

  it('stars a node type and remembers what was added last, newest first', () => {
    vi.stubGlobal('patina', undefined);
    const preferences = TestBed.inject(Preferences);
    preferences.toggleFavouriteNode('Colour/Blend');
    preferences.toggleFavouriteNode('Filters/Blur');
    preferences.toggleFavouriteNode('Colour/Blend');
    expect(preferences.favouriteNodes()).toEqual(['Filters/Blur']);

    for (const path of ['A', 'B', 'C', 'A']) {
      preferences.usedNode(path);
    }
    expect(preferences.recentNodes()).toEqual(['A', 'C', 'B']);
  });

  it('keeps recent documents whole, each once, from a file a person may have edited', () => {
    const rust = { library: 'lib', project: 'projects/a', path: 'projects/a/rust.vxtexgraph', name: 'Rust' };
    const read = readPreferences({
      recentDocuments: [
        rust,
        { ...rust, name: 'Rust again' },
        { ...rust, path: 7 },
        'projects/b',
        null,
        { library: 'x' }
      ]
    });
    expect(read.recentDocuments).toEqual([rust]);
    expect(readPreferences({ recentDocuments: 'rust' }).recentDocuments).toEqual([]);
  });

  it('puts the document worked on last first, writes nothing when it is first already, and forgets one that went', () => {
    const written: unknown[] = [];
    vi.stubGlobal('patina', {
      readPreferences: () => Promise.resolve(null),
      writePreferences: (settings: unknown) => {
        written.push(settings);
        return Promise.resolve();
      }
    });
    const preferences = TestBed.inject(Preferences);
    const document = (library: string, path: string) => ({ library, project: 'projects/a', path, name: path });

    preferences.usedDocument(document('lib', 'a'));
    preferences.usedDocument(document('lib', 'b'));
    preferences.usedDocument(document('other', 'a'));
    preferences.usedDocument(document('lib', 'a'));
    preferences.usedDocument(document('lib', 'a'));
    expect(preferences.recentDocuments().map(held => `${held.library}:${held.path}`)).toEqual([
      'lib:a',
      'other:a',
      'lib:b'
    ]);
    expect(written).toHaveLength(4);

    preferences.forgetDocument('lib', 'b');
    preferences.forgetDocument('lib', 'gone');
    expect(preferences.recentDocuments().map(held => `${held.library}:${held.path}`)).toEqual(['lib:a', 'other:a']);
    expect(written).toHaveLength(5);
  });

  it('scales the camera by the speeds chosen, and turns the wheel round when it is inverted', () => {
    vi.stubGlobal('patina', undefined);
    const preferences = TestBed.inject(Preferences);
    expect([preferences.orbitScale(), preferences.zoomScale()]).toEqual([1, 1]);

    preferences.set({ orbitSpeed: 'slow', zoomSpeed: 'fast', invertZoom: true });

    expect([preferences.orbitScale(), preferences.zoomScale()]).toEqual([0.5, -2]);
    expect(readPreferences({ orbitSpeed: 'warp', zoomSpeed: 2, invertZoom: 'yes' })).toEqual(DefaultPreferences);
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
