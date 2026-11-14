import { DestroyRef, Injectable, computed, effect, inject, signal } from '@angular/core';
import { desktop } from './desktop';

export type Theme = 'dark' | 'light' | 'system';
export type LibraryViewChoice = 'list' | 'grid';
export type LibraryTabChoice = 'projects' | 'assets';
export type ShelfViewChoice = 'list' | 'small' | 'large';
/** How fast the viewport's camera answers the mouse. */
export type CameraSpeed = 'slow' | 'normal' | 'fast';
/** What each camera speed multiplies the mouse's movement by. */
export const CameraSpeeds: Readonly<Record<CameraSpeed, number>> = { slow: 0.5, normal: 1, fast: 2 };
/** How the graph draws a wire: xUI's routings. */
export type WireStyle = 'bezier' | 'smoothstep' | 'straight';
/** A viewport display a person made their own, by setting: what the Server's display settings are called. */
export type DisplayStart = Readonly<Record<string, string | number | boolean | null>>;

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
  /** How fast a drag turns the viewport's camera. */
  orbitSpeed: CameraSpeed;
  /** How fast the wheel and a zoom drag move the viewport's camera in and out. */
  zoomSpeed: CameraSpeed;
  /** Whether the wheel zooms the other way: rolled away from you, out. */
  invertZoom: boolean;
  /** Whether a node dragged on the graph lands on the grid. */
  graphSnap: boolean;
  /** Whether a node's card lists what was changed on it, under its title. */
  graphParameters: boolean;
  /** Whether a node's card shows how long it took on the GPU the last time it ran. */
  graphTimings: boolean;
  /** How the graph draws its wires. */
  graphWires: WireStyle;
  /** Node types starred in the Nodes list, by path, which the palette offers first. */
  favouriteNodes: readonly string[];
  /** Node types added last, by path, newest first, which the palette offers after the favourites. */
  recentNodes: readonly string[];
  /**
   * Materials starred on the shelf, by their source: a path below the library, or a starter's name. The shelf lists
   * them first, and From Favourites lists them alone.
   */
  favouriteMaterials: readonly string[];
  /** Documents brought to the front last, newest first, in every library, which Start offers again. */
  recentDocuments: readonly RecentDocument[];
  /**
   * How the viewport opens in a project that kept no display of its own, such as its light, background and grid, or
   * null for Patina's (#189).
   */
  viewportStart: DisplayStart | null;
  /** Searches of the shelf kept to come back to (#203), in the order they were kept. */
  shelfSearches: readonly ShelfSearch[];
}

/** A search of the shelf kept to come back to: its words, its type and its origin, `all` for any. */
export interface ShelfSearch {
  search: string;
  role: string;
  origin: string;
}

/** How many searches the shelf keeps. */
export const ShelfSearchLimit = 32;

/** Kept searches as a file holds them: each whole, each once, as many as it may keep. */
function shelfSearchesOf(value: unknown): readonly ShelfSearch[] {
  const read: ShelfSearch[] = [];
  for (const item of Array.isArray(value) ? (value as unknown[]) : []) {
    const { search, role, origin } = (typeof item === 'object' && item !== null ? item : {}) as Record<string, unknown>;
    if (typeof search !== 'string' || typeof role !== 'string' || typeof origin !== 'string' || !role || !origin) {
      continue;
    }

    const kept = { search: search.trim(), role, origin };
    if (!read.some(held => sameSearch(held, kept))) {
      read.push(kept);
    }
  }

  return read.slice(0, ShelfSearchLimit);
}

/** Whether two searches of the shelf find the same: the same words, whatever their case, type and origin. */
export function sameSearch(a: ShelfSearch, b: ShelfSearch): boolean {
  return (
    a.search.trim().toLocaleLowerCase() === b.search.trim().toLocaleLowerCase() &&
    a.role === b.role &&
    a.origin === b.origin
  );
}

/** A document someone worked on, by where it is in its library. */
export interface RecentDocument {
  /** The library's identity, from `library.yaml`, which stays when its folder moves. */
  library: string;
  /** The project's path below the library. */
  project: string;
  /** The document's path below the library. */
  path: string;
  /** Its name as its tab showed it. */
  name: string;
}

/** How many recent documents the file keeps, over every library. */
export const RecentDocumentLimit = 40;

/** Recent documents as a file holds them: each whole, each once, newest first, as many as it may keep. */
function recentDocumentsOf(value: unknown): readonly RecentDocument[] {
  if (!Array.isArray(value)) {
    return [];
  }

  const read: RecentDocument[] = [];
  for (const item of value as unknown[]) {
    if (typeof item !== 'object' || item === null) {
      continue;
    }

    const { library, project, path, name } = item as Record<string, unknown>;
    const whole = [library, project, path, name].every(field => typeof field === 'string' && field.length > 0);
    if (whole && !read.some(held => held.library === library && held.path === path)) {
      read.push({ library, project, path, name } as RecentDocument);
    }
  }

  return read.slice(0, RecentDocumentLimit);
}

