import { CdkVirtualScrollViewport, ScrollingModule } from '@angular/cdk/scrolling';
import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, viewChild } from '@angular/core';
import type { AssetSummary, ProjectSummary } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiInputImports } from '@xui/input';
import { XuiMultiSelectImports } from '@xui/multi-select';
import { XuiNonIdealStateImports } from '@xui/non-ideal-state';
import { XuiSegmentedControlImports, type XuiSegmentedOption } from '@xui/segmented-control';
import { XuiSkeletonImports } from '@xui/skeleton';
import { XuiSpinnerImports } from '@xui/spinner';
import { LibrarySelection } from './library-selection';
import { LibraryStore, type LibraryView } from './library-store';
import { counted, nextIndex } from './list-keys';
import { NewProjectDialog } from './new-project-dialog';
import { ViewportFit } from './viewport-fit';

type ViewName = 'projects' | 'assets';
type LibraryRow = ProjectSummary | AssetSummary;

const RowHeight = 40;

const views: readonly XuiSegmentedOption<ViewName>[] = [
  { value: 'projects', label: 'Projects' },
  { value: 'assets', label: 'Shared Assets' }
];

function isAsset(row: LibraryRow): row is AssetSummary {
  return 'kind' in row;
}

/** The Library browser: projects or shared assets, searched and filtered by the Server, a page at a time. */
@Component({
  selector: 'ptn-library',
  imports: [
    NewProjectDialog,
    ScrollingModule,
    ViewportFit,
    XuiButtonImports,
    XuiInputImports,
    XuiMultiSelectImports,
    XuiNonIdealStateImports,
    XuiSegmentedControlImports,
    XuiSkeletonImports,
    XuiSpinnerImports
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col' },
  template: `
    @let state = store.state();
    @switch (state.kind) {
      @case ('loading') {
        <div class="grid flex-1 place-items-center">
          <xui-spinner size="md" aria-label="Opening the library" />
        </div>
      }
      @case ('none') {
        <xui-non-ideal-state
          class="flex-1"
          title="No library open"
          description="Start the Server with --library and a folder. patina library new makes one."
        />
      }
      @case ('error') {
        <xui-non-ideal-state class="flex-1" title="The library cannot be reached" [description]="state.reason">
          <button action type="button" xuiButton size="sm" (click)="store.retry()">Try again</button>
        </xui-non-ideal-state>
      }
      @case ('ready') {
        <div class="chrome-bar flex h-9 shrink-0 items-center px-2">
          <xui-segmented-control
            class="w-full"
            size="sm"
            fill
            aria-label="Library view"
            [options]="views"
            [value]="viewName()"
            (valueChange)="switchTo($event)"
          />
        </div>
        <div class="flex shrink-0 flex-col gap-1.5 p-2">
          <input
            xuiInput
            size="sm"
            type="search"
            aria-label="Search the library"
            data-testid="library-search"
            placeholder="Search names, tags and paths"
            [value]="view().search()"
            (input)="search($event)"
          />
          <xui-multi-select
            data-testid="library-tags"
            placeholder="Filter by tag"
            noResultsText="No such tag"
            [items]="tagNames()"
            [values]="view().tags()"
            (valuesChange)="filter($event)"
          />
        </div>

        <div class="well relative min-h-0 flex-1 overflow-hidden">
          <cdk-virtual-scroll-viewport
            ptnViewportFit
            class="size-full outline-none"
            role="listbox"
            tabindex="0"
            data-testid="library-list"
            minBufferPx="400"
            maxBufferPx="800"
            [attr.aria-label]="viewName() === 'projects' ? 'Projects' : 'Shared assets'"
            [attr.aria-activedescendant]="active() === null ? null : 'library-row-' + active()"
            [itemSize]="rowHeight"
            (click)="click($event)"
            (keydown)="press($event)"
          >
            <div
              *cdkVirtualFor="let row of view().list; let index = index; trackBy: byIndex"
              class="border-hairline flex items-center gap-2 border-b px-2"
              role="option"
              data-testid="library-row"
              [id]="'library-row-' + index"
              [style.height.px]="rowHeight"
              [class.bg-selection]="row && row.id === selectedId()"
              [class.outline]="index === active()"
              [class.-outline-offset-1]="index === active()"
              [class.outline-focus]="index === active()"
              [attr.aria-selected]="!!row && row.id === selectedId()"
              [attr.aria-posinset]="index + 1"
              [attr.aria-setsize]="view().list.total()"
              [attr.data-index]="index"
            >
              @if (row) {
                <span class="min-w-0 flex-1 leading-tight">
                  <span class="block truncate">{{ row.name }}</span>
                  <span class="text-foreground-muted block truncate text-xs">{{ detail(row) }}</span>
                </span>
                <span class="readout text-foreground-muted shrink-0 text-xs" [title]="countLabel(row)">
                  {{ count(row) }}
                </span>
              } @else {
                <span class="flex min-w-0 flex-1 flex-col gap-1.5">
                  <xui-skeleton class="h-2.5 w-2/3" />
                  <xui-skeleton class="h-2 w-1/2" />
                </span>
              }
            </div>
          </cdk-virtual-scroll-viewport>

          @if (overlay(); as message) {
            <xui-non-ideal-state
              class="absolute inset-0 m-auto h-fit"
              [title]="message.title"
              [description]="message.description"
            >
              @if (message.retry) {
                <button action type="button" xuiButton size="sm" (click)="view().list.retry()">Try again</button>
              }
            </xui-non-ideal-state>
          } @else if (view().list.error(); as reason) {
            <p class="bg-error-subtle text-error absolute inset-x-0 bottom-0 flex items-center gap-2 px-2 py-1 text-xs">
              <span class="min-w-0 flex-1 truncate" [title]="reason">{{ reason }}</span>
              <button type="button" xuiButton size="sm" (click)="view().list.retry()">Try again</button>
            </p>
          }
        </div>

        <footer class="chrome-bar flex h-8 shrink-0 items-center gap-2 px-2">
          <span class="readout truncate" data-testid="library-count">{{ countText() }}</span>
          @if (state.info.indexing) {
            <span class="text-foreground-muted flex items-center gap-1 text-xs" data-testid="library-indexing">
              <xui-spinner size="xs" aria-hidden="true" />
              Indexing
            </span>
          }
          <span class="flex-1"></span>
          @if (viewName() === 'projects') {
            <button type="button" xuiButton size="sm" (click)="creating.set(true)">New…</button>
          }
        </footer>
        <ptn-new-project [(open)]="creating" />
      }
    }
  `
})
export class LibraryPane {
  protected readonly store = inject(LibraryStore);
  private readonly selection = inject(LibrarySelection);
  private readonly viewport = viewChild(CdkVirtualScrollViewport);

  protected readonly views = views;
  protected readonly rowHeight = RowHeight;
  protected readonly viewName = signal<ViewName>('projects');
  protected readonly creating = signal(false);
  /** The keyboard's row, which can be one whose page has not arrived yet. */
  protected readonly active = signal<number | null>(null);
  protected readonly selectedId = computed(() => this.selection.id());

  protected readonly view = computed<LibraryView<LibraryRow>>(() =>
    this.viewName() === 'projects'
      ? (this.store.projects as LibraryView<LibraryRow>)
      : (this.store.assets as LibraryView<LibraryRow>)
  );

  protected readonly tagNames = computed(() =>
    this.view()
      .tagOptions()
      .map(option => option.tag)
  );

  protected readonly countText = computed(() => {
    const total = this.view().list.total();
    if (total === null) {
      return '…';
    }

    return counted(total, this.viewName() === 'projects' ? 'project' : 'shared asset');
  });

  /** What the list area says instead of rows. */
  protected readonly overlay = computed(() => {
    const view = this.view();
    const { error, total } = view.list;
    if (total() === null && error() !== null) {
      return { title: 'The list did not load', description: error(), retry: true };
    }

    if (total() !== 0) {
      return null;
    }

    if (view.filtered) {
      return { title: 'Nothing matches', description: 'Try fewer words or fewer tags.', retry: false };
    }

    const projects = this.viewName() === 'projects';
    const state = this.store.state();
    if (state.kind === 'ready' && state.info.indexing) {
      return { title: 'Indexing the library', description: 'This list fills in when the scan ends.', retry: false };
    }

    return projects
      ? { title: 'No projects yet', description: 'Make one with New.', retry: false }
      : { title: 'No shared assets yet', description: 'Assets live under shared/ in the library.', retry: false };
  });

  constructor() {
    // The list stops at its last row, so an index past it after a smaller result set has no row to land on.
    effect(() => {
      const total = this.view().list.total();
      this.active.update(index => (index !== null && total !== null && index >= total ? null : index));
    });
  }

  protected switchTo(name: ViewName | null): void {
    if (name !== null && name !== this.viewName()) {
      this.viewName.set(name);
      this.toTop();
    }
  }

  protected search(event: Event): void {
    this.view().setSearch((event.target as HTMLInputElement).value);
    this.toTop();
  }

  protected filter(tags: string[]): void {
    this.view().setTags(tags);
    this.toTop();
  }

  /** One listener for every row, rather than one per row recycled by the viewport. */
  protected click(event: MouseEvent): void {
    const index = (event.target as HTMLElement).closest('[data-index]')?.getAttribute('data-index');
    if (index === undefined || index === null) {
      return;
    }

    this.active.set(Number(index));
    const row = this.view().list.at(Number(index));
    if (row) {
      this.select(row);
    }
  }

  protected press(event: KeyboardEvent): void {
    const list = this.view().list;
    if (event.key === 'Enter' || event.key === ' ') {
      const index = this.active();
      const row = index === null ? undefined : list.at(index);
      if (row) {
        this.select(row);
      }

      event.preventDefault();
      return;
    }

    const rowsPerPage = Math.max(1, Math.floor((this.viewport()?.getViewportSize() ?? 0) / RowHeight) - 1);
    const next = nextIndex(event.key, this.active(), list.total() ?? 0, rowsPerPage);
    if (next === null) {
      return;
    }

    event.preventDefault();
    this.active.set(next);
    this.reveal(next);
    const row = list.at(next);
    if (row) {
      this.select(row);
    }
  }

  protected detail(row: LibraryRow): string {
    return isAsset(row) ? row.kind : row.path;
  }

  protected count(row: LibraryRow): number {
    return isAsset(row) ? row.usageCount : row.referenceCount;
  }

  protected countLabel(row: LibraryRow): string {
    return isAsset(row)
      ? `Used by ${counted(row.usageCount, 'project')}`
      : `References ${counted(row.referenceCount, 'shared asset')}`;
  }

  protected byIndex(index: number): number {
    return index;
  }

  private select(row: LibraryRow): void {
    this.selection.select(isAsset(row) ? { kind: 'asset', asset: row } : { kind: 'project', project: row });
  }

  private reveal(index: number): void {
    const viewport = this.viewport();
    if (!viewport) {
      return;
    }

    const top = index * RowHeight;
    const offset = viewport.measureScrollOffset();
    const height = viewport.getViewportSize();
    if (top < offset) {
      viewport.scrollToOffset(top);
    } else if (top + RowHeight > offset + height) {
      viewport.scrollToOffset(top + RowHeight - height);
    }
  }

  private toTop(): void {
    this.active.set(null);
    this.viewport()?.scrollToIndex(0);
  }
}
