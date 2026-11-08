import { CdkVirtualScrollViewport, ScrollingModule } from '@angular/cdk/scrolling';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  type ElementRef,
  computed,
  inject,
  output,
  signal,
  viewChild
} from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiContextMenuImports } from '@xui/context-menu';
import { XuiDialogImports } from '@xui/dialog';
import { XuiInputImports } from '@xui/input';
import { XuiMenuImports } from '@xui/menu';
import { Commands } from '../core/commands';
import { desktop } from '../core/desktop';
import { documentKindName } from '../core/labels';
import { MenuKeys } from '../core/menu-keys';
import { SystemStatus } from '../core/system-status';
import { LibrarySelection } from './library-selection';
import { FolderColours, LibraryTreeStore, type TreeRow, canDrop, parentOf } from './library-tree-store';
import { type DocumentTab, ProjectStore } from './project-store';
import { ViewportFit } from './viewport-fit';

const RowHeight = 28;
const Indent = 14;
const DragType = 'application/x-patina-library';

/** What a graph can be used as, which decides where the shelf puts it. */
const GraphRoles: readonly { role: string; label: string }[] = [
  { role: 'material', label: 'Use as Material' },
  { role: 'mask', label: 'Use as Mask' },
  { role: 'brush', label: 'Use as Brush' },
  { role: 'filter', label: 'Use as Filter' }
];

/** What each folder colour is called, in the order the store keeps them. */
const ColourNames: readonly string[] = ['Red', 'Orange', 'Yellow', 'Green', 'Teal', 'Blue', 'Purple', 'Pink'];

/** A move or rename that would close open documents, waiting for a yes. */
interface Closing {
  what: string;
  documents: readonly DocumentTab[];
  go: () => Promise<void>;
}

