import { Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { type FolderSummary, type ProjectSummary, unwrap } from '@patina/api-client';
import { LibraryStore } from './library-store';

const PageSize = 200;
const ExpandedKey = 'patina.library.expanded';

/** The colours a folder can take, and the first of them is the one none has. */
export const FolderColours: readonly string[] = [
  '#e5484d',
  '#f76b15',
  '#f5d90a',
  '#46a758',
  '#12a594',
  '#3e63dd',
  '#8e4ec6',
  '#d6409f'
];

export interface FolderContents {
  folders: readonly FolderSummary[];
  projects: readonly ProjectSummary[];
  total: number;
}

export type TreeRow =
  | { kind: 'folder'; path: string; depth: number; folder: FolderSummary; expanded: boolean }
  | { kind: 'project'; path: string; depth: number; project: ProjectSummary }
  | { kind: 'more'; path: string; depth: number; remaining: number };

/** The folder a path sits in, where empty is the top. */
export function parentOf(path: string): string {
  const slash = path.lastIndexOf('/');
  return slash < 0 ? '' : path.slice(0, slash);
}

/** The rows on screen: a folder's folders, then its projects, then what has not loaded, depth first. */
export function flatten(
  contents: ReadonlyMap<string, FolderContents>,
  expanded: ReadonlySet<string>,
  folder = '',
  depth = 0
): TreeRow[] {
  const held = contents.get(folder);
  if (!held) {
    return [];
  }

  const rows: TreeRow[] = [];
  for (const child of held.folders) {
    const open = expanded.has(child.path);
    rows.push({ kind: 'folder', path: child.path, depth, folder: child, expanded: open });
    if (open) {
      rows.push(...flatten(contents, expanded, child.path, depth + 1));
    }
  }

  for (const project of held.projects) {
    rows.push({ kind: 'project', path: project.path, depth, project });
  }

  if (held.projects.length < held.total) {
    rows.push({ kind: 'more', path: folder, depth, remaining: held.total - held.projects.length });
  }

  return rows;
}

/** Whether what is being dragged can land in a folder: not in itself, not below itself, and not where it already is. */
export function canDrop(paths: readonly string[], into: string): boolean {
  return (
    paths.length > 0 &&
    paths.every(path => path !== into && !into.startsWith(`${path}/`)) &&
    paths.some(path => parentOf(path) !== into)
  );
}

/**
 * The Library as folders. A folder's contents load when it opens, a page at a time, so a tree over 10,000 projects
 * costs what is on screen. The Server owns the folders; this holds what has been read, and reads it again whenever the
 * index moves.
 */
@Injectable({ providedIn: 'root' })
export class LibraryTreeStore {
  private readonly library = inject(LibraryStore);
  private readonly contents = signal<ReadonlyMap<string, FolderContents>>(new Map());
  private readonly mutableExpanded = signal<ReadonlySet<string>>(new Set(loadExpanded()));
  private readonly mutableSelected = signal<ReadonlySet<string>>(new Set());
  private readonly mutableFailure = signal<string | null>(null);
  private anchor: string | null = null;

  readonly rows = computed(() => flatten(this.contents(), this.mutableExpanded()));
  readonly selected = this.mutableSelected.asReadonly();
  readonly failure = this.mutableFailure.asReadonly();
  /** Whether the top of the library has been read. */
  readonly loaded = computed(() => this.contents().has(''));
  /** The folder new things go in: the one chosen, or the one the chosen project is in. */
  readonly target = computed(() => {
    const path = this.mutableSelected().values().next().value;
    if (path === undefined) {
      return '';
    }

    // A project just made is chosen before the index lists it, and only a folder is sure to be on screen.
    const row = this.rows().find(candidate => candidate.path === path);
    return row?.kind === 'folder' ? path : parentOf(path);
  });

  constructor() {
    effect(() => {
      const state = this.library.state();
      this.library.changes();
      if (state.kind === 'ready') {
        untracked(() => void this.reload());
      }
    });
  }

  clearFailure(): void {
    this.mutableFailure.set(null);
  }

  async toggle(path: string): Promise<void> {
    if (this.mutableExpanded().has(path)) {
      this.setExpanded(path, false);
      return;
    }

    this.setExpanded(path, true);
    await this.read(path);
  }

  async expand(path: string): Promise<void> {
    if (!this.mutableExpanded().has(path)) {
      await this.toggle(path);
    }
  }

  collapse(path: string): void {
    this.setExpanded(path, false);
  }

  /** Reads the next page of a folder's projects. */
  async more(folder: string): Promise<void> {
    await this.attempt(async () => {
      const held = this.contents().get(folder);
      if (held) {
        const page = await this.projects(folder, held.projects.length);
        this.put(folder, { ...held, projects: [...held.projects, ...page.items], total: page.total });
      }
    });
  }

  /** Chooses a row: on its own, added with Ctrl or Cmd, or as a range with Shift. */
  choose(path: string, mode: 'only' | 'toggle' | 'range' = 'only'): void {
    if (mode === 'range' && this.anchor !== null) {
      const paths = this.rows().filter(row => row.kind !== 'more').map(row => row.path);
      const [from, to] = [paths.indexOf(this.anchor), paths.indexOf(path)].sort((a, b) => a - b);
      if (from !== undefined && to !== undefined && from >= 0) {
        this.mutableSelected.set(new Set(paths.slice(from, to + 1)));
        return;
      }
    }

    this.anchor = path;
    if (mode === 'toggle') {
      const next = new Set(this.mutableSelected());
      if (!next.delete(path)) {
        next.add(path);
      }

      this.mutableSelected.set(next);
      return;
    }

    this.mutableSelected.set(new Set([path]));
  }

  clearChoice(): void {
    this.anchor = null;
    this.mutableSelected.set(new Set());
  }

  /** Makes a folder and opens the way to it. Null when the Server refused, and {@link failure} says why. */
  async createFolder(parent: string, name: string): Promise<string | null> {
    return this.attempt(async () => {
      const client = await this.library.api();
      const made = unwrap(
        await client.POST('/api/v1/library/folders', { body: { name, parent: parent || null } })
      );
      if (parent !== '') {
        await this.expand(parent);
      }

      await this.read(parent);
      this.choose(made.path);
      return made.path;
    });
  }

  async renameFolder(path: string, name: string): Promise<void> {
    await this.attempt(async () => {
      const client = await this.library.api();
      unwrap(await client.PATCH('/api/v1/library/folders', { body: { path, name } }));
      this.movedAway(path, `${parentOf(path) ? `${parentOf(path)}/` : ''}${name.trim()}`);
      await this.afterMove();
    });
  }

  async setColour(path: string, color: string | null): Promise<void> {
    await this.attempt(async () => {
      const client = await this.library.api();
      unwrap(await client.PATCH('/api/v1/library/folders', { body: { path, color: color ?? '' } }));
      await this.read(parentOf(path));
    });
  }

  async renameProject(path: string, name: string): Promise<void> {
    await this.attempt(async () => {
      const client = await this.library.api();
      unwrap(await client.PATCH('/api/v1/library/project', { body: { path, name } }));
      await this.afterMove();
    });
  }

  /** Deletes a folder. One with something in it moves that up a level first, so no project is ever deleted. */
  async deleteFolder(path: string, lift: boolean): Promise<void> {
    await this.attempt(async () => {
      const client = await this.library.api();
      unwrap(await client.DELETE('/api/v1/library/folders', { params: { query: { path, lift } } }));
      this.setExpanded(path, false);
      this.movedAway(path, parentOf(path));
      this.clearChoice();
      await this.afterMove();
    });
  }

  /** Moves folders and projects into a folder, all of them or none. */
  async move(paths: readonly string[], into: string): Promise<void> {
    await this.attempt(async () => {
      const client = await this.library.api();
      const moved = unwrap(
        await client.POST('/api/v1/library/move', { body: { paths: [...paths], into: into || null } })
      );
      paths.forEach((path, index) => this.movedAway(path, moved.paths[index] ?? path));
      if (into !== '') {
        this.setExpanded(into, true);
      }

      this.mutableSelected.set(new Set(moved.paths));
      await this.afterMove();
    });
  }

  /** Reads every folder that has been read, for when the index moved under them. */
  async reload(): Promise<void> {
    await this.attempt(async () => {
      const open = ['', ...[...this.mutableExpanded()]];
      await Promise.all(open.map(path => this.read(path)));
    });
  }

  private async afterMove(): Promise<void> {
    this.library.refreshSoon();
    await this.reload();
  }

  /** A folder that moved keeps its place in the tree, and so does what was open below it. */
  private movedAway(from: string, to: string): void {
    const renamed = [...this.mutableExpanded()].map(path =>
      path === from ? to : path.startsWith(`${from}/`) ? (to ? to + path.slice(from.length) : path.slice(from.length + 1)) : path
    );
    this.mutableExpanded.set(new Set(renamed));
    saveExpanded(renamed);
  }

  private async read(folder: string): Promise<void> {
    const client = await this.library.api();
    try {
      const [folders, page] = await Promise.all([
        client.GET('/api/v1/library/folders', { params: { query: { parent: folder } } }).then(unwrap),
        this.projects(folder, 0)
      ]);
      this.put(folder, { folders: folders.items, projects: page.items, total: page.total });
    } catch (failure) {
      // A folder someone removed outside the app stops being open, and that is all.
      if (folder === '' || !isMissing(failure)) {
        throw failure;
      }

      this.setExpanded(folder, false);
    }
  }

  private async projects(folder: string, offset: number) {
    const client = await this.library.api();
    return unwrap(
      await client.GET('/api/v1/library/projects', { params: { query: { folder, offset, limit: PageSize } } })
    );
  }

  private put(folder: string, held: FolderContents): void {
    this.contents.update(all => new Map(all).set(folder, held));
  }

  private setExpanded(path: string, open: boolean): void {
    const next = new Set(this.mutableExpanded());
    if (open) {
      next.add(path);
    } else {
      next.delete(path);
    }

    this.mutableExpanded.set(next);
    saveExpanded([...next]);
  }

  private async attempt<T>(act: () => Promise<T>): Promise<T | null> {
    this.mutableFailure.set(null);
    try {
      return await act();
    } catch (failure) {
      this.mutableFailure.set(failure instanceof Error ? failure.message : String(failure));
      return null;
    }
  }
}

function isMissing(failure: unknown): boolean {
  return (failure as { status?: number }).status === 404;
}

function loadExpanded(): string[] {
  try {
    const saved: unknown = JSON.parse(globalThis.localStorage.getItem(ExpandedKey) ?? '[]');
    return Array.isArray(saved) ? saved.filter((path): path is string => typeof path === 'string') : [];
  } catch {
    return [];
  }
}

function saveExpanded(paths: readonly string[]): void {
  globalThis.localStorage.setItem(ExpandedKey, JSON.stringify(paths));
}
