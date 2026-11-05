import type { CollectionViewer, ListRange } from '@angular/cdk/collections';
import { Subject } from 'rxjs';
import { type Page, PagedList } from './paged-list';

const SettleMs = 10;

interface Call {
  offset: number;
  limit: number;
  signal: AbortSignal;
  answer: (page: Page<string>) => void;
  fail: (error: Error) => void;
}

/** A source whose pages arrive when the test says, and a viewer to drive it. */
function harness(options: { pageSize?: number; maxPages?: number } = {}) {
  const calls: Call[] = [];
  const list = new PagedList<string>(
    (offset, limit, signal) =>
      new Promise((answer, fail) => {
        calls.push({ offset, limit, signal, answer, fail });
      }),
    { pageSize: 10, settleMs: SettleMs, ...options }
  );
  const viewChange = new Subject<ListRange>();
  const viewer: CollectionViewer = { viewChange };
  let latest: readonly (string | undefined)[] = [];
  list.connect(viewer).subscribe(data => (latest = data));
  const pageOf = (offset: number, total: number): Page<string> => ({
    items: Array.from({ length: Math.min(10, total - offset) }, (_, i) => `item ${offset + i}`),
    total
  });
  return { calls, list, viewer, viewChange, pageOf, slots: () => latest };
}

const settled = () => new Promise(done => setTimeout(done, SettleMs * 3));

describe('PagedList', () => {
  it('asks for the first page as soon as a viewer connects', () => {
    const { calls, list } = harness();

    expect(calls.map(call => call.offset)).toEqual([0]);
    expect(list.total()).toBeNull();
    expect(list.loading()).toBe(true);
  });

  it('holds one slot per item, whatever has arrived', async () => {
    const { calls, list, pageOf, slots } = harness();

    calls[0]?.answer(pageOf(0, 10_000));
    await settled();

    expect(slots()).toHaveLength(10_000);
    expect(slots()[3]).toBe('item 3');
    expect(slots()[10]).toBeUndefined();
    expect(list.total()).toBe(10_000);
    expect(list.loading()).toBe(false);
  });

  it('fetches only the pages of the range it is shown', async () => {
    const { calls, list, pageOf, viewChange } = harness();
    calls[0]?.answer(pageOf(0, 10_000));
    await settled();

    viewChange.next({ start: 5005, end: 5025 });
    await settled();

    expect(calls.map(call => call.offset)).toEqual([0, 5000, 5010, 5020]);
    expect(list.loading()).toBe(true);
  });

  it('keeps filling in while the list never stops moving', async () => {
    const { calls, pageOf, viewChange } = harness();
    calls[0]?.answer(pageOf(0, 10_000));
    await settled();

    for (let start = 100; start < 400; start += 10) {
      viewChange.next({ start, end: start + 5 });
      await new Promise(done => setTimeout(done, 2));
    }

    await settled();
    expect(calls.length).toBeGreaterThan(2);
    expect(calls.length).toBeLessThan(30);
  });

  it('aborts what it no longer needs and ignores a late answer', async () => {
    const { calls, pageOf, slots, viewChange } = harness();
    calls[0]?.answer(pageOf(0, 10_000));
    await settled();
    viewChange.next({ start: 500, end: 505 });
    await settled();
    const stale = calls[1];

    viewChange.next({ start: 900, end: 905 });
    await settled();
    stale?.answer(pageOf(500, 10_000));
    await settled();

    expect(stale?.signal.aborted).toBe(true);
    expect(slots()[500]).toBeUndefined();
  });

  it('never holds more than its limit of pages', async () => {
    const { calls, list, pageOf, slots, viewChange } = harness({ maxPages: 3 });
    calls[0]?.answer(pageOf(0, 10_000));
    await settled();

    for (const start of [100, 200, 300, 400, 500]) {
      viewChange.next({ start, end: start + 5 });
      await settled();
      calls.at(-1)?.answer(pageOf(start, 10_000));
      await settled();
    }

    expect(list.heldItems).toBe(30);
    expect(slots().filter(slot => slot !== undefined)).toHaveLength(30);
    expect(slots()[505]).toBe('item 505');
    expect(slots()[0]).toBeUndefined();
  });

  it('fetches a page again after it was evicted', async () => {
    const { calls, pageOf, viewChange } = harness({ maxPages: 1 });
    calls[0]?.answer(pageOf(0, 100));
    await settled();
    viewChange.next({ start: 50, end: 55 });
    await settled();
    calls[1]?.answer(pageOf(50, 100));
    await settled();

    viewChange.next({ start: 0, end: 5 });
    await settled();

    expect(calls.map(call => call.offset)).toEqual([0, 50, 0]);
  });

  it('forgets everything on a reset, and an old answer cannot come back', async () => {
    const { calls, list, pageOf, slots } = harness();
    const old = calls[0];

    list.reset();
    old?.answer(pageOf(0, 500));
    await settled();

    expect(old?.signal.aborted).toBe(true);
    expect(list.total()).toBeNull();
    expect(slots()).toHaveLength(0);
    expect(calls).toHaveLength(2);
  });

  it('keeps the rows up during a refresh, then replaces them', async () => {
    const { calls, list, pageOf, slots } = harness();
    calls[0]?.answer(pageOf(0, 100));
    await settled();

    list.refresh();

    expect(slots()[0]).toBe('item 0');
    calls[1]?.answer({ items: ['new'], total: 1 });
    await settled();
    expect(slots()).toEqual(['new']);
    expect(list.total()).toBe(1);
  });

  it('reports a failure, and a retry fetches again', async () => {
    const { calls, list, pageOf } = harness();

    calls[0]?.fail(new Error('refused'));
    await settled();
    expect(list.error()).toBe('refused');
    expect(list.loading()).toBe(false);

    list.retry();
    calls[1]?.answer(pageOf(0, 10));
    await settled();
    expect(list.error()).toBeNull();
    expect(list.total()).toBe(10);
  });

  it('is quiet with no viewer', async () => {
    const { calls, list, viewer } = harness();
    calls[0]?.answer({ items: [], total: 0 });
    await settled();
    list.disconnect(viewer);

    list.reset();
    await settled();

    expect(calls).toHaveLength(1);
  });

  it('starts a returning viewer from the top', async () => {
    const { calls, list, pageOf, viewChange, viewer } = harness();
    calls[0]?.answer(pageOf(0, 1000));
    await settled();
    viewChange.next({ start: 900, end: 905 });
    await settled();
    list.disconnect(viewer);
    list.reset();

    list.connect(viewer);

    expect(calls.at(-1)?.offset).toBe(0);
  });
});
