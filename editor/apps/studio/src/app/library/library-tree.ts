import { CdkVirtualScrollViewport, ScrollingModule } from '@angular/cdk/scrolling';
import { ChangeDetectionStrategy, Component, type ElementRef, computed, inject, output, signal, viewChild } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiDialogImports } from '@xui/dialog';
import { XuiInputImports } from '@xui/input';
import { desktop } from '../core/desktop';
import { SystemStatus } from '../core/system-status';
import { LibrarySelection } from './library-selection';
import { FolderColours, LibraryTreeStore, type TreeRow, canDrop, parentOf } from './library-tree-store';
import { ViewportFit } from './viewport-fit';
import { ProjectStore } from './project-store';

const RowHeight = 28;
const Indent = 14;
const DragType = 'application/x-patina-library';

interface MenuAt {
  x: number;
  y: number;
  row: TreeRow | null;
}

/** The projects as folders: drag to regroup, right-click or the keyboard for the rest. */
@Component({
  selector: 'ptn-library-tree',
  imports: [ScrollingModule, ViewportFit, XuiButtonImports, XuiCalloutImports, XuiDialogImports, XuiInputImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'relative flex min-h-0 flex-1 flex-col', '(document:click)': 'menu.set(null)' },
  template: `
    @if (tree.failure(); as failure) {
      <xui-callout color="error" data-testid="tree-failure" compact>
        <span class="flex items-center gap-2">
          <span class="min-w-0 flex-1">{{ failure }}</span>
          <button type="button" xuiButton size="sm" variant="ghost" (click)="tree.clearFailure()">Dismiss</button>
        </span>
      </xui-callout>
    }

    <cdk-virtual-scroll-viewport
      ptnViewportFit
      class="min-h-0 flex-1 outline-none"
      role="tree"
      tabindex="0"
      aria-label="Projects"
      data-testid="library-tree"
      minBufferPx="400"
      maxBufferPx="800"
      [class.bg-selection]="dropInto() === ''"
      [itemSize]="rowHeight"
      (keydown)="press($event)"
      (click)="click($event)"
      (dblclick)="activateAt($event)"
      (contextmenu)="openMenu($event)"
      (dragstart)="start($event)"
      (dragover)="over($event)"
      (dragleave)="dropInto.set(null)"
      (drop)="drop($event)"
    >
        <div
          *cdkVirtualFor="let row of tree.rows(); trackBy: byPath"
          class="flex cursor-default items-center gap-1 pe-2 select-none"
          role="treeitem"
          data-testid="tree-row"
          [attr.data-path]="row.path"
          [attr.data-kind]="row.kind"
          [attr.aria-level]="row.depth + 1"
          [attr.aria-expanded]="row.kind === 'folder' ? row.expanded : null"
          [attr.aria-selected]="tree.selected().has(row.path)"
          [attr.draggable]="row.kind !== 'more' && renaming() !== row.path"
          [class.bg-selection]="tree.selected().has(row.path) || (dropInto() === row.path && row.kind === 'folder')"
          [style.height.px]="rowHeight"
          [style.padding-inline-start.px]="row.depth * indent + 4"
        >
          @switch (row.kind) {
            @case ('more') {
              <span class="text-foreground-muted text-xs" data-testid="tree-more">{{ row.remaining }} more…</span>
            }
            @case ('folder') {
              <button
                type="button"
                class="text-foreground-muted grid size-4 shrink-0 place-items-center"
                tabindex="-1"
                data-testid="tree-toggle"
                [attr.aria-label]="row.expanded ? 'Close ' + row.folder.name : 'Open ' + row.folder.name"
                (click)="toggle($event, row.path)"
              >
                <svg viewBox="0 0 16 16" class="size-3" [class.rotate-90]="row.expanded" aria-hidden="true">
                  <path d="M6 3l5 5-5 5z" fill="currentColor" />
                </svg>
              </button>
              <svg viewBox="0 0 16 16" class="size-4 shrink-0" aria-hidden="true" data-testid="tree-folder-icon">
                <path
                  d="M1.5 3.5h5l1.5 2h6.5v7.5h-13z"
                  [attr.fill]="row.folder.color ?? 'currentColor'"
                  [attr.fill-opacity]="row.folder.color ? 1 : 0.45"
                />
              </svg>
            }
            @case ('project') {
              <span class="size-4 shrink-0"></span>
              <svg viewBox="0 0 16 16" class="text-foreground-muted size-4 shrink-0" aria-hidden="true">
                <path d="M8 1.5l5.5 3v7L8 14.5l-5.5-3v-7zM8 8l5.5-3.5M8 8v6.5M8 8L2.5 4.5" fill="none" stroke="currentColor" />
              </svg>
            }
          }
          @if (row.kind !== 'more') {
            @if (renaming() === row.path) {
              <input
                #renameInput
                class="min-w-0 flex-1"
                xuiInput
                size="sm"
                autocomplete="off"
                aria-label="Name"
                data-testid="tree-rename"
                [value]="labelOf(row)"
                (click)="$event.stopPropagation()"
                (keydown)="renameKey($event)"
                (blur)="finishRename($event, row)"
              />
            } @else {
              <span class="min-w-0 flex-1 truncate" [class.font-semibold]="isOpen(row)" [title]="row.path">
                {{ labelOf(row) }}
              </span>
              @if (row.kind === 'folder') {
                <span class="readout text-foreground-muted shrink-0 text-xs" title="Projects in this folder">
                  {{ row.folder.projectCount }}
                </span>
              }
            }
          }
        </div>
    </cdk-virtual-scroll-viewport>

    @if (menu(); as at) {
      <div
        class="bg-surface-raised border-hairline fixed z-50 flex min-w-44 flex-col rounded border py-1 text-sm shadow-lg"
        role="menu"
        tabindex="-1"
        data-testid="tree-menu"
        [style.left.px]="at.x"
        [style.top.px]="at.y"
        (click)="$event.stopPropagation()"
        (keydown.escape)="menu.set(null)"
      >
        @if (at.row?.kind === 'project') {
          <button type="button" class="hover:bg-selection px-3 py-1 text-start" role="menuitem" data-testid="menu-open" (click)="act(openRow)">
            Open
          </button>
        }
        <button type="button" class="hover:bg-selection px-3 py-1 text-start" role="menuitem" data-testid="menu-new-project" (click)="act(newProject)">
          New Project…
        </button>
        <button type="button" class="hover:bg-selection px-3 py-1 text-start" role="menuitem" data-testid="menu-new-folder" (click)="act(newFolder)">
          New Folder
        </button>
        @if (at.row && at.row.kind !== 'more') {
          <div class="border-hairline my-1 border-t"></div>
          <button type="button" class="hover:bg-selection px-3 py-1 text-start" role="menuitem" data-testid="menu-rename" (click)="act(rename)">
            Rename
          </button>
          @if (canReveal()) {
            <button type="button" class="hover:bg-selection px-3 py-1 text-start" role="menuitem" data-testid="menu-reveal" (click)="act(reveal)">
              Show in File Browser
            </button>
          }
        }
        @if (at.row?.kind === 'folder') {
          <div class="border-hairline my-1 border-t"></div>
          <div class="flex items-center gap-1 px-3 py-1" role="group" aria-label="Folder colour">
            <button
              type="button"
              class="border-hairline size-4 rounded-full border"
              aria-label="No colour"
              title="No colour"
              data-testid="menu-colour-none"
              (click)="colour(null)"
            ></button>
            @for (colour of colours; track colour) {
              <button
                type="button"
                class="size-4 rounded-full"
                [attr.aria-label]="'Colour ' + colour"
                [attr.data-testid]="'menu-colour-' + colour"
                [style.background]="colour"
                (click)="this.colour(colour)"
              ></button>
            }
          </div>
          <div class="border-hairline my-1 border-t"></div>
          <button type="button" class="hover:bg-selection px-3 py-1 text-start" role="menuitem" data-testid="menu-delete" (click)="act(askDelete)">
            Delete Folder…
          </button>
        }
      </div>
    }

    <xui-dialog size="sm" title="Delete folder" [open]="deleting() !== null" (openChange)="$event || deleting.set(null)">
      <xui-dialog-body data-testid="tree-delete">
        @if (deleting()?.folder; as folder) {
          @if (folder.projectCount > 0) {
            {{ folder.name }} holds {{ folder.projectCount }} project(s). They move up a level, and nothing is deleted
            but the folder.
          } @else {
            Delete {{ folder.name }} and any empty folders in it?
          }
        }
      </xui-dialog-body>
      <xui-dialog-footer>
        <button type="button" xuiButton variant="ghost" (click)="deleting.set(null)">Cancel</button>
        <button type="button" xuiButton color="primary" data-testid="tree-delete-confirm" (click)="confirmDelete()">
          Delete Folder
        </button>
      </xui-dialog-footer>
    </xui-dialog>
  `
})
export class LibraryTree {
  protected readonly tree = inject(LibraryTreeStore);
  private readonly project = inject(ProjectStore);
  private readonly selection = inject(LibrarySelection);
  private readonly status = inject(SystemStatus);
  private readonly viewport = viewChild(CdkVirtualScrollViewport);
  private readonly renameInput = viewChild<ElementRef<HTMLInputElement>>('renameInput');

