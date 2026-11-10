import { DestroyRef, Injectable, computed, effect, inject, signal } from '@angular/core';
import { desktop } from './desktop';

export type Theme = 'dark' | 'light' | 'system';
export type LibraryViewChoice = 'list' | 'grid';
export type LibraryTabChoice = 'projects' | 'assets';
export type ShelfViewChoice = 'list' | 'small' | 'large';
/** How the graph draws a wire: xUI's routings. */
export type WireStyle = 'bezier' | 'smoothstep' | 'straight';

/**
 * The person's settings for this app, wherever they work: not a library's or a project's. The desktop app keeps them
 * in `preferences.json` beside `keys.json`, plain JSON a person can read and edit; a plain browser keeps them in local
 * storage. What the Server acts on, such as autosave, is the Server's own (GET /api/v1/preferences), so every client
 * shares it.
 */
export interface PreferenceValues {
  theme: Theme;
  /** Whether the Library opens as a list or a grid of pictures. */
  libraryView: LibraryViewChoice;
  /** Whether the Library opens on projects or materials. */
  libraryTab: LibraryTabChoice;
  /** How large the shelf's pictures are. */
  shelfView: ShelfViewChoice;
  /** Whether the viewport shows render statistics. */
  stats: boolean;
  /** Whether a node dragged on the graph lands on the grid. */
  graphSnap: boolean;
  /** How the graph draws its wires. */
  graphWires: WireStyle;
  /** Node types starred in the Nodes list, by path, which the palette offers first. */
  favouriteNodes: readonly string[];
  /** Node types added last, by path, newest first, which the palette offers after the favourites. */
  recentNodes: readonly string[];
}

export const DefaultPreferences: PreferenceValues = {
  theme: 'dark',
  libraryView: 'list',
  libraryTab: 'projects',
  shelfView: 'list',
  stats: false,
  graphSnap: false,
  graphWires: 'bezier',
  favouriteNodes: [],
  recentNodes: []
};

/** How many node types each list keeps, so a hand-edited file cannot grow the palette without end. */
export const ListLimits = { favouriteNodes: 64, recentNodes: 8 } as const;
type ListPreference = keyof typeof ListLimits;

const StorageKey = 'patina.preferences';
const DarkQuery = '(prefers-color-scheme: dark)';

const Choices: { [K in Exclude<keyof PreferenceValues, ListPreference>]: readonly PreferenceValues[K][] } = {
  theme: ['dark', 'light', 'system'],
  libraryView: ['list', 'grid'],
  libraryTab: ['projects', 'assets'],
  shelfView: ['list', 'small', 'large'],
  stats: [false, true],
  graphSnap: [false, true],
  graphWires: ['bezier', 'smoothstep', 'straight']
};

/** A list as a file holds it: its strings, each once, as many as it may keep. */
function listOf(value: unknown, limit: number): readonly string[] {
  return Array.isArray(value)
    ? [...new Set(value.filter((item): item is string => typeof item === 'string' && item.length > 0))].slice(0, limit)
    : [];
}

/** Preferences as read from a file a person may have edited: anything unknown or out of range is the default. */
export function readPreferences(value: unknown): PreferenceValues {
  const held =
    typeof value === 'object' && value !== null && !Array.isArray(value) ? (value as Record<string, unknown>) : {};
  const read = { ...DefaultPreferences };
  for (const key of Object.keys(Choices) as (keyof typeof Choices)[]) {
    const given = held[key];
    if ((Choices[key] as readonly unknown[]).includes(given)) {
      (read as Record<string, unknown>)[key] = given;
    }
  }

  for (const key of Object.keys(ListLimits) as ListPreference[]) {
    read[key] = listOf(held[key], ListLimits[key]);
  }

  return read;
}

/** The person's app-wide preferences, read once at start and written whole on every change. */
@Injectable({ providedIn: 'root' })
export class Preferences {
  private readonly values = signal<PreferenceValues>(readStored());
  private readonly systemDark = signal(true);

  /** Every preference as it stands. */
  readonly all = this.values.asReadonly();

  readonly theme = computed(() => this.values().theme);
  readonly libraryView = computed(() => this.values().libraryView);
  readonly libraryTab = computed(() => this.values().libraryTab);
  readonly shelfView = computed(() => this.values().shelfView);
  readonly stats = computed(() => this.values().stats);
  readonly graphSnap = computed(() => this.values().graphSnap);
  readonly graphWires = computed(() => this.values().graphWires);
  readonly favouriteNodes = computed(() => this.values().favouriteNodes);
  readonly recentNodes = computed(() => this.values().recentNodes);
  /** Whether the app is dark now: the theme chosen, or the system's for `system`. */
  readonly dark = computed(() => {
    const theme = this.theme();
    return theme === 'system' ? this.systemDark() : theme === 'dark';
  });

  constructor() {
    // A test's page may have no media queries at all.
    const match = globalThis.matchMedia as ((media: string) => MediaQueryList) | undefined;
    const query = match?.(DarkQuery);
    if (query) {
      this.systemDark.set(query.matches);
      const follow = (event: MediaQueryListEvent) => this.systemDark.set(event.matches);
      query.addEventListener('change', follow);
      inject(DestroyRef).onDestroy(() => query.removeEventListener('change', follow));
    }

    effect(() => {
      globalThis.document.documentElement.classList.toggle('dark', this.dark());
    });

    const bridge = desktop();
    if (bridge) {
      void bridge
        .readPreferences()
        .then(value => {
          if (value !== null && value !== undefined) {
            this.values.set(readPreferences(value));
          }
        })
        .catch(() => undefined); // the defaults, or what this machine last had
    }
  }

  /** Stars a node type for the palette, or takes its star away. */
  toggleFavouriteNode(path: string): void {
    const held = this.favouriteNodes();
    this.set({ favouriteNodes: held.includes(path) ? held.filter(item => item !== path) : [...held, path] });
  }

  /** Puts a node type at the front of the recent ones, as adding it does. */
  usedNode(path: string): void {
    this.set({ recentNodes: [path, ...this.recentNodes().filter(item => item !== path)] });
  }

  /** Changes some preferences and keeps them. */
  set(change: Partial<PreferenceValues>): void {
    const next = readPreferences({ ...this.values(), ...change });
    this.values.set(next);
    try {
      globalThis.localStorage.setItem(StorageKey, JSON.stringify(next));
    } catch {
      // Only this machine's copy is lost; the desktop app's file below is the one that counts there.
    }

    void desktop()
      ?.writePreferences(next)
      .catch(() => undefined);
  }
}

function readStored(): PreferenceValues {
  try {
    return readPreferences(JSON.parse(globalThis.localStorage.getItem(StorageKey) ?? 'null'));
  } catch {
    return { ...DefaultPreferences };
  }
}
