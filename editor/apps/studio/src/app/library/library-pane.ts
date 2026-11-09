import { CdkVirtualScrollViewport, ScrollingModule } from '@angular/cdk/scrolling';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  type ElementRef,
  computed,
  effect,
  inject,
  linkedSignal,
  signal,
  untracked,
  viewChild
} from '@angular/core';
import type { AssetSummary, ProjectSummary } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiContextMenuImports } from '@xui/context-menu';
import { XuiInputImports } from '@xui/input';
import { XuiMenuImports } from '@xui/menu';
import { XuiMultiSelectImports } from '@xui/multi-select';
import { XuiNonIdealStateImports } from '@xui/non-ideal-state';
import { XuiSegmentedControlImports, type XuiSegmentedOption } from '@xui/segmented-control';
import { XuiSkeletonImports } from '@xui/skeleton';
import { XuiSpinnerImports } from '@xui/spinner';
import { Commands } from '../core/commands';
import { desktop } from '../core/desktop';
import { documentKindName } from '../core/labels';
import { MenuKeys } from '../core/menu-keys';
import { type LibraryTabChoice, type LibraryViewChoice, Preferences } from '../core/preferences';
import { SystemStatus } from '../core/system-status';
import { NewGraphDialog } from '../graph/new-graph-dialog';
import { BinDialog } from './bin-dialog';
import { GridRows } from './grid-rows';
import { LibraryActions, type LibraryTarget, assetTarget, projectTarget, thumbnailSource } from './library-actions';
import { LibraryDialogs } from './library-dialogs';
import { LibrarySelection } from './library-selection';
import { LibraryStore, type LibraryView } from './library-store';
import { LibraryTree } from './library-tree';
import { LibraryTreeStore } from './library-tree-store';
import { counted, nextIndex } from './list-keys';
import { NewProjectDialog } from './new-project-dialog';
import { ProjectStore } from './project-store';
import { LibraryThumbnails } from './thumbnails';
import { ViewportFit } from './viewport-fit';

type LibraryRow = ProjectSummary | AssetSummary;

const RowHeight = 40;
/** The narrowest a grid tile gets before the grid drops a column. */
const TileWidth = 112;
/** Room under a tile's picture for its name and what it is. */
const TileLabel = 36;

const views: readonly XuiSegmentedOption<LibraryTabChoice>[] = [
  { value: 'projects', label: 'Projects' },
  { value: 'assets', label: 'Materials' }
];


function isAsset(row: LibraryRow): row is AssetSummary {
  return 'kind' in row;
}

function targetOf(row: LibraryRow): LibraryTarget {
  return isAsset(row) ? assetTarget(row) : projectTarget(row);
}

/**
 * The Library browser: projects as a tree of folders, or projects and the materials in them as a list or a grid of
 * pictures, searched and filtered by the Server a page at a time. Double-click or Enter opens what is chosen, of any
 * kind, and a right-click or the menu key offers the rest.
 */