  readonly creatingProject = output<string>();

  protected readonly rowHeight = RowHeight;
  protected readonly indent = Indent;
  protected readonly colours = FolderColours;
  protected readonly menu = signal<MenuAt | null>(null);
  protected readonly renaming = signal<string | null>(null);
  protected readonly dropInto = signal<string | null>(null);
  protected readonly deleting = signal<Extract<TreeRow, { kind: 'folder' }> | null>(null);
  protected readonly canReveal = computed(() => desktop() !== undefined);
  private dragged: readonly string[] = [];

  protected byPath(_index: number, row: TreeRow): string {
    return `${row.kind}:${row.path}`;
  }

  protected labelOf(row: TreeRow): string {
    return row.kind === 'folder' ? row.folder.name : row.kind === 'project' ? row.project.name : '';
  }

  protected isOpen(row: TreeRow): boolean {
    return row.kind === 'project' && this.project.current()?.path === row.path;
  }

  protected toggle(event: Event, path: string): void {
    event.stopPropagation();
    void this.tree.toggle(path);
  }

  protected click(event: MouseEvent): void {
    const row = this.rowAt(event);
    if (row === null) {
      this.tree.clearChoice();
      this.selection.clear();
      return;
    }

    if (row.kind === 'more') {
      void this.tree.more(row.path);
      return;
    }

    this.tree.choose(row.path, event.shiftKey ? 'range' : event.metaKey || event.ctrlKey ? 'toggle' : 'only');
    this.show(row);
  }

