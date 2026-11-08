import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import {
  ApiError,
  type AssetImpact,
  type AssetSummary,
  type ImpactedProject,
  type LibraryInfo,
  type PatinaClient,
  type ProjectCreateRequest,
  type ProjectSummary,
  type TagCount,
  type TagPage,
  unwrap
} from '@patina/api-client';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';
import { LibrarySelection } from './library-selection';
import { type Page, PagedList } from './paged-list';
import { messageOf } from '../core/messages';

export const SearchDebounceMs = 200;
const IdlePollMs = 5_000;
const IndexingPollMs = 300;
const TagLimit = 500;

export type LibraryState =
  { kind: 'loading' } | { kind: 'none' } | { kind: 'error'; reason: string } | { kind: 'ready'; info: LibraryInfo };

export interface LibraryQuery {
  search: string;
  tags: readonly string[];
}

type QueryPages<T> = (query: LibraryQuery, offset: number, limit: number, signal: AbortSignal) => Promise<Page<T>>;

/** One tab of the browser: its query, the page of results it holds, and the tags it can filter by. */
export class LibraryView<T> {
  private query: LibraryQuery = { search: '', tags: [] };
  private debounce: ReturnType<typeof setTimeout> | null = null;

  /** The search box as typed; {@link list} follows it after a pause. */
  readonly search = signal('');
  readonly tags = signal<string[]>([]);
  readonly tagOptions = signal<readonly TagCount[]>([]);
  readonly list: PagedList<T>;

  constructor(
    pages: QueryPages<T>,
    private readonly fetchTags: () => Promise<TagPage>
  ) {
    this.list = new PagedList((offset, limit, signal) => pages(this.query, offset, limit, signal));
  }

  /** Whether a search or a tag narrows the list, which decides what an empty list means. */
  get filtered(): boolean {
    return this.query.search !== '' || this.query.tags.length > 0;
  }

  setSearch(text: string): void {
    this.search.set(text);
    this.stopDebounce();
    this.debounce = setTimeout(() => {
      this.debounce = null;
      this.apply();
    }, SearchDebounceMs);
  }

  setTags(tags: string[]): void {
    this.tags.set(tags);
    this.stopDebounce();
    this.apply();
  }

  /** The index changed under this view. */
  refresh(): void {
    this.list.refresh();
    void this.loadTags();
  }

  async loadTags(): Promise<void> {
    try {
      this.tagOptions.set((await this.fetchTags()).items);
    } catch {
      // The filter is a convenience, and the list beside it reports the same failure.
    }
  }

  dispose(): void {
    this.stopDebounce();
    this.list.cancel();
  }

  private apply(): void {
    const query = { search: this.search().trim(), tags: this.tags() };
    if (query.search !== this.query.search || query.tags !== this.query.tags) {
      this.query = query;
      this.list.reset();
    }
  }

  private stopDebounce(): void {
    if (this.debounce !== null) {
      clearTimeout(this.debounce);
      this.debounce = null;
    }
  }
}

/** A material's dependent projects, a page at a time, and the counts that come with every page. */
export class ImpactList {
  private readonly latest = signal<AssetImpact | null>(null);

  readonly impact = this.latest.asReadonly();
  readonly list: PagedList<ImpactedProject>;

  constructor(pages: (offset: number, limit: number, signal: AbortSignal) => Promise<AssetImpact>) {
    this.list = new PagedList(async (offset, limit, signal) => {
      const impact = await pages(offset, limit, signal);
      this.latest.set(impact);
      return { items: impact.items, total: impact.projects };
    });
  }
}

/**
 * The library as a client: the Server owns it, and this mirrors what the browser needs. `/library` is polled, slowly
 * until a rescan runs and quickly while one does, and the views refetch what they show when the index has moved. There
 * is no event stream yet, and this is the only part that would change when there is.
 */
@Injectable({ providedIn: 'root' })
export class LibraryStore {
  private readonly connection = inject(SERVER_CONNECTION);
  private readonly createClient = inject(API_CLIENT_FACTORY);
  private readonly selection = inject(LibrarySelection);
  private readonly mutableState = signal<LibraryState>({ kind: 'loading' });
  private readonly mutableChanges = signal(0);
  private client: PatinaClient | null = null;
  private seen: LibraryInfo | null = null;
  private timer: ReturnType<typeof setTimeout> | null = null;
  private stopped = false;

