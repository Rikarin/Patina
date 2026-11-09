import { type CollectionViewer, DataSource, type ListRange } from '@angular/cdk/collections';
import { type Observable, map, tap } from 'rxjs';
import type { PagedList } from './paged-list';

/** One row of a list laid out in columns: the index of its first item, and the items, each undefined until loaded. */
export interface GridRow<T> {
  readonly row: number;
  readonly first: number;
  readonly items: readonly (T | undefined)[];
}

/**
 * A paged list as rows of a few items, for a virtual viewport that scrolls rows: the Library's grid, and its list as
 * one column. Rows on screen become the items the list fetches, so a grid over 10,000 projects still costs what it
 * shows. Each answer is new row objects, so a row whose page arrived draws again.
 */
export class GridRows<T> extends DataSource<GridRow<T>> {
  private proxy: CollectionViewer | null = null;
  private range: ListRange = { start: 0, end: 0 };

  /**
   * @param list The items.
   * @param columns How many to a row.
   * @param shown Told the items of the rows on screen whenever they change or arrive, for what goes with them, such as
   * their pictures.
   */
  constructor(
    private readonly list: PagedList<T>,
    readonly columns: number,
    private readonly shown: (items: readonly T[]) => void = () => undefined
  ) {
    super();
  }

  connect(viewer: CollectionViewer): Observable<readonly GridRow<T>[]> {
    const columns = this.columns;
    const proxy: CollectionViewer = {
      viewChange: viewer.viewChange.pipe(
        tap(range => {
          this.range = range;
          this.tell(); // rows scrolled back to are loaded already, and no page arrives to say so
        }),
        map(range => ({ start: range.start * columns, end: range.end * columns }))
      )
    };
    this.proxy = proxy;
    return this.list.connect(proxy).pipe(
      map(() => this.rows()),
      tap(() => this.tell())
    );
  }

  disconnect(): void {
    if (this.proxy) {
      this.list.disconnect(this.proxy);
      this.proxy = null;
    }
  }

  private rows(): GridRow<T>[] {
    const total = this.list.total() ?? 0;
    const rows: GridRow<T>[] = [];
    for (let row = 0, first = 0; first < total; row += 1, first += this.columns) {
      const items: (T | undefined)[] = [];
      for (let index = first; index < Math.min(total, first + this.columns); index += 1) {
        items.push(this.list.at(index));
      }

      rows.push({ row, first, items });
    }

    return rows;
  }

  private tell(): void {
    const items: T[] = [];
    for (let index = this.range.start * this.columns; index < this.range.end * this.columns; index += 1) {
      const item = this.list.at(index);
      if (item !== undefined) {
        items.push(item);
      }
    }

    if (items.length > 0) {
      this.shown(items);
    }
  }
}