  protected activateAt(event: MouseEvent): void {
    const row = this.rowAt(event);
    if (row && !(event.target as HTMLElement).closest('button, input')) {
      this.activate(row);
    }
  }

  protected activate(row: TreeRow): void {
    if (row.kind === 'folder') {
      void this.tree.toggle(row.path);
    } else if (row.kind === 'project') {
      void this.project.open(row.path);
    }
  }

  protected press(event: KeyboardEvent): void {
    if (this.renaming() !== null) {
      return;
    }

    const rows = this.tree.rows();
    const at = rows.findIndex(row => this.tree.selected().has(row.path) && row.kind !== 'more');
    const row = rows[at];
    const step = (by: number) => {
      const next = rows[Math.max(0, Math.min(rows.length - 1, at + by))];
      if (next && next.kind !== 'more') {
        this.tree.choose(next.path);
        this.show(next);
        this.viewport()?.scrollToIndex(Math.max(0, at + by - 2));
      }
    };

    const handled = this.keyAction(event.key, row, step);
    if (handled) {
      event.preventDefault();
    }
  }

  protected openMenu(event: MouseEvent): void {
    const row = this.rowAt(event);
    event.preventDefault();
    event.stopPropagation();
    if (row && row.kind !== 'more' && !this.tree.selected().has(row.path)) {
      this.tree.choose(row.path);
      this.show(row);
    }

    this.menu.set({ x: event.clientX, y: event.clientY, row });
  }

  /** Carries out a menu item on what the menu was opened over. */
  protected act(item: (row: TreeRow | null) => void | Promise<void>): void {
    const at = this.menu();
    this.menu.set(null);
    void item(at?.row ?? null);
  }

  protected readonly openRow = (row: TreeRow | null): void => {
    if (row) {
      this.activate(row);
    }
  };

  protected readonly newProject = (row: TreeRow | null): void => {
    this.creatingProject.emit(this.folderOf(row));
  };

  protected readonly newFolder = async (row: TreeRow | null): Promise<void> => {
    await this.makeFolder(this.folderOf(row));
  };

  protected readonly rename = (row: TreeRow | null): void => {
    if (row) {
      this.startRename(row.path);
    }
  };

  protected readonly reveal = (row: TreeRow | null): void => {
    const state = this.status.state();
    if (row && state.kind === 'online' && state.system.library) {
      void desktop()?.reveal(`${state.system.library}/projects/${row.path}`);
    }
  };

  protected readonly askDelete = (row: TreeRow | null): void => {
    if (row?.kind === 'folder') {
      this.deleting.set(row);
    }
  };

  protected colour(color: string | null): void {
    const at = this.menu();
    this.menu.set(null);
    if (at?.row?.kind === 'folder') {
      void this.tree.setColour(at.row.path, color);
    }
  }

  protected async confirmDelete(): Promise<void> {
    const row = this.deleting();
    this.deleting.set(null);
    if (row) {
      await this.leaveIfInside([row.path]);
      await this.tree.deleteFolder(row.path, true);
    }
  }

