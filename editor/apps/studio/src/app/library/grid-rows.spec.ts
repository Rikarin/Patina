import type { ListRange } from '@angular/cdk/collections';
import { Subject } from 'rxjs';
import { type GridRow, GridRows } from './grid-rows';
import { type Page, PagedList } from './paged-list';

const SettleMs = 5;
const settled = () => new Promise(done => setTimeout(done, SettleMs * 4));

/** A list of `total` items whose pages arrive at once, shown as rows of `columns`. */
function harness(total: number, columns: number) {
  const asked: { offset: number; limit: number }[] = [];
  const list = new PagedList<string>(
    (offset, limit) => {
      asked.push({ offset, limit });
      const page: Page<string> = {
        items: Array.from({ length: Math.max(0, Math.min(limit, total - offset)) }, (_, i) => `item ${offset + i}`),
        total
      };
      return Promise.resolve(page);
    },
    { pageSize: 10, settleMs: SettleMs }
  );
  const shown: string[][] = [];
  const rows = new GridRows(list, columns, items => shown.push([...items]));
  const viewChange = new Subject<ListRange>();
  let latest: readonly GridRow<string>[] = [];
  rows.connect({ viewChange }).subscribe(data => (latest = data));
  return { asked, rows, viewChange, shown, latest: () => latest };
}

describe('GridRows', () => {
  it('lays the items out in rows of the columns asked for, the last row short', async () => {
    const { latest } = harness(7, 3);
    await settled();

    expect(latest().map(row => row.items)).toEqual([
      ['item 0', 'item 1', 'item 2'],
      ['item 3', 'item 4', 'item 5'],
      ['item 6']
    ]);
    expect(latest().map(row => row.first)).toEqual([0, 3, 6]);
  });

  it('fetches the items of the rows on screen, not the rows', async () => {
    const { asked, viewChange } = harness(1_000, 4);
    await settled();

    viewChange.next({ start: 50, end: 53 });
    await settled();

    // Rows 50 to 53 hold items 200 to 211, which are pages 20 and 21 of ten.
    expect(asked.map(page => page.offset)).toEqual(expect.arrayContaining([200, 210]));
    expect(asked.map(page => page.offset)).not.toContain(50);
  });

  it('tells what is on screen once it has arrived, so its pictures can be asked for', async () => {
    const { shown, viewChange } = harness(100, 2);
    viewChange.next({ start: 0, end: 2 });
    await settled();

    expect(shown.at(-1)).toEqual(['item 0', 'item 1', 'item 2', 'item 3']);
  });
});