  readonly state = this.mutableState.asReadonly();
  /** Goes up each time the index moves, for what has to refetch alongside the views. */
  readonly changes = this.mutableChanges.asReadonly();

  readonly projects = new LibraryView<ProjectSummary>(
    async (query, offset, limit, signal) => {
      const client = await this.api();
      return unwrap(
        await client.GET('/api/v1/library/projects', { params: { query: searchOf(query, offset, limit) }, signal })
      );
    },
    async () => {
      const client = await this.api();
      return unwrap(await client.GET('/api/v1/library/projects/tags', { params: { query: { limit: TagLimit } } }));
    }
  );

  readonly assets = new LibraryView<AssetSummary>(
    async (query, offset, limit, signal) => {
      const client = await this.api();
      return unwrap(
        await client.GET('/api/v1/library/assets', { params: { query: searchOf(query, offset, limit) }, signal })
      );
    },
    async () => {
      const client = await this.api();
      return unwrap(await client.GET('/api/v1/library/assets/tags', { params: { query: { limit: TagLimit } } }));
    }
  );

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.stopped = true;
      this.stopTimer();
      this.projects.dispose();
      this.assets.dispose();
    });
    void this.poll();
  }

  /** Asks the Server again at once, for when a problem on screen is one the user may just have fixed. */
  retry(): void {
    this.mutableState.set({ kind: 'loading' });
    void this.poll();
  }

  /** Asks at once, for a write that sets the index moving. */
  refreshSoon(): void {
    void this.poll();
  }

  /** The dependents of an asset. Each call is a list of its own, which the caller cancels. */
  impactOf(asset: AssetSummary): ImpactList {
    return new ImpactList(async (offset, limit, signal) => {
      const client = await this.api();
      return unwrap(
        await client.GET('/api/v1/library/assets/{id}/impact', {
          params: { path: { id: asset.id }, query: { offset, limit } },
          signal
        })
      );
    });
  }

  /**
   * Creates a project. The Server writes its files and the index catches up behind it, so the reply is what selects the
   * new project; the next polls bring it into the list.
   */
  async createProject(request: ProjectCreateRequest): Promise<ProjectSummary> {
    const client = await this.api();
    const project = unwrap(await client.POST('/api/v1/library/projects', { body: request }));
    this.selection.select({ kind: 'project', project });
    void this.poll();
    return project;
  }

  async api(): Promise<PatinaClient> {
    this.client ??= this.createClient(await this.connection());
    return this.client;
  }

  private async poll(): Promise<void> {
    this.stopTimer();
    try {
      const client = await this.api();
      this.accept(unwrap(await client.GET('/api/v1/library')));
    } catch (error) {
      this.seen = null;
      if (error instanceof ApiError && error.status === 404) {
        this.mutableState.set({ kind: 'none' });
        this.selection.clear();
      } else {
        this.client = null; // a restarted Server has a new port and token
        this.mutableState.set({ kind: 'error', reason: messageOf(error) });
      }
    }

    if (!this.stopped) {
      this.stopTimer(); // two polls can overlap: a project created while one was out
      const delay = this.seen?.indexing ? IndexingPollMs : IdlePollMs;
      this.timer = setTimeout(() => void this.poll(), delay);
    }
  }

  private accept(info: LibraryInfo): void {
    const before = this.seen;
    this.seen = info;
    this.mutableState.set({ kind: 'ready', info });
    if (before === null) {
      void this.projects.loadTags();
      void this.assets.loadTags();
      return;
    }

    if (before.id !== info.id) {
      this.selection.clear();
    }

    const caughtUp = before.indexing && !info.indexing;
    const moved = before.projectCount !== info.projectCount || before.assetCount !== info.assetCount;
    if (before.id !== info.id || caughtUp || moved) {
      this.projects.refresh();
      this.assets.refresh();
      this.mutableChanges.update(count => count + 1);
    }
  }

  private stopTimer(): void {
    if (this.timer !== null) {
      clearTimeout(this.timer);
      this.timer = null;
    }
  }
}

function searchOf(
  query: LibraryQuery,
  offset: number,
  limit: number
): { search?: string; tag?: string[]; offset: number; limit: number } {
  return {
    ...(query.search ? { search: query.search } : {}),
    ...(query.tags.length > 0 ? { tag: [...query.tags] } : {}),
    offset,
    limit
  };
}
