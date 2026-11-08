import { CdkVirtualScrollViewport, ScrollingModule } from '@angular/cdk/scrolling';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  signal,
  untracked,
  viewChild
} from '@angular/core';
import type { AssetSummary, ProjectSummary } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiInputImports } from '@xui/input';
import { XuiMultiSelectImports } from '@xui/multi-select';
import { XuiNonIdealStateImports } from '@xui/non-ideal-state';
import { XuiSegmentedControlImports, type XuiSegmentedOption } from '@xui/segmented-control';
import { XuiSkeletonImports } from '@xui/skeleton';
import { XuiSpinnerImports } from '@xui/spinner';
import { Commands } from '../core/commands';
import { desktop } from '../core/desktop';
import { NewGraphDialog } from '../graph/new-graph-dialog';
import { LibrarySelection } from './library-selection';
import { LibraryStore, type LibraryView } from './library-store';
import { LibraryTree } from './library-tree';
import { LibraryTreeStore } from './library-tree-store';
import { counted, nextIndex } from './list-keys';
import { documentKindName } from '../core/labels';
import { NewProjectDialog } from './new-project-dialog';
import { ProjectStore } from './project-store';
import { ViewportFit } from './viewport-fit';

type ViewName = 'projects' | 'assets';
type LibraryRow = ProjectSummary | AssetSummary;

const RowHeight = 40;

const views: readonly XuiSegmentedOption<ViewName>[] = [
  { value: 'projects', label: 'Projects' },
  { value: 'assets', label: 'Materials' }
];

function isAsset(row: LibraryRow): row is AssetSummary {
  return 'kind' in row;
}