@Component({
  selector: 'ptn-library',
  imports: [
    BinDialog,
    LibraryDialogs,
    LibraryTree,
    MenuKeys,
    NewGraphDialog,
    NewProjectDialog,
    ScrollingModule,
    ViewportFit,
    XuiButtonImports,
    XuiContextMenuImports,
    XuiInputImports,
    XuiMenuImports,
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
        <div class="chrome-bar flex h-9 shrink-0 items-center gap-1.5 px-2">
          <xui-segmented-control
            class="min-w-0 flex-1"
            size="sm"
            fill
            aria-label="Library view"
            [options]="views"
            [value]="viewName()"
            (valueChange)="switchTo($event)"
          />
          <button
            type="button"
            xuiButton
            size="sm"
            variant="ghost"
            class="shrink-0 px-1.5"
            data-testid="library-layout"
            [attr.data-layout]="layout()"
            [attr.aria-label]="layout() === 'grid' ? 'Show as a list' : 'Show as a grid of pictures'"
            [attr.aria-pressed]="layout() === 'grid'"
            [title]="layout() === 'grid' ? 'Show as a list' : 'Show as a grid of pictures'"
            (click)="lay(layout() === 'grid' ? 'list' : 'grid')"
          >
            <svg viewBox="0 0 16 16" class="size-4" aria-hidden="true">
              @if (layout() === 'grid') {
                <path d="M2 3.5h12M2 8h12M2 12.5h12" stroke="currentColor" stroke-width="1.5" fill="none" />
              } @else {
                <path d="M2 2h5v5H2zM9 2h5v5H9zM2 9h5v5H2zM9 9h5v5H9z" fill="currentColor" />
              }
            </svg>
          </button>
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
              <xui-non-ideal-state class="absolute inset-0 m-auto h-fit" [title]="message.title" [description]="message.description">
                @if (message.make) {
                  <button action type="button" xuiButton size="sm" (click)="newProjectIn(treeStore.target())">
                    New Project…
                  </button>
                }
              </xui-non-ideal-state>
            }
          </div>
        } @else {
          <div #area class="well relative min-h-0 flex-1 overflow-hidden">
            <cdk-virtual-scroll-viewport
              ptnViewportFit
              class="size-full outline-none"
              role="listbox"
              tabindex="0"
              data-testid="library-list"
              minBufferPx="400"
              maxBufferPx="800"
              [attr.data-layout]="layout()"
              [attr.aria-label]="viewName() === 'projects' ? 'Projects' : 'Materials'"
              [attr.aria-activedescendant]="active() === null ? null : 'library-row-' + active()"
              [itemSize]="rowSize()"
              [xuiContextMenuTriggerFor]="itemMenu"
              ptnMenuKeys="[aria-selected=true]"
              (aiming)="aim($event)"
              (contextmenu)="aim($event)"
              (click)="click($event)"
              (dblclick)="openAt($event)"
              (keydown)="press($event)"
            >
              <div
                *cdkVirtualFor="let line of rows(); trackBy: byRow"
                class="flex"
                [class.gap-1]="layout() === 'grid'"
                [class.px-1]="layout() === 'grid'"
                [style.height.px]="rowSize()"
              >
                @for (row of line.items; track $index) {
                  @let index = line.first + $index;
                  @let picture = row ? pictures.url(source(row)) : undefined;
                  <div
                    role="option"
                    data-testid="library-row"
                    [id]="'library-row-' + index"
                    [attr.data-index]="index"
                    [attr.data-layout]="layout()"
                    [attr.aria-selected]="!!row && row.id === selectedId()"
                    [attr.aria-posinset]="index + 1"
                    [attr.aria-setsize]="view().list.total()"
                    [class]="layout() === 'grid' ? tileClass : rowClass"
                    [style.width.px]="layout() === 'grid' ? tileSize() : null"
                    [class.bg-selection]="!!row && row.id === selectedId()"
                    [class.outline]="index === active()"
                    [class.-outline-offset-1]="index === active()"
                    [class.outline-focus]="index === active()"
                    [title]="row ? row.name + ' — ' + detail(row) : ''"
                  >
                    @if (row) {
                      <span
                        class="well grid shrink-0 place-items-center overflow-hidden rounded-sm"
                        [class]="layout() === 'grid' ? 'aspect-square w-full' : 'size-7'"
                      >
                        @if (picture) {
                          <img
                            class="block size-full object-cover"
                            alt=""
                            decoding="async"
                            draggable="false"
                            data-testid="library-picture"
                            [src]="picture"
                          />
                        } @else {
                          <span class="text-foreground-muted text-xs" aria-hidden="true">{{ mark(row) }}</span>
                        }
                      </span>
                      <span class="min-w-0 flex-1 leading-tight" [class.w-full]="layout() === 'grid'">
                        <span class="block truncate">{{ row.name }}</span>
                        <span class="text-foreground-muted block truncate text-xs">{{ detail(row) }}</span>
                      </span>
                      @if (layout() === 'list') {
                        <span class="readout text-foreground-muted shrink-0 text-xs" [title]="countLabel(row)">
                          {{ count(row) }}
                        </span>
                      }
                    } @else {
                      <span class="flex min-w-0 flex-1 flex-col gap-1.5">
                        @if (layout() === 'grid') {
                          <xui-skeleton class="aspect-square w-full" />
                        }
                        <xui-skeleton class="h-2.5 w-2/3" />
                        <xui-skeleton class="h-2 w-1/2" />
                      </span>
                    }
                  </div>
                }
              </div>
            </cdk-virtual-scroll-viewport>

            @if (overlay(); as message) {
              <xui-non-ideal-state class="absolute inset-0 m-auto h-fit" [title]="message.title" [description]="message.description">
                @switch (message.action) {
                  @case ('retry') {
                    <button action type="button" xuiButton size="sm" (click)="view().list.retry()">Try again</button>
                  }
                  @case ('clear') {
                    <button action type="button" xuiButton size="sm" (click)="clearFilters()">Show Everything</button>
                  }
                  @case ('project') {
                    <button action type="button" xuiButton size="sm" (click)="newProjectIn(treeStore.target())">
                      New Project…
                    </button>
                  }
                  @case ('graph') {
                    <button action type="button" xuiButton size="sm" (click)="creatingGraph.set(true)">New Graph…</button>
                  }
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
            @if (actions.failure(); as reason) {
              <p
                class="bg-error-subtle text-error absolute inset-x-0 bottom-0 flex items-center gap-2 px-2 py-1 text-xs"
                data-testid="library-failure"
              >
                <span class="min-w-0 flex-1 truncate" [title]="reason">{{ reason }}</span>
                <button type="button" xuiButton size="sm" variant="ghost" (click)="actions.failure.set(null)">
                  Dismiss
                </button>
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
          <button
            type="button"
            xuiButton
            size="sm"
            variant="ghost"
            class="shrink-0 px-1.5"
            aria-label="Bin"
            data-testid="library-bin"
            [title]="commands.tooltip('showBin')"
            (click)="commands.run('showBin')"
          >
            <svg viewBox="0 0 16 16" class="size-4" aria-hidden="true">
              <path
                d="M2.5 4h11M6 4V2.5h4V4M4 4l.7 9.5h6.6L12 4M6.5 6.5v5M9.5 6.5v5"
                stroke="currentColor"
                stroke-width="1.2"
                fill="none"
              />
            </svg>
          </button>
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
        @defer (when actions.asking()) {
          <ptn-library-dialogs />
        }
        @defer (when actions.binOpen()) {
          <ptn-bin />
        }
      }
    }

    <ng-template #itemMenu>
      <xui-menu data-testid="library-menu" aria-label="Library">
        @if (menuTarget(); as target) {
          <button type="button" xuiMenuItem data-testid="item-open" (triggered)="actions.open(target)">Open</button>
          @if (target.kind === 'project') {
            <button type="button" xuiMenuItem (triggered)="project.openSettings(target.path)">Project Settings…</button>
            <button type="button" xuiMenuItem data-testid="item-new-document" (triggered)="actions.adding.set(target.path)">
              New Document…
            </button>
          }
          <xui-menu-divider />
          <button type="button" xuiMenuItem data-testid="item-rename" (triggered)="actions.renaming.set(target)">Rename…</button>
          <button type="button" xuiMenuItem data-testid="item-tags" (triggered)="actions.tagging.set(target)">Tags…</button>
          @if (target.kind === 'project') {
            <button type="button" xuiMenuItem data-testid="item-duplicate" (triggered)="duplicate(target)">Duplicate</button>
          }
          @if (canPick) {
            <button type="button" xuiMenuItem data-testid="item-reveal" (triggered)="reveal(target)">
              Show in File Browser
            </button>
          }
          <xui-menu-divider />
          <button type="button" xuiMenuItem color="error" data-testid="item-delete" (triggered)="actions.deleting.set(target)">
            Delete…
          </button>
        }
      </xui-menu>
    </ng-template>
  `
})
export class LibraryPane {
  protected readonly store = inject(LibraryStore);
  protected readonly commands = inject(Commands);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly treeStore = inject(LibraryTreeStore);
  protected readonly project = inject(ProjectStore);
  protected readonly actions = inject(LibraryActions);
  protected readonly pictures = inject(LibraryThumbnails);
  private readonly preferences = inject(Preferences);
  private readonly status = inject(SystemStatus);
  private readonly selection = inject(LibrarySelection);
  private readonly tree = viewChild<LibraryTree>('tree');
  private readonly viewport = viewChild(CdkVirtualScrollViewport);
  private readonly area = viewChild<ElementRef<HTMLElement>>('area');

  protected readonly views = views;
  protected readonly rowClass = 'border-hairline flex items-center gap-2 border-b px-2';
  protected readonly tileClass = 'flex min-w-0 shrink-0 cursor-default flex-col gap-1 rounded-sm p-1 text-xs';
  /** Which tab is in front: the one the person's preferences open on, until they choose another. */
  protected readonly viewName = linkedSignal<LibraryTabChoice>(() => this.preferences.libraryTab());
  /** List or grid: the last one chosen, which the preferences keep. */
  protected readonly layout = this.preferences.libraryView;
  protected readonly creating = signal(false);
  protected readonly creatingIn = signal('');
  protected readonly creatingGraph = signal(false);
  protected readonly creatingKind = signal('texturing');
  protected readonly canPick = desktop() !== undefined;
  protected readonly noLibraryText = this.canPick
    ? "A library holds one game's projects, and anything in one can be used in another."
    : 'No library is open.';
  protected readonly recent = signal<readonly string[]>([]);
  /** The tree is the Library's home in the list layout; a search, a tag or the grid looks across every folder. */
  protected readonly showsTree = computed(() => {
    const view = this.view();
    return (
      this.viewName() === 'projects' && this.layout() === 'list' && view.search() === '' && view.tags().length === 0
    );
  });
  /** The keyboard's item, which can be one whose page has not arrived yet. */
  protected readonly active = signal<number | null>(null);
  protected readonly selectedId = computed(() => this.selection.id());
  /** What the item menu is about. */
  protected readonly menuTarget = signal<LibraryTarget | null>(null);

  /** How wide the list area is, which decides the grid's columns. */
  private readonly width = signal(0);
  protected readonly columns = computed(() =>
    this.layout() === 'grid' ? Math.max(1, Math.floor((this.width() - 8) / TileWidth)) : 1
  );
  protected readonly tileSize = computed(() => Math.floor((this.width() - 8) / this.columns()) - 4);
  protected readonly rowSize = computed(() => (this.layout() === 'grid' ? this.tileSize() + TileLabel + 4 : RowHeight));

  protected readonly view = computed<LibraryView<LibraryRow>>(() =>
    this.viewName() === 'projects'
      ? (this.store.projects as LibraryView<LibraryRow>)
      : (this.store.assets as LibraryView<LibraryRow>)
  );

  /** The list as rows of a column or a few, each showing asking for its pictures. */
  protected readonly rows = computed(
    () =>
      new GridRows<LibraryRow>(this.view().list, this.columns(), shown =>
        this.pictures.want(shown.map(row => this.source(row)))
      )
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
      return { title: 'The list did not load', description: failure, make: false };
    }

    if (!this.treeStore.loaded() || this.treeStore.rows().length > 0) {
      return null;
    }

    const state = this.store.state();
    return state.kind === 'ready' && state.info.indexing
      ? { title: 'Indexing the library', description: 'This list fills in when the scan ends.', make: false }
      : {
          title: 'No projects yet',
          description: 'A project is one object or asset set: a mesh to paint, or a material to make.',
          make: true
        };
  });

  /** What the list area says instead of rows, and the one action that fills it. */
  protected readonly overlay = computed(() => {
    const view = this.view();
    const { error, total } = view.list;
    if (total() === null && error() !== null) {
      return { title: 'The list did not load', description: error(), action: 'retry' };
    }

    if (total() !== 0) {
      return null;
    }

    if (view.filtered) {
      return { title: 'Nothing matches', description: 'No name, tag or path has every word and tag asked for.', action: 'clear' };
    }

    const projects = this.viewName() === 'projects';
    const state = this.store.state();
    if (state.kind === 'ready' && state.info.indexing) {
      return { title: 'Indexing the library', description: 'This list fills in when the scan ends.', action: null };
    }

    return projects
      ? {
          title: 'No projects yet',
          description: 'A project is one object or asset set: a mesh to paint, or a material to make.',
          action: 'project'
        }
      : {
          title: 'No materials yet',
          description: 'Material graphs and layered materials in any project appear here, for any project to use.',
          action: 'graph'
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
    this.commands.handle('showBin', { enabled: ready, run: () => this.actions.binOpen.set(true) }, this.destroyRef);

    // The list stops at its last item, so an index past it after a smaller result set has no item to land on.
    effect(() => {
      const total = this.view().list.total();
      this.active.update(index => (index !== null && total !== null && index >= total ? null : index));
    });

    // A dock pane changes size without a window resize, and the grid's columns follow its width.
    effect(onCleanup => {
      const area = this.area()?.nativeElement;
      if (!area) {
        return;
      }

      const observer = new ResizeObserver(() => this.width.set(area.clientWidth));
      observer.observe(area);
      this.width.set(area.clientWidth);
      onCleanup(() => observer.disconnect());
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

  protected switchTo(name: LibraryTabChoice | null): void {
    if (name !== null && name !== this.viewName()) {
      this.viewName.set(name);
      this.toTop();
    }
  }

  protected lay(layout: LibraryViewChoice | null): void {
    if (layout !== null && layout !== this.layout()) {
      this.preferences.set({ libraryView: layout });
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

  protected clearFilters(): void {
    this.view().setSearch('');
    this.view().setTags([]);
    this.toTop();
  }

  /** One listener for every item, rather than one per item recycled by the viewport. */
  protected click(event: MouseEvent): void {
    const index = this.indexAt(event);
    if (index === null) {
      return;
    }

    this.active.set(index);
    const row = this.view().list.at(index);
    if (row) {
      this.select(row);
    }
  }

  protected openAt(event: MouseEvent): void {
    const index = this.indexAt(event);
    const row = index === null ? undefined : this.view().list.at(index);
    if (row) {
      this.actions.open(targetOf(row));
    }
  }

  /** Says what the menu is about before it draws: the item under the pointer, or the one the keyboard is on. */
  protected aim(event: { target: EventTarget | null }): void {
    const index = this.indexAt(event);
    const row = index === null ? undefined : this.view().list.at(index);
    if (row && index !== null) {
      this.active.set(index);
      this.select(row);
    }

    this.menuTarget.set(row ? targetOf(row) : null);
  }

  protected press(event: KeyboardEvent): void {
    const list = this.view().list;
    const index = this.active();
    const row = index === null ? undefined : list.at(index);
    if (event.key === 'Enter' || event.key === ' ') {
      if (row) {
        this.select(row);
        if (event.key === 'Enter') {
          this.actions.open(targetOf(row));
        }
      }

      event.preventDefault();
      return;
    }

    if (event.key === 'F2' && row) {
      event.preventDefault();
      this.actions.renaming.set(targetOf(row));
      return;
    }

    if ((event.key === 'Delete' || event.key === 'Backspace') && row) {
      event.preventDefault();
      this.actions.deleting.set(targetOf(row));
      return;
    }

    const next = this.step(event.key, index, list.total() ?? 0);
    if (next === null) {
      return;
    }

    event.preventDefault();
    this.active.set(next);
    this.scrollTo(next);
    const chosen = list.at(next);
    if (chosen) {
      this.select(chosen);
    }
  }

  protected detail(row: LibraryRow): string {
    return isAsset(row) ? documentKindName(row.kind) : row.path;
  }

  /** What stands in for a picture that is not there: the first letter of what it is. */
  protected mark(row: LibraryRow): string {
    return isAsset(row) ? documentKindName(row.kind).slice(0, 1) : row.name.slice(0, 1).toUpperCase();
  }

  protected source(row: LibraryRow): string {
    return thumbnailSource(targetOf(row));
  }

  protected count(row: LibraryRow): number {
    return isAsset(row) ? row.usageCount : row.referenceCount;
  }

  protected countLabel(row: LibraryRow): string {
    return isAsset(row)
      ? `Used by ${counted(row.usageCount, 'project')}`
      : `References ${counted(row.referenceCount, 'material')}`;
  }

  protected byRow(_index: number, line: { row: number }): number {
    return line.row;
  }

  protected duplicate(target: LibraryTarget): void {
    if (target.kind === 'project') {
      void this.treeStore.duplicateProject(target.path, `${target.name} Copy`);
    }
  }

  protected reveal(target: LibraryTarget): void {
    const state = this.status.state();
    if (state.kind === 'online' && state.system.library) {
      const below = target.kind === 'project' ? `projects/${target.path}` : target.path;
      void desktop()?.reveal(`${state.system.library}/${below}`);
    }
  }

  private indexAt(event: { target: EventTarget | null }): number | null {
    const index = (event.target as HTMLElement | null)?.closest('[data-index]')?.getAttribute('data-index');
    return index === undefined || index === null ? null : Number(index);
  }

  /** The item a key moves to: across a row, or a row up or down, which in the list is the same thing. */
  private step(key: string, index: number | null, total: number): number | null {
    const columns = this.columns();
    if (total === 0) {
      return null;
    }

    if (columns > 1) {
      const current = index ?? -1;
      switch (key) {
        case 'ArrowRight':
          return Math.min(current + 1, total - 1);
        case 'ArrowLeft':
          return Math.max(current - 1, 0);
        case 'ArrowDown':
          return Math.min(current + columns, total - 1);
        case 'ArrowUp':
          return Math.max(index === null ? 0 : current - columns, 0);
        default:
          break;
      }
    }

    const rowsPerPage = Math.max(1, Math.floor((this.viewport()?.getViewportSize() ?? 0) / this.rowSize()) - 1);
    return nextIndex(key, index, total, rowsPerPage * columns);
  }

  private select(row: LibraryRow): void {
    this.selection.select(isAsset(row) ? { kind: 'asset', asset: row } : { kind: 'project', project: row });
  }

  private scrollTo(index: number): void {
    const viewport = this.viewport();
    if (!viewport) {
      return;
    }

    const size = this.rowSize();
    const top = Math.floor(index / this.columns()) * size;
    const offset = viewport.measureScrollOffset();
    const height = viewport.getViewportSize();
    if (top < offset) {
      viewport.scrollToOffset(top);
    } else if (top + size > offset + height) {
      viewport.scrollToOffset(top + size - height);
    }
  }

  private toTop(): void {
    this.active.set(null);
    this.viewport()?.scrollToIndex(0);
  }
}
