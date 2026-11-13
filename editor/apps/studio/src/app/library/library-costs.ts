import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { type components, unwrap } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiSelectImports } from '@xui/select';
import { bytes, count } from '../core/numbers';
import { LibraryStore } from './library-store';

type LibraryCosts = components['schemas']['LibraryCosts'];
type DocumentCost = components['schemas']['DocumentCost'];

const MiB = 1024 * 1024;

/** The budgets a document can be held to, each a size of its maps together. */
const Budgets: readonly number[] = [4 * MiB, 16 * MiB, 64 * MiB, 256 * MiB];

/** How many documents over the budget the list shows; the total says how many there are. */
const OverShown = 50;

/**
 * What the library costs (#207), while nothing in it is chosen: its documents' maps, uncompressed at each one's size,
 * by folder, costliest first, and a click goes into a folder. Below, the documents in the folder that cost more than a
 * budget. Both are the engine's index, so neither opens a document.
 */
@Component({
  selector: 'ptn-library-costs',
  imports: [XuiButtonImports, XuiSelectImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex min-h-0 flex-1 flex-col', role: 'group', 'aria-label': 'Texture memory' },
  template: `
    <div class="chrome-bar flex h-8 shrink-0 items-center gap-1 px-2" data-testid="costs-path">
      <button type="button" xuiButton size="sm" variant="ghost" (click)="folder.set('')">Library</button>
      @for (step of steps(); track step.path) {
        <span class="text-foreground-muted">/</span>
        <button type="button" xuiButton size="sm" variant="ghost" (click)="folder.set(step.path)">
          {{ step.name }}
        </button>
      }
      <span class="flex-1"></span>
      @if (costs(); as answer) {
        <span
          class="readout shrink-0"
          data-testid="costs-total"
          title="What the documents' maps hold together, uncompressed at each document's size, before an export compresses them"
        >
          {{ size(answer.bytes) }}
        </span>
      }
    </div>
    <ol class="min-h-0 flex-1 overflow-y-auto py-1" aria-label="Texture memory by folder" data-testid="costs-list">
      @for (item of costs()?.items ?? []; track item.name) {
        <li>
          <button
            type="button"
            class="hover:bg-hover flex w-full items-center gap-2 px-3 py-0.5 text-left text-sm"
            data-testid="costs-item"
            [disabled]="isDocument(item.name)"
            [title]="isDocument(item.name) ? item.name : 'Show what is in ' + item.name"
            (click)="enter(item.name)"
          >
            <span class="min-w-0 flex-1 truncate">{{ item.name }}</span>
            <span class="text-foreground-muted shrink-0 text-xs">{{ documents(item.documents) }}</span>
            <span class="readout w-20 shrink-0 text-right">{{ size(item.bytes) }}</span>
          </button>
        </li>
      } @empty {
        <li class="text-foreground-muted px-3 py-1 text-sm">No document here has maps to count yet.</li>
      }
    </ol>
    <div class="chrome-bar border-hairline flex h-8 shrink-0 items-center gap-2 border-t px-2">
      <span class="section-label flex-1">Over budget</span>
      <xui-select
        class="w-28"
        size="sm"
        aria-label="Budget for one document"
        data-testid="costs-budget"
        [filterable]="false"
        [items]="budgets"
        [itemText]="size"
        [value]="budget()"
        (valueChange)="budget.set($event ?? budget())"
      />
    </div>
    <ol class="max-h-[40%] shrink-0 overflow-y-auto py-1" aria-label="Documents over budget" data-testid="costs-over">
      @for (item of over(); track item.path) {
        <li class="flex items-center gap-2 px-3 py-0.5 text-sm" data-testid="costs-over-item" [title]="item.path">
          <span class="min-w-0 flex-1 truncate">{{ nameOf(item.path) }}</span>
          <span class="text-foreground-muted shrink-0 text-xs">{{ shape(item) }}</span>
          <span class="readout w-20 shrink-0 text-right">{{ size(item.bytes) }}</span>
        </li>
      } @empty {
        <li class="text-foreground-muted px-3 py-1 text-sm">Every document here fits.</li>
      }
      @if (overTotal() > over().length) {
        <li class="text-foreground-muted px-3 py-1 text-xs">and {{ documents(overTotal() - over().length) }} more</li>
      }
    </ol>
  `
})
export class LibraryCostsView {
  private readonly store = inject(LibraryStore);
  protected readonly budgets = Budgets;
  /** The folder shown, below the library's projects, empty for the whole library. */
  protected readonly folder = signal('');
  protected readonly budget = signal(Budgets[1] ?? 16 * MiB);
  protected readonly costs = signal<LibraryCosts | null>(null);
  protected readonly over = signal<readonly DocumentCost[]>([]);
  protected readonly overTotal = signal(0);
  protected readonly size = bytes;

  /** Each folder on the way to the one shown, for going back up. */
  protected readonly steps = computed(() => {
    const parts = this.folder()
      .split('/')
      .filter(part => part.length > 0);
    return parts.map((name, index) => ({ name, path: parts.slice(0, index + 1).join('/') }));
  });

  constructor() {
    effect(() => {
      const folder = this.folder();
      const budget = this.budget();
      untracked(() => void this.read(folder, budget));
    });
  }

  protected documents(total: number): string {
    return total === 1 ? '1 document' : `${count(total)} documents`;
  }

  protected shape(item: DocumentCost): string {
    const maps = item.maps === 1 ? '1 map' : `${count(item.maps)} maps`;
    return `${count(item.width)} × ${count(item.height)}, ${maps}`;
  }

  protected nameOf(path: string): string {
    return path.split('/').pop() ?? path;
  }

  /** A stack's file, which is the last step down: there is nothing in it to show. */
  protected isDocument(name: string): boolean {
    return name.includes('.');
  }

  protected enter(name: string): void {
    this.folder.update(folder => (folder ? `${folder}/${name}` : name));
  }

  private reading = '';

  private async read(folder: string, budget: number): Promise<void> {
    const key = `${folder}|${budget}`;
    this.reading = key;
    try {
      const client = await this.store.api();
      const [costs, over] = await Promise.all([
        client.GET('/api/v1/library/costs', { params: { query: { folder } } }),
        client.GET('/api/v1/library/costs/documents', {
          params: { query: { folder, over: budget, offset: 0, limit: OverShown } }
        })
      ]);
      if (this.reading === key) {
        this.costs.set(unwrap(costs));
        const page = unwrap(over);
        this.over.set(page.items);
        this.overTotal.set(page.total);
      }
    } catch {
      if (this.reading === key) {
        this.costs.set(null);
        this.over.set([]);
        this.overTotal.set(0);
      }
    }
  }
}
