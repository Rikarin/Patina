import { DestroyRef, Injectable, effect, inject, signal, untracked } from '@angular/core';
import { LibraryStore } from './library-store';

/** How many pictures one request asks for, so that the first ones show while the rest are drawn. */
export const ThumbnailBatch = 8;
/** How long requests gather before they go, so that a list filling in asks once, not row by row. */
const GatherMs = 30;

/**
 * Pictures of what a library holds, for any list in the app: the Library's grid and list, and the shelf. The Server
 * draws them into its content-addressed cache and answers each source with a hash (POST /library/thumbnails); the
 * pixels come from /previews over HTTP, never in JSON. A source is what its list names: a document's path below the
 * library, a project's folder (`projects/…`), or a material the shelf offers from Patina itself.
 *
 * A picture is a nicety. With no Worker, a source keeps its name only, and is asked for again when the library moves.
 */
@Injectable({ providedIn: 'root' })
export class LibraryThumbnails {
  private readonly library = inject(LibraryStore);
  /** Each source's picture, as an object URL, or null for one with nothing to show. */
  private readonly drawn = signal<ReadonlyMap<string, string | null>>(new Map());
  private readonly urls = new Map<string, string>();
  private readonly asked = new Set<string>();
  private queue: string[] = [];
  private timer: ReturnType<typeof setTimeout> | null = null;
  private running = false;
  /** Goes up whenever what is held stops meaning anything, so that answers to older asks land nowhere. */
  private generation = 0;

  constructor() {
    // A saved edit, a new project or a restore moves the index: each picture is asked for again, and the Server's
    // answer for one that did not change is its old hash, from its cache.
    let seen = untracked(this.library.changes);
    effect(() => {
      const changes = this.library.changes();
      if (changes !== seen) {
        seen = changes;
        untracked(() => this.forget());
      }
    });
    inject(DestroyRef).onDestroy(() => {
      this.stop();
      for (const url of this.urls.values()) {
        URL.revokeObjectURL(url);
      }
    });
  }

  /** A source's picture as an address an image can show, null for none, or undefined until it is known. */
  url(source: string): string | null | undefined {
    return this.drawn().get(source);
  }

  /** Asks for the pictures of these sources, in this order, unless they are known or on their way. */
  want(sources: readonly string[]): void {
    for (const source of sources) {
      if (!this.asked.has(source)) {
        this.asked.add(source);
        this.queue.push(source);
      }
    }

    if (this.queue.length > 0 && this.timer === null && !this.running) {
      this.timer = setTimeout(() => {
        this.timer = null;
        void this.run();
      }, GatherMs);
    }
  }

  /** Forgets which source has which picture, so the next ask goes to the Server again. Pictures stay until replaced. */
  forget(): void {
    this.generation += 1;
    this.stop();
    this.asked.clear();
    this.queue = [];
    // What is on screen keeps its picture until the new answer comes, so nothing blinks.
    for (const source of this.drawn().keys()) {
      this.want([source]);
    }
  }

  private stop(): void {
    if (this.timer !== null) {
      clearTimeout(this.timer);
      this.timer = null;
    }
  }

  private async run(): Promise<void> {
    this.running = true;
    const generation = this.generation;
    try {
      while (this.queue.length > 0 && generation === this.generation) {
        const batch = this.queue.splice(0, ThumbnailBatch);
        if (!(await this.draw(batch, generation))) {
          // No Worker, or no Server: these are asked for again when the library next moves.
          for (const source of batch) {
            this.asked.delete(source);
          }

          return;
        }
      }
    } finally {
      this.running = false;
      if (this.queue.length > 0 && generation !== this.generation) {
        this.want([]);
      }
    }
  }

  private async draw(sources: readonly string[], generation: number): Promise<boolean> {
    try {
      const client = await this.library.api();
      const { data } = await client.POST('/api/v1/library/thumbnails', { body: { sources: [...sources] } });
      if (!data) {
        return false;
      }

      const found = new Map<string, string | null>();
      for (const { source, hash } of data.items) {
        found.set(source, hash ? await this.urlOf(hash) : null);
      }

      if (generation === this.generation) {
        this.drawn.update(held => new Map([...held, ...found]));
      }

      return true;
    } catch {
      return false;
    }
  }

  private async urlOf(hash: string): Promise<string | null> {
    const held = this.urls.get(hash);
    if (held) {
      return held;
    }

    const client = await this.library.api();
    const { data } = await client.GET('/api/v1/previews/{hash}', { params: { path: { hash } }, parseAs: 'blob' });
    if (!data) {
      return null;
    }

    const url = URL.createObjectURL(data);
    this.urls.set(hash, url);
    return url;
  }
}
