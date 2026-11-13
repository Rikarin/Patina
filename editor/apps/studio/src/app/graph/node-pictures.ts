import { type Signal, signal } from '@angular/core';

/** One node type's picture as the Server names it, or none for a type with nothing to show. */
export interface NodePictureHash {
  source: string;
  hash?: string | null;
}

/** Where the pictures come from: the open graph's Server, which draws them and serves their PNGs. */
export interface NodePictureSource {
  /** The hashes of these types' pictures, or null when they cannot be had now (no graph, no Worker). */
  draw(types: readonly string[]): Promise<readonly NodePictureHash[] | null>;
  /** The PNG. The token is header-only, so a bare `<img src>` cannot load it. */
  fetch(hash: string): Promise<Blob | null>;
  url(blob: Blob): string;
  revoke(url: string): void;
}

/** How many types one request asks for, so the first rows show while the rest are drawn. */
export const NodePictureBatch = 12;
/** How long asks gather before they go, so a list filling in asks once, not row by row. */
const GatherMs = 30;

/**
 * Small pictures of node types for the palette and the Nodes list (#183, #38). The Server draws each type once at its
 * defaults, with a noise on its picture inputs, and answers with a hash (POST /graphs/{id}/node-types/thumbnails); the
 * PNG comes from /previews. A picture is a nicety: with no Worker a type keeps its name alone, and is asked for again
 * after {@link forget}.
 */
export class NodePictures {
  private readonly drawn = signal<ReadonlyMap<string, string | null>>(new Map());
  private readonly urls = new Map<string, string>();
  private readonly asked = new Set<string>();
  private queue: string[] = [];
  private timer: ReturnType<typeof setTimeout> | null = null;
  private running = false;
  /** Goes up whenever what is held stops meaning anything, so an older answer lands nowhere. */
  private generation = 0;

  constructor(private readonly source: NodePictureSource) {}

  /** Each type's picture as an address an image can show, null for none; a type not there is not known yet. */
  readonly pictures: Signal<ReadonlyMap<string, string | null>> = this.drawn.asReadonly();

  /** A type's picture, null for none, or undefined until it is known. */
  url(type: string): string | null | undefined {
    return this.drawn().get(type);
  }

  /** Asks for these types' pictures, in this order, unless they are known or on their way. */
  want(types: readonly string[]): void {
    for (const type of types) {
      if (!this.asked.has(type)) {
        this.asked.add(type);
        this.queue.push(type);
      }
    }

    if (this.queue.length > 0 && this.timer === null && !this.running) {
      this.timer = setTimeout(() => {
        this.timer = null;
        void this.run();
      }, GatherMs);
    }
  }

  /**
   * Forgets which type has which picture, as when the graph's compounds change or another graph comes in front. What
   * is shown stays until its new answer comes, so nothing blinks.
   */
  forget(): void {
    this.generation += 1;
    this.stop();
    this.asked.clear();
    this.queue = [];
    this.want([...this.drawn().keys()]);
  }

  /** Lets every picture go. */
  dispose(): void {
    this.generation += 1;
    this.stop();
    for (const url of this.urls.values()) {
      this.source.revoke(url);
    }

    this.urls.clear();
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
        const batch = this.queue.splice(0, NodePictureBatch);
        if (!(await this.draw(batch, generation))) {
          for (const type of batch) {
            this.asked.delete(type);
          }

          return;
        }
      }
    } finally {
      this.running = false;
      // Asks made after a forget wait for this run to end; a failed run's own leftovers wait for the next forget.
      if (this.queue.length > 0 && generation !== this.generation) {
        this.want([]);
      }
    }
  }

  private async draw(types: readonly string[], generation: number): Promise<boolean> {
    try {
      const answered = await this.source.draw(types);
      if (!answered) {
        return false;
      }

      const found = new Map<string, string | null>();
      for (const { source, hash } of answered) {
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

    const blob = await this.source.fetch(hash);
    if (!blob) {
      return null;
    }

    const url = this.source.url(blob);
    this.urls.set(hash, url);
    return url;
  }
}