/** The projects as folders: drag to regroup, right-click or the keyboard for the rest. */
@Component({
  selector: 'ptn-library-tree',
  imports: [
    MenuKeys,
    ScrollingModule,
    ViewportFit,
    XuiButtonImports,
    XuiCalloutImports,
    XuiContextMenuImports,
    XuiDialogImports,
    XuiInputImports,
    XuiMenuImports
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'relative flex min-h-0 flex-1 flex-col' },
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
      ptnMenuKeys="[role=treeitem][aria-selected=true]"
      (aiming)="aimMenu($event)"
      [xuiContextMenuTriggerFor]="treeMenu"
      (pointerdown)="pressed($event)"
      (contextmenu)="aimMenu($event)"
      (contextMenuClosed)="restoreFocus()"
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
          [attr.aria-expanded]="row.kind === 'folder' || row.kind === 'project' ? row.expanded : null"
          [attr.aria-selected]="tree.selected().has(row.path)"
          [attr.draggable]="(row.kind === 'folder' || row.kind === 'project') && renaming() !== row.path"
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
            @case ('document') {
              <span class="size-4 shrink-0"></span>
              <span class="text-foreground-muted w-4 shrink-0 text-center text-xs" [title]="kindName(row.document.kind)">
                {{ kindMark(row.document.kind) }}
              </span>
            }
            @case ('project') {
              <button
                type="button"
                class="text-foreground-muted grid size-4 shrink-0 place-items-center"
                tabindex="-1"
                data-testid="tree-toggle"
                [attr.aria-label]="row.expanded ? 'Close ' + row.project.name : 'Open ' + row.project.name"
                (click)="toggleProject($event, row.path)"
              >
                <svg viewBox="0 0 16 16" class="size-3" [class.rotate-90]="row.expanded" aria-hidden="true">
                  <path d="M6 3l5 5-5 5z" fill="currentColor" />
                </svg>
              </button>
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

    <ng-template #treeMenu>
      <xui-menu data-testid="tree-menu" aria-label="Library">
        @let row = menuRow();
        @if (row?.kind === 'project' || row?.kind === 'document') {
          <button type="button" xuiMenuItem data-testid="menu-open" (triggered)="act(openRow)">Open</button>
        }
        @if (row?.kind === 'project') {
          <button type="button" xuiMenuItem data-testid="menu-project-settings" (triggered)="act(openSettings)">
            Project Settings…
          </button>
        }
        @if (row?.kind === 'document' && row.document.kind === 'graph') {
          <xui-menu-divider />
          @for (choice of graphRoles; track choice.role) {
            <button
              type="button"
              xuiMenuItem
              [attr.role]="'menuitemradio'"
              [attr.aria-checked]="(row.document.role ?? 'material') === choice.role"
              [attr.data-testid]="'menu-role-' + choice.role"
              [selected]="(row.document.role ?? 'material') === choice.role"
              (triggered)="setRole(row, choice.role)"
            >
              {{ choice.label }}
            </button>
          }
          <xui-menu-divider />
        }
        <button type="button" xuiMenuItem data-testid="menu-new-project" (triggered)="act(newProject)">New Project…</button>
        <button type="button" xuiMenuItem data-testid="menu-new-folder" (triggered)="act(newFolder)">New Folder</button>
        @if (row?.kind === 'folder' || row?.kind === 'project') {
          <xui-menu-divider />
          <button type="button" xuiMenuItem data-testid="menu-rename" (triggered)="act(rename)">
            Rename <span menuItemLabel>F2</span>
          </button>
          @if (canReveal()) {
            <button type="button" xuiMenuItem data-testid="menu-reveal" (triggered)="act(reveal)">Show in File Browser</button>
          }
        }
        @if (row?.kind === 'project') {
          <button type="button" xuiMenuItem data-testid="menu-duplicate" (triggered)="act(duplicate)">Duplicate</button>
          <xui-menu-divider />
          <button type="button" xuiMenuItem color="error" data-testid="menu-delete-project" (triggered)="act(askDelete)">
            Delete Project…
          </button>
        }
        @if (row?.kind === 'folder') {
          <button type="button" xuiMenuItem data-testid="menu-colour" [xuiMenuTriggerFor]="colourMenu">Colour</button>
          <xui-menu-divider />
          <button type="button" xuiMenuItem color="error" data-testid="menu-delete" (triggered)="act(askDelete)">
            Delete Folder…
          </button>
        }
      </xui-menu>
    </ng-template>

    <ng-template #colourMenu>
      <xui-menu aria-label="Folder colour">
        @let row = menuRow();
        @let held = row?.kind === 'folder' ? (row.folder.color ?? null) : null;
        <button type="button" xuiMenuItem data-testid="menu-colour-none" [selected]="held === null" (triggered)="colour(null)">
          No Colour
        </button>
        @for (choice of colours; track choice.hex) {
          <button
            type="button"
            xuiMenuItem
            [attr.data-testid]="'menu-colour-' + choice.hex"
            [selected]="held === choice.hex"
            (triggered)="colour(choice.hex)"
          >
            <span class="flex items-center gap-2">
              <span class="size-3 shrink-0 rounded-full" aria-hidden="true" [style.background]="choice.hex"></span>
              {{ choice.name }}
            </span>
          </button>
        }
      </xui-menu>
    </ng-template>

    <xui-dialog size="sm" title="Delete project" [open]="deletingProject() !== null" (openChange)="$event || deletingProject.set(null)">
      <xui-dialog-body data-testid="tree-delete-project">
        Delete {{ projectName(deletingProject()) }}? Its files are kept in the library's trash, and can be brought back
        from there by hand.
      </xui-dialog-body>
      <xui-dialog-footer>
        <button type="button" xuiButton variant="ghost" (click)="deletingProject.set(null)">Cancel</button>
        <button type="button" xuiButton color="error" data-testid="tree-delete-project-confirm" (click)="confirmDeleteProject()">
          Delete Project
        </button>
      </xui-dialog-footer>
    </xui-dialog>

    <xui-dialog size="sm" title="Delete folder" [open]="deleting() !== null" (openChange)="$event || deleting.set(null)">
      <xui-dialog-body data-testid="tree-delete">
        @if (deleting()?.folder; as folder) {
          @if (folder.projectCount > 0) {
            {{ folder.name }} holds {{ folder.projectCount }} {{ folder.projectCount === 1 ? 'project' : 'projects' }}.
            They move up a level, and nothing is deleted but the folder.
          } @else {
            Delete {{ folder.name }} and any empty folders in it?
          }
        }
      </xui-dialog-body>
      <xui-dialog-footer>
        <button type="button" xuiButton variant="ghost" (click)="deleting.set(null)">Cancel</button>
        <button type="button" xuiButton color="error" data-testid="tree-delete-confirm" (click)="confirmDelete()">
          Delete Folder
        </button>
      </xui-dialog-footer>
    </xui-dialog>

    <xui-dialog size="sm" title="Close open documents?" [open]="closing() !== null" (openChange)="$event || closing.set(null)">
      <xui-dialog-body data-testid="tree-closing">
        @if (closing(); as ask) {
          <p>
            {{ ask.what }} closes
            {{ ask.documents.length === 1 ? ask.documents[0]?.name : ask.documents.length + ' open documents' }}, and
            {{ ask.documents.length === 1 ? 'its' : 'their' }} undo history is lost. Saved work is kept.
          </p>
          @if (ask.documents.length > 1) {
            <ul class="text-foreground-muted mt-1 list-inside list-disc text-xs">
              @for (document of ask.documents; track document.path) {
                <li>{{ document.name }}</li>
              }
            </ul>
          }
        }
      </xui-dialog-body>
      <xui-dialog-footer>
        <button type="button" xuiButton variant="ghost" (click)="closing.set(null)">Cancel</button>
        <button type="button" xuiButton color="warning" data-testid="tree-closing-confirm" (click)="confirmClosing()">
          Close and Continue
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
  protected readonly colours = FolderColours.map((hex, at) => ({ hex, name: ColourNames[at] ?? hex }));
  /** The row a menu is about, or null for the library itself. */
  protected readonly menuRow = signal<TreeRow | null>(null);
  protected readonly closing = signal<Closing | null>(null);
  protected readonly renaming = signal<string | null>(null);
  protected readonly dropInto = signal<string | null>(null);
  protected readonly deleting = signal<Extract<TreeRow, { kind: 'folder' }> | null>(null);
  protected readonly deletingProject = signal<TreeRow | null>(null);
  private readonly commands = inject(Commands);

  /** The folder chosen in the tree, which Edit > Delete deletes while the Library has focus. */
  private readonly chosenFolder = computed(
    () =>
      this.tree
        .rows()
        .find((row): row is Extract<TreeRow, { kind: 'folder' }> => row.kind === 'folder' && this.tree.selected().has(row.path)) ??
      null
  );

  constructor() {
    this.commands.handle(
      'delete',
      {
        enabled: () => this.commands.focus().area === 'library' && this.chosenFolder() !== null,
        run: () => this.deleting.set(this.chosenFolder())
      },
      inject(DestroyRef)
    );
  }
  protected readonly canReveal = computed(() => desktop() !== undefined);
  private dragged: readonly string[] = [];

  protected byPath(_index: number, row: TreeRow): string {
    return `${row.kind}:${row.path}`;
  }

  protected labelOf(row: TreeRow): string {
    return row.kind === 'folder'
      ? row.folder.name
      : row.kind === 'project'
        ? row.project.name
        : row.kind === 'document'
          ? row.document.name
          : '';
  }

  protected isOpen(row: TreeRow): boolean {
    return (
      (row.kind === 'project' && this.project.current()?.path === row.path) ||
      (row.kind === 'document' && this.project.active()?.path === row.path)
    );
  }

  protected kindName(kind: string): string {
    return documentKindName(kind);
  }

  protected kindMark(kind: string): string {
    return documentKindName(kind).slice(0, 1);
  }

  protected toggleProject(event: Event, path: string): void {
    event.stopPropagation();
    void this.tree.toggleProject(path);
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

    if (row.kind === 'document') {
      this.tree.choose(row.path);
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
    } else if (row.kind === 'document') {
      void this.project.openDocument(row.project, row.path);
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

  /**
   * A menu is about the row it opens on, chosen unless it is already in what is selected. The press before a
   * right-click says which, so the menu draws once, with the right items, as it opens.
   */
  /**
   * A right press aims the menu before it opens. ⚠ A method, not `button === 2 && …` in the template: a listener that
   * returns false makes Angular prevent the default, and a pointerdown with its default prevented never starts a drag.
   */
  protected pressed(event: PointerEvent): void {
    if (event.button === 2) {
      this.aimMenu(event);
    }
  }

  protected aimMenu(event: { target: EventTarget | null }): void {
    const found = this.rowAt(event);
    const row = found?.kind === 'more' ? null : found;
    if (row && row.kind !== 'document' && !this.tree.selected().has(row.path)) {
      this.tree.choose(row.path);
      this.show(row);
    }

    this.menuRow.set(row);
  }

  /** A closed menu gives focus back to the tree, unless an item sent it somewhere on purpose. */
  protected restoreFocus(): void {
    // The menu says it closed while focus is still on its item, so this looks once the menu has gone.
    setTimeout(() => {
      const active = globalThis.document.activeElement;
      if (this.renaming() === null && (active === null || active === globalThis.document.body)) {
        this.viewport()?.elementRef.nativeElement.focus();
      }
    });
  }

  protected readonly graphRoles = GraphRoles;

  protected setRole(row: Extract<TreeRow, { kind: 'document' }>, role: string): void {
    void this.project.setRole(row.project, row.document, role);
  }

  /** Carries out a menu item on what the menu was opened over. */
  protected act(item: (row: TreeRow | null) => void | Promise<void>): void {
    void item(this.menuRow());
  }

  /** The settings belong to what is open, so a project opens first. */
  protected readonly openSettings = (row: TreeRow | null): void => {
    if (row?.kind === 'project') {
      void this.project.openSettings(row.path);
    }
  };

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
    } else if (row?.kind === 'project') {
      this.deletingProject.set(row);
    }
  };

  protected readonly duplicate = (row: TreeRow | null): void => {
    if (row?.kind === 'project') {
      void this.tree.duplicateProject(row.path, `${row.project.name} Copy`);
    }
  };

  protected colour(color: string | null): void {
    const row = this.menuRow();
    if (row?.kind === 'folder') {
      void this.tree.setColour(row.path, color);
    }
  }

  /**
   * Moving or renaming what holds open documents closes them, which loses their undo, so it asks first. Nothing open
   * there, and it goes ahead.
   */
  private whenClosed(paths: readonly string[], what: string, go: () => Promise<void>): void {
    const documents = this.project.openInside(paths);
    if (documents.length === 0) {
      void go();
    } else {
      this.closing.set({ what, documents, go });
    }
  }

  protected async confirmClosing(): Promise<void> {
    const ask = this.closing();
    this.closing.set(null);
    await ask?.go();
  }

  protected async confirmDelete(): Promise<void> {
    const row = this.deleting();
    this.deleting.set(null);
    if (row) {
      if (await this.project.release([row.path])) {
        await this.tree.deleteFolder(row.path, true);
      }
    }
  }

  protected projectName(row: TreeRow | null): string {
    return row?.kind === 'project' ? row.project.name : '';
  }

  protected async confirmDeleteProject(): Promise<void> {
    const row = this.deletingProject();
    this.deletingProject.set(null);
    if (row && (await this.project.release([row.path]))) {
      await this.tree.deleteProject(row.path);
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
    if (row === null || row.kind === 'more' || row.kind === 'document') {
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

  protected drop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    const into = this.dropTarget(this.rowAt(event));
    const paths = this.dragged;
    this.dragged = [];
    this.dropInto.set(null);
    if (canDrop(paths, into)) {
      const what = paths.length === 1 ? `Moving ${this.nameAt(paths[0] ?? '')}` : `Moving ${paths.length} items`;
      this.whenClosed(paths, what, async () => {
        if (await this.project.release(paths)) {
          await this.tree.move(paths, into);
        }
      });
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
      this.whenClosed([row.path], `Renaming ${row.folder.name}`, async () => {
        if (await this.project.release([row.path])) {
          await this.tree.renameFolder(row.path, name);
        }
      });
    } else if (row.kind === 'project') {
      await this.tree.renameProject(row.path, name);
    }
  }

  private nameAt(path: string): string {
    const row = this.tree.rows().find(held => held.path === path);
    return row && row.kind !== 'more' ? this.labelOf(row) : (path.split('/').pop() ?? path);
  }

  /** One listener for every row, rather than one per row recycled by the viewport. */
  private rowAt(event: { target: EventTarget | null }): TreeRow | null {
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

    return this.folderOf(row);
  }

  private folderOf(row: TreeRow | null): string {
    if (row === null) {
      return '';
    }

    return row.kind === 'project' ? parentOf(row.path) : row.kind === 'document' ? parentOf(row.project) : row.path;
  }

  private show(row: TreeRow): void {
    if (row.kind === 'project') {
      this.selection.select({ kind: 'project', project: row.project });
    } else {
      this.selection.clear();
    }
  }
}