export const DefaultPreferences: PreferenceValues = {
  theme: 'dark',
  libraryView: 'list',
  libraryTab: 'projects',
  shelfView: 'list',
  stats: false,
  orbitSpeed: 'normal',
  zoomSpeed: 'normal',
  invertZoom: false,
  graphSnap: false,
  graphParameters: false,
  graphTimings: false,
  graphWires: 'bezier',
  favouriteNodes: [],
  recentNodes: [],
  favouriteMaterials: [],
  recentDocuments: [],
  viewportStart: null,
  shelfSearches: []
};

/** How many node types each list keeps, so a hand-edited file cannot grow the palette without end. */
export const ListLimits = { favouriteNodes: 64, recentNodes: 8, favouriteMaterials: 256 } as const;
type ListPreference = keyof typeof ListLimits;

const StorageKey = 'patina.preferences';
const DarkQuery = '(prefers-color-scheme: dark)';

const Choices: {
  [
    K in Exclude<keyof PreferenceValues, ListPreference | 'recentDocuments' | 'viewportStart' | 'shelfSearches'>
  ]: readonly PreferenceValues[K][];
} = {
  theme: ['dark', 'light', 'system'],
  libraryView: ['list', 'grid'],
  libraryTab: ['projects', 'assets'],
  shelfView: ['list', 'small', 'large'],
  stats: [false, true],
  orbitSpeed: ['normal', 'slow', 'fast'],
  zoomSpeed: ['normal', 'slow', 'fast'],
  invertZoom: [false, true],
  graphSnap: [false, true],
  graphParameters: [false, true],
  graphTimings: [false, true],
  graphWires: ['bezier', 'smoothstep', 'straight']
};

/** A list as a file holds it: its strings, each once, as many as it may keep. */
function listOf(value: unknown, limit: number): readonly string[] {
  return Array.isArray(value)
    ? [...new Set(value.filter((item): item is string => typeof item === 'string' && item.length > 0))].slice(0, limit)
    : [];
}

/** A display made one's own as a file holds it: its settings of plain values, or null for none. */
function displayStartOf(value: unknown): DisplayStart | null {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    return null;
  }

  const plain = Object.entries(value as Record<string, unknown>).filter(
    ([, setting]) => setting === null || ['string', 'number', 'boolean'].includes(typeof setting)
  );
  return plain.length > 0 ? (Object.fromEntries(plain) as DisplayStart) : null;
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

  read.recentDocuments = recentDocumentsOf(held['recentDocuments']);
  read.viewportStart = displayStartOf(held['viewportStart']);
  read.shelfSearches = shelfSearchesOf(held['shelfSearches']);

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
  /** What a drag's pixels are multiplied by before they turn the camera. */
  readonly orbitScale = computed(() => CameraSpeeds[this.values().orbitSpeed]);
  /** What the wheel's and a zoom drag's pixels are multiplied by, negative when the wheel is inverted. */
  readonly zoomScale = computed(() => CameraSpeeds[this.values().zoomSpeed] * (this.values().invertZoom ? -1 : 1));
  readonly graphSnap = computed(() => this.values().graphSnap);
  readonly graphParameters = computed(() => this.values().graphParameters);
  readonly graphTimings = computed(() => this.values().graphTimings);
  readonly graphWires = computed(() => this.values().graphWires);
  readonly favouriteNodes = computed(() => this.values().favouriteNodes);
  readonly recentNodes = computed(() => this.values().recentNodes);
  readonly favouriteMaterials = computed(() => this.values().favouriteMaterials);
  readonly recentDocuments = computed(() => this.values().recentDocuments);
  readonly viewportStart = computed(() => this.values().viewportStart);
  readonly shelfSearches = computed(() => this.values().shelfSearches);
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

  /** Stars a material on the shelf, or takes its star away. */
  toggleFavouriteMaterial(source: string): void {
    const held = this.favouriteMaterials();
    this.set({ favouriteMaterials: held.includes(source) ? held.filter(item => item !== source) : [...held, source] });
  }

  /** Puts a node type at the front of the recent ones, as adding it does. */
  usedNode(path: string): void {
    this.set({ recentNodes: [path, ...this.recentNodes().filter(item => item !== path)] });
  }

  /** Puts a document at the front of the recent ones, unless it is there already. */
  usedDocument(document: RecentDocument): void {
    const [first] = this.recentDocuments();
    if (first?.library === document.library && first.path === document.path && first.name === document.name) {
      return; // no write for a tab that only came back to the front
    }

    this.set({
      recentDocuments: [
        document,
        ...this.recentDocuments().filter(held => held.library !== document.library || held.path !== document.path)
      ]
    });
  }

  /** Forgets a recent document that has moved or gone. */
  forgetDocument(library: string, path: string): void {
    const held = this.recentDocuments();
    const kept = held.filter(item => item.library !== library || item.path !== path);
    if (kept.length !== held.length) {
      this.set({ recentDocuments: kept });
    }
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
