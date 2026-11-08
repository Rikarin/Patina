import { type CollectionViewer, DataSource, type ListRange } from '@angular/cdk/collections';
import { signal } from '@angular/core';
import { BehaviorSubject, type Observable, type Subscription } from 'rxjs';
import { messageOf } from '../core/messages';

/** One page of a list, as every library endpoint answers it. */
export interface Page<T> {
  items: readonly T[];
  total: number;
}

/** Fetches one page. The signal fires when nobody wants the page any more. */
export type PageSource<T> = (offset: number, limit: number, signal: AbortSignal) => Promise<Page<T>>;

export interface PagedListOptions {
  pageSize?: number;
  /** Pages kept in memory: enough to scroll back without a fetch, too few to ever hold a large library. */
  maxPages?: number;
  /** The shortest time between looks at what the viewport needs, so that a dragged scrollbar asks for little of what it flies past. */
  settleMs?: number;
}

const firstRange: ListRange = { start: 0, end: 1 };

/**
 * A list of any length, fetched a page at a time as a virtual scroll viewport asks for ranges (plan §5: nothing loads a
 * whole library). What it emits has one slot per item, `undefined` until the item's page arrives, so the viewport knows
 * the full height from the first page while holding only a bounded number of items.
 *
 * Only the pages of the range on screen are fetched: leaving a range aborts what was in flight for it, and a reply that
 * arrives after its abort is dropped, so a slow answer to an old query can never replace a newer one.
 */
export class PagedList<T> extends DataSource<T | undefined> {
  private readonly pageSize: number;
  private readonly maxPages: number;
  private readonly settleMs: number;
  /** Always the same array, so that a page costs its own items and not a copy of the whole list. */
  private readonly slots: (T | undefined)[] = [];
  private readonly emitted = new BehaviorSubject<readonly (T | undefined)[]>(this.slots);
  /** Pages held, least recently seen first. */
  private readonly held = new Set<number>();
  private readonly flights = new Map<number, AbortController>();
  private readonly viewers = new Map<CollectionViewer, Subscription>();
  private range: ListRange = firstRange;
  private settling: ReturnType<typeof setTimeout> | null = null;

  /** How many items match, or null until the first page arrives. */
  readonly total = signal<number | null>(null);
  readonly loading = signal(false);
  /** Why the last fetch failed, until {@link retry} or a later fetch succeeds. */
  readonly error = signal<string | null>(null);

  constructor(
    private readonly source: PageSource<T>,
    options: PagedListOptions = {}
  ) {
    super();
    this.pageSize = options.pageSize ?? 100;
    this.maxPages = options.maxPages ?? 20;
    this.settleMs = options.settleMs ?? 50;
  }

  /** How many items are in memory: the bound that makes this safe at any size. */
  get heldItems(): number {
    return this.held.size * this.pageSize;
  }

  /** The item at an index, if its page is here. */
  at(index: number): T | undefined {
    return this.slots[index];
  }

  /** A viewer starts at the top: a viewport tells a new source nothing about a range it is already showing. */
  connect(viewer: CollectionViewer): Observable<readonly (T | undefined)[]> {
    this.range = { start: 0, end: this.pageSize };
    this.viewers.set(
      viewer,
      viewer.viewChange.subscribe(range => {
        this.show(range);
      })
    );
    this.load();
    return this.emitted;
  }

  disconnect(viewer: CollectionViewer): void {
    this.viewers.get(viewer)?.unsubscribe();
    this.viewers.delete(viewer);
    if (this.viewers.size === 0) {
      this.cancel();
    }
  }

  /** The viewport moved. Fetching looks at the newest range a moment later, so that most of what flies past is skipped. */
  show(range: ListRange): void {
    this.range = range;
    for (const page of this.visiblePages()) {
      if (this.held.delete(page)) {
        this.held.add(page); // most recently seen
      }
    }

    // A rate, not a debounce: a list that keeps moving has to keep filling in, or a long scroll would show no rows at all.
    this.settling ??= setTimeout(() => {
      this.settling = null;
      this.load();
    }, this.settleMs);
  }

  /** Forgets everything: the query changed, so no row on screen means anything now. */
  reset(): void {
    this.cancel();
    this.held.clear();
    this.slots.length = 0;
    this.range = firstRange;
    this.total.set(null);
    this.error.set(null);
    this.emitted.next(this.slots);
    this.load();
  }

  /** Fetches again what is on screen, which stays up until the new answer replaces it: the same query, newer data. */
  refresh(): void {
    this.cancel();
    const visible = new Set(this.visiblePages());
    for (const page of this.held) {
      if (!visible.has(page)) {
        this.clear(page);
      }
    }

    this.held.clear();
    this.error.set(null);
    this.load();
  }

  /** Tries again after a failure. */
  retry(): void {
    this.error.set(null);
    this.load();
  }

  /** Stops everything in flight, for good unless a viewer asks again. */
  cancel(): void {
    if (this.settling !== null) {
      clearTimeout(this.settling);
      this.settling = null;
    }

    for (const flight of this.flights.values()) {
      flight.abort();
    }

    this.flights.clear();
    this.loading.set(false);
  }

  private load(): void {
    if (this.viewers.size === 0) {
      return; // nobody is looking; the next viewer starts this
    }

    const wanted = new Set(this.visiblePages());
    for (const [page, flight] of this.flights) {
      if (!wanted.has(page)) {
        flight.abort();
        this.flights.delete(page);
      }
    }

    for (const page of wanted) {
      if (!this.held.has(page) && !this.flights.has(page)) {
        void this.fetch(page);
      }
    }

    this.loading.set(this.flights.size > 0);
  }

  private async fetch(page: number): Promise<void> {
    const flight = new AbortController();
    this.flights.set(page, flight);
    try {
      const answer = await this.source(page * this.pageSize, this.pageSize, flight.signal);
      if (!flight.signal.aborted) {
        this.accept(page, answer);
      }
    } catch (error) {
      if (!flight.signal.aborted) {
        this.error.set(messageOf(error));
      }
    } finally {
      if (this.flights.get(page) === flight) {
        this.flights.delete(page);
        this.loading.set(this.flights.size > 0);
      }
    }
  }

  private accept(page: number, answer: Page<T>): void {
    this.slots.length = answer.total;
    const start = page * this.pageSize;
    this.slots.fill(undefined, start, Math.min(start + this.pageSize, answer.total));
    answer.items.forEach((item, index) => (this.slots[start + index] = item));
    this.held.delete(page);
    this.held.add(page);
    this.evict();
    this.total.set(answer.total);
    this.error.set(null);
    this.emitted.next(this.slots);
  }

  private evict(): void {
    const visible = new Set(this.visiblePages());
    for (const page of this.held) {
      if (this.held.size <= this.maxPages) {
        return;
      }

      if (!visible.has(page)) {
        this.held.delete(page);
        this.clear(page);
      }
    }
  }

  private clear(page: number): void {
    const start = page * this.pageSize;
    this.slots.fill(undefined, Math.min(start, this.slots.length), Math.min(start + this.pageSize, this.slots.length));
  }

  private visiblePages(): number[] {
    const end = Math.max(this.range.end, this.range.start + 1);
    const known = this.total();
    const last = known === null ? end : Math.min(end, Math.max(known, 1));
    const first = Math.min(this.range.start, last - 1);
    const pages: number[] = [];
    for (let page = Math.floor(first / this.pageSize); page * this.pageSize < last; page++) {
      pages.push(page);
    }

    return pages;
  }
}