  /** Makes a folder and goes straight to naming it, as a file browser does. */
  async makeFolder(parent: string): Promise<void> {
    const taken = new Set(this.tree.rows().map(row => row.path));
    let name = 'New Folder';
    for (let count = 2; taken.has(parent ? `${parent}/${name}` : name); count += 1) {
      name = `New Folder ${count}`;
    }

    const made = await this.tree.createFolder(parent, name);
    if (made !== null) {
      this.startRename(made);
    }
  }

  protected start(event: DragEvent): void {
    const row = this.rowAt(event);
    if (row === null || row.kind === 'more') {
      event.preventDefault();
      return;
    }

    if (!this.tree.selected().has(row.path)) {
      this.tree.choose(row.path);
    }

    this.dragged = [...this.tree.selected()];
    event.dataTransfer?.setData(DragType, JSON.stringify(this.dragged));
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = 'move';
    }
  }

  protected over(event: DragEvent): void {
    const into = this.dropTarget(this.rowAt(event));
    if (!canDrop(this.dragged, into)) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = 'move';
    }

    this.dropInto.set(into);
  }

  protected async drop(event: DragEvent): Promise<void> {
    event.preventDefault();
    event.stopPropagation();
    const into = this.dropTarget(this.rowAt(event));
    const paths = this.dragged;
    this.dragged = [];
    this.dropInto.set(null);
    if (canDrop(paths, into)) {
      await this.leaveIfInside(paths);
      await this.tree.move(paths, into);
    }
  }

  protected renameKey(event: KeyboardEvent): void {
    event.stopPropagation();
    if (event.key === 'Escape') {
      this.renaming.set(null);
    } else if (event.key === 'Enter') {
      (event.target as HTMLInputElement).blur();
    }
  }

  protected async finishRename(event: FocusEvent, row: TreeRow): Promise<void> {
    if (this.renaming() !== row.path) {
      return;
    }

    this.renaming.set(null);
    const name = (event.target as HTMLInputElement).value.trim();
    if (name.length === 0 || name === this.labelOf(row)) {
      return;
    }

    if (row.kind === 'folder') {
      await this.leaveIfInside([row.path]);
      await this.tree.renameFolder(row.path, name);
    } else if (row.kind === 'project') {
      await this.tree.renameProject(row.path, name);
    }
  }

  /** One listener for every row, rather than one per row recycled by the viewport. */
  private rowAt(event: Event): TreeRow | null {
    const element = (event.target as HTMLElement).closest<HTMLElement>('[data-path]');
    const { path, kind } = element?.dataset ?? {};
    return this.tree.rows().find(row => row.path === path && row.kind === kind) ?? null;
  }

  private keyAction(key: string, row: TreeRow | undefined, step: (by: number) => void): boolean {
    switch (key) {
      case 'ArrowDown':
        step(1);
        return true;
      case 'ArrowUp':
        step(-1);
        return true;
      case 'ArrowRight':
        if (row?.kind === 'folder') {
          void this.tree.expand(row.path);
        }

        return row !== undefined;
      case 'ArrowLeft':
        if (row?.kind === 'folder' && row.expanded) {
          this.tree.collapse(row.path);
        } else if (row && parentOf(row.path) !== '') {
          this.tree.choose(parentOf(row.path));
        }

        return row !== undefined;
      case 'Enter':
        if (row) {
          this.activate(row);
        }

        return row !== undefined;
      case 'F2':
        if (row) {
          this.startRename(row.path);
        }

        return row !== undefined;
      case 'Delete':
      case 'Backspace':
        if (row?.kind === 'folder') {
          this.deleting.set(row);
        }

        return row?.kind === 'folder';
      default:
        return false;
    }
  }

  private startRename(path: string): void {
    this.renaming.set(path);
    setTimeout(() => {
      const input = this.renameInput()?.nativeElement;
      input?.focus();
      input?.select();
    });
  }

  /** What a drop on a row means: into a folder, or beside a project. */
  private dropTarget(row: TreeRow | null): string {
    if (row === null) {
      return '';
    }

    return row.kind === 'folder' ? row.path : row.kind === 'project' ? parentOf(row.path) : row.path;
  }

  private folderOf(row: TreeRow | null): string {
    if (row === null) {
      return '';
    }

    return row.kind === 'project' ? parentOf(row.path) : row.path;
  }

  /** A project that is open cannot move, so moving it closes it; its paint has to be saved by then. */
  private async leaveIfInside(paths: readonly string[]): Promise<void> {
    const open = this.project.current()?.path;
    if (open && paths.some(path => open === path || open.startsWith(`${path}/`))) {
      await this.project.close();
    }
  }

  private show(row: TreeRow): void {
    if (row.kind === 'project') {
      this.selection.select({ kind: 'project', project: row.project });
    } else {
      this.selection.clear();
    }
  }
}