/** The Library browser: projects or the materials in them, searched and filtered by the Server, a page at a time. */
@Component({
  selector: 'ptn-library',
  imports: [
    LibraryTree,
    NewGraphDialog,
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
  host: { class: 'flex h-full min-h-0 flex-col', 'data-commands': 'library' },
  template: `
    @let state = store.state();
    @switch (state.kind) {
      @case ('loading') {
        <div class="grid flex-1 place-items-center">
          <xui-spinner size="md" aria-label="Opening the library" />
        </div>
      }
      @case ('none') {
        <xui-non-ideal-state class="flex-1" title="No library open" [description]="noLibraryText">
          <div action class="flex gap-2">
            @if (canPick) {
              <button
                type="button"
                xuiButton
                size="sm"
                color="primary"
                data-testid="library-new"
                (click)="newLibrary()"
              >
                New Library…
              </button>
              <button type="button" xuiButton size="sm" data-testid="library-open" (click)="openLibrary()">
                Open Library…
              </button>
            }
          </div>
        </xui-non-ideal-state>
        @for (path of recent(); track path) {
          <button
            type="button"
            class="hover:bg-selection w-full truncate px-3 py-1 text-start text-sm"
            data-testid="library-recent"
            [title]="path"
            (click)="openRecent(path)"
          >
            {{ nameOf(path) }}
          </button>
        }
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

        @if (showsTree()) {
          <div class="well relative flex min-h-0 flex-1 flex-col overflow-hidden">
            <ptn-library-tree #tree (creatingProject)="newProjectIn($event)" />
            @if (treeOverlay(); as message) {
              <xui-non-ideal-state
                class="pointer-events-none absolute inset-0 m-auto h-fit"
                [title]="message.title"
                [description]="message.description"
              />
            }
          </div>
        } @else {
          <div class="well relative min-h-0 flex-1 overflow-hidden">
            <cdk-virtual-scroll-viewport
              ptnViewportFit
              class="size-full outline-none"
              role="listbox"
              tabindex="0"
              data-testid="library-list"
              minBufferPx="400"
              maxBufferPx="800"
              [attr.aria-label]="viewName() === 'projects' ? 'Projects' : 'Materials'"
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
              <p
                class="bg-error-subtle text-error absolute inset-x-0 bottom-0 flex items-center gap-2 px-2 py-1 text-xs"
              >
                <span class="min-w-0 flex-1 truncate" [title]="reason">{{ reason }}</span>
                <button type="button" xuiButton size="sm" (click)="view().list.retry()">Try again</button>
              </p>
            }
          </div>
        }

        <footer class="chrome-bar flex h-8 shrink-0 items-center gap-2 px-2">
          <span class="readout min-w-0 flex-1 truncate" data-testid="library-count">{{ countText() }}</span>
          @if (state.info.indexing) {
            <span class="text-foreground-muted flex items-center gap-1 text-xs" data-testid="library-indexing">
              <xui-spinner size="xs" aria-hidden="true" />
              Indexing
            </span>
          }
          @if (viewName() === 'assets') {
            <button type="button" xuiButton size="sm" data-testid="library-new-graph" (click)="creatingGraph.set(true)">
              New Graph…
            </button>
          }
          @if (viewName() === 'projects') {
            <button
              type="button"
              xuiButton
              size="sm"
              variant="ghost"
              data-testid="library-new-folder"
              (click)="newFolder()"
            >
              Folder
            </button>
            <button
              type="button"
              xuiButton
              size="sm"
              data-testid="library-new-project"
              (click)="newProjectIn(treeStore.target())"
            >
              Project…
            </button>
          }
        </footer>
        <ptn-new-project [(open)]="creating" [folder]="creatingIn()" [startKind]="creatingKind()" />
        <ptn-new-graph [(open)]="creatingGraph" />
      }
    }
  `
})
export class LibraryPane {
  protected readonly store = inject(LibraryStore);
  private readonly commands = inject(Commands);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly treeStore = inject(LibraryTreeStore);
  private readonly project = inject(ProjectStore);
  private readonly selection = inject(LibrarySelection);
  private readonly tree = viewChild<LibraryTree>('tree');
  private readonly viewport = viewChild(CdkVirtualScrollViewport);

  protected readonly views = views;
  protected readonly rowHeight = RowHeight;
  protected readonly viewName = signal<ViewName>('projects');
  protected readonly creating = signal(false);
  protected readonly creatingIn = signal('');
  protected readonly creatingGraph = signal(false);
  protected readonly creatingKind = signal('texturing');
  protected readonly canPick = desktop() !== undefined;
  protected readonly noLibraryText = this.canPick
    ? "A library holds one game's projects, and anything in one can be used in another."
    : 'No library is open.';
  protected readonly recent = signal<readonly string[]>([]);
  /** The tree is the Library's home; a search or a tag looks across every folder instead. */
  protected readonly showsTree = computed(() => {
    const view = this.view();
    return this.viewName() === 'projects' && view.search() === '' && view.tags().length === 0;
  });
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
    const state = this.store.state();
    const total = this.showsTree() && state.kind === 'ready' ? state.info.projectCount : this.view().list.total();
    if (total === null) {
      return '…';
    }

    return counted(total, this.viewName() === 'projects' ? 'project' : 'material');
  });

  /** What the tree says instead of rows. */
  protected readonly treeOverlay = computed(() => {
    const failure = this.treeStore.failure();
    if (!this.treeStore.loaded() && failure !== null) {
      return { title: 'The list did not load', description: failure };
    }

    if (!this.treeStore.loaded() || this.treeStore.rows().length > 0) {
      return null;
    }

    const state = this.store.state();
    return state.kind === 'ready' && state.info.indexing
      ? { title: 'Indexing the library', description: 'This list fills in when the scan ends.' }
      : { title: 'No projects yet', description: 'Make a project from a mesh, or a folder to group them in.' };
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
      : {
          title: 'No materials yet',
          description: 'Material graphs and Layered Materials in any project appear here.',
          retry: false
        };
  });

  constructor() {
    void desktop()
      ?.recentLibraries()
      .then(paths => this.recent.set(paths));
    effect(() => {
      const asked = this.project.newKind();
      if (asked) {
        this.creatingKind.set(asked.kind);
        untracked(() => this.newProjectIn(this.treeStore.target()));
      }
    });
    const ready = () => this.store.state().kind === 'ready';
    this.commands.handle('newProject', { enabled: ready, run: () => this.newProjectIn(this.treeStore.target()) }, this.destroyRef);
    this.commands.handle('newFolder', { enabled: ready, run: () => this.newFolder() }, this.destroyRef);

    // The list stops at its last row, so an index past it after a smaller result set has no row to land on.
    effect(() => {
      const total = this.view().list.total();
      this.active.update(index => (index !== null && total !== null && index >= total ? null : index));
    });
  }

  protected newProjectIn(folder: string): void {
    this.creatingIn.set(folder);
    this.creating.set(true);
  }

  protected async newFolder(): Promise<void> {
    await this.tree()?.makeFolder(this.treeStore.target());
  }

  protected openLibrary(): void {
    void desktop()?.openLibrary();
  }

  protected newLibrary(): void {
    void desktop()?.newLibrary();
  }

  protected openRecent(path: string): void {
    void desktop()?.openRecentLibrary(path);
  }

  protected nameOf(path: string): string {
    return (
      path
        .split(/[\\/]/)
        .pop()
        ?.replace(/\.ptlib$/, '') ?? path
    );
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
    return isAsset(row) ? documentKindName(row.kind) : row.path;
  }

  protected count(row: LibraryRow): number {
    return isAsset(row) ? row.usageCount : row.referenceCount;
  }

  protected countLabel(row: LibraryRow): string {
    return isAsset(row)
      ? `Used by ${counted(row.usageCount, 'project')}`
      : `References ${counted(row.referenceCount, 'material')}`;
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
