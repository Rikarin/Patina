import { type Signal, type WritableSignal, signal } from '@angular/core';
import type { GraphPreview } from '@patina/api-client';

/** A picture on screen. */
export interface ShownPreview {
  hash: string;
  url: string;
  width: number;
  height: number;
}

interface Picture {
  url: string;
  uses: number;
}

interface NodeSlot {
  shown: WritableSignal<ShownPreview | null>;
  wanted: GraphPreview | null;
  /** Whether it waits in the queue for a free request. */
  queued: boolean;
  /** At most one request per node, so a slow old picture cannot land on top of a new one. */
  loading: boolean;
}

export interface PreviewSource {
  /** The PNG. The token is header-only, so a bare `<img src>` cannot load it. */
  fetch(hash: string): Promise<Blob>;
  url(blob: Blob): string;
  revoke(url: string): void;
}

/** Past this the browser queues them anyway, and a slider's next hashes should not wait behind its last. */
const RequestLimit = 6;

/**
 * Each node's thumbnail. Only the newest hash of a node is fetched, a node's old picture stays until the new one has
 * arrived, and each node has a signal of its own, so that one preview redraws one thumbnail and 500 arriving at once
 * do not each touch 500. Two nodes with one picture share one object URL, which goes when the last stops showing it.
 */
export class PreviewLoader {
  private readonly slots = new Map<number, NodeSlot>();
  private readonly pictures = new Map<string, Picture>();
  private readonly fetching = new Map<string, Promise<Blob>>();
  private readonly queue: number[] = [];
  private readonly lost = new Set<number>();
  private active = 0;

  constructor(
    private readonly source: PreviewSource,
    /** A hash the Server no longer has: its cache was deleted. */
    private readonly onLost: () => void
  ) {}

  /** The picture to show for a node. It keeps its signal for as long as the node exists. */
  shown(node: number): Signal<ShownPreview | null> {
    return this.slot(node).shown;
  }

  /** The newest preview of a node. Asking again for what is on screen does nothing. */
  want(preview: GraphPreview): void {
    const slot = this.slot(preview.node);
    slot.wanted = preview;
    if (slot.shown()?.hash === preview.hash) {
      return;
    }

    this.enqueue(preview.node, slot);
    this.pump();
  }

  /** A node has gone. */
  forget(node: number): void {
    const slot = this.slots.get(node);
    if (slot) {
      this.release(slot.shown()?.hash);
      slot.shown.set(null);
      slot.wanted = null;
      this.slots.delete(node);
    }
  }

  /** Whatever a deleted cache lost, again: an evaluation has written the files back. */
  retryLost(): void {
    for (const node of this.lost) {
      const wanted = this.slots.get(node)?.wanted;
      if (wanted) {
        this.want(wanted);
      }
    }

    this.lost.clear();
  }

  dispose(): void {
    for (const node of [...this.slots.keys()]) {
      this.forget(node);
    }

    this.queue.length = 0;
  }

  private slot(node: number): NodeSlot {
    let slot = this.slots.get(node);
    if (!slot) {
      slot = { shown: signal(null), wanted: null, queued: false, loading: false };
      this.slots.set(node, slot);
    }

    return slot;
  }

  private pump(): void {
    while (this.active < RequestLimit && this.queue.length > 0) {
      const node = this.queue.shift() as number;
      const slot = this.slots.get(node);
      if (slot) {
        slot.queued = false;
        if (slot.wanted) {
          this.active++;
          slot.loading = true;
          void this.load(node, slot, slot.wanted);
        }
      }
    }
  }

  private enqueue(node: number, slot: NodeSlot): void {
    if (!slot.queued && !slot.loading) {
      slot.queued = true;
      this.queue.push(node);
    }
  }

  private async load(node: number, slot: NodeSlot, wanted: GraphPreview): Promise<void> {
    try {
      const url = await this.urlOf(wanted.hash);
      if (this.slots.get(node) === slot) {
        this.show(slot, wanted, url);
      } else {
        this.release(wanted.hash);
      }
    } catch {
      this.lost.add(node);
      this.onLost();
    } finally {
      this.active--;
      slot.loading = false;
      if (slot.wanted && slot.wanted.hash !== wanted.hash) {
        // Something newer came while this was on its way. This one was still newer than what it replaced, so it shows
        // until that arrives, and a fast slider's thumbnail keeps moving instead of waiting for the hand to stop.
        this.enqueue(node, slot);
      }

      this.pump();
    }
  }

  private show(slot: NodeSlot, preview: GraphPreview, url: string): void {
    this.release(slot.shown()?.hash);
    slot.shown.set({ hash: preview.hash, url, width: preview.width, height: preview.height });
  }

  /** A use of the picture for a hash, which the caller gives back with {@link release}. */
  private async urlOf(hash: string): Promise<string> {
    const held = this.pictures.get(hash);
    if (held) {
      held.uses++;
      return held.url;
    }

    let fetching = this.fetching.get(hash);
    if (!fetching) {
      fetching = this.source.fetch(hash).finally(() => this.fetching.delete(hash));
      this.fetching.set(hash, fetching);
    }

    const blob = await fetching;
    const picture = this.pictures.get(hash) ?? { url: this.source.url(blob), uses: 0 };
    picture.uses++;
    this.pictures.set(hash, picture);
    return picture.url;
  }

  private release(hash: string | undefined): void {
    const picture = hash === undefined ? undefined : this.pictures.get(hash);
    if (picture && hash !== undefined && --picture.uses === 0) {
      this.source.revoke(picture.url);
      this.pictures.delete(hash);
    }
  }
}
