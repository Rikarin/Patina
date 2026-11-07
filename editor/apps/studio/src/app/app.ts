import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, linkedSignal, signal } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { XuiDialogImports } from '@xui/dialog';
import { type XuiDockManagerLayout, XuiDockManagerImports } from '@xui/dock-manager';
import { XuiInputImports } from '@xui/input';
import { BakePane } from './bake/bake-pane';
import { BakeStore } from './bake/bake-store';
import { desktop } from './core/desktop';
import { DiagnosticsDialog } from './core/diagnostics-dialog';
import { EditCommands } from './core/edit-commands';
import { FileConflictDialog } from './core/file-conflict-dialog';
import { Shortcuts } from './core/shortcuts';
import { StartPane } from './core/start-pane';
import { SystemStatus } from './core/system-status';
import { ViewOptions } from './core/view-options';
import { keepLayout, layoutOf, resetLayout, showing } from './core/workspaces';
import { ExportPane } from './export/export-pane';
import { Inspector } from './graph/graph-inspector';
import { GraphPane } from './graph/graph-pane';
import { ImageMaterialPane } from './graph/image-material-pane';
import { NodeLibrary } from './graph/node-library';
import { TextureView } from './graph/texture-view';
import { LibraryPane } from './library/library-pane';
import { ProjectSettingsDialog } from './library/project-settings-dialog';
import { TextureSets } from './paint/texture-sets';
import { ProjectStore } from './library/project-store';
import { LayerStack } from './paint/layer-stack';
import { MaterialShelf } from './paint/material-shelf';
import { PaintAtlas } from './paint/paint-atlas';
import { PaintPane } from './paint/paint-pane';
import { PaintStore } from './paint/paint-store';
import { ViewportPane } from './viewport/viewport-pane';

/** The app shell: a title bar, the docked panes and the status bar. `chrome-bar` and `readout` are bezel's. */
@Component({
  selector: 'ptn-root',
  imports: [
    BakePane,
    ExportPane,
    GraphPane,
    ImageMaterialPane,
    Inspector,
    NodeLibrary,
    ProjectSettingsDialog,
    TextureSets,
    LayerStack,
    LibraryPane,
    DatePipe,
    DiagnosticsDialog,
    FileConflictDialog,
    MaterialShelf,
    XuiInputImports,
    PaintAtlas,
    PaintPane,
    StartPane,
    TextureView,
    ViewportPane,
    XuiButtonImports,
    XuiDialogImports,
    XuiDockManagerImports
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'flex h-full flex-col',
    '(document:keydown)': 'shortcuts.press($event)',
    '(document:dragover)': 'allowFiles($event)',
    '(document:drop)': 'dropFiles($event)'
  },
  template: `
    <header class="chrome-bar window-drag flex h-10 shrink-0 items-center gap-2 px-3" [class.pl-20]="traffic()">
      <span class="shrink-0 font-semibold" data-testid="app-title" [title]="title()">Patina</span>
      <div class="flex min-w-0 flex-1 items-center gap-1 overflow-x-auto" role="tablist" aria-label="Open documents">
        @for (tab of project.tabs(); track tab.path) {
          <div
            class="flex items-center gap-1 rounded px-2 py-0.5 text-sm"
            [class.bg-selection]="tab.path === project.active()?.path"
            data-testid="document-tab"
            [attr.data-kind]="tab.kind"
          >
            <button
              type="button"
              class="max-w-40 truncate"
              role="tab"
              data-testid="document-tab-name"
              [attr.aria-selected]="tab.path === project.active()?.path"
              [title]="tab.path"
              (click)="project.activate(tab.path)"
            >
              {{ tab.name }}{{ tab.dirty ? ' •' : '' }}
            </button>
            <button
              type="button"
              [attr.aria-label]="'Close ' + tab.name"
              data-testid="document-tab-close"
              (click)="project.closeTab(tab.path)"
            >
              ×
            </button>
          </div>
        }
      </div>
      <div class="flex shrink-0 items-center gap-1">
        <button type="button" xuiButton size="sm" variant="ghost" data-testid="open-bake" (click)="baking.set(true)">
          Bake…
        </button>
        <button
          type="button"
          xuiButton
          size="sm"
          variant="ghost"
          data-testid="open-export"
          (click)="exporting.set(true)"
        >
          Export…
        </button>
      </div>
    </header>

    <xui-dock-manager class="min-h-0 flex-1" [layout]="layout()" (layoutChange)="keep($event)">
      <ng-template xuiDockContent="start">
        <ptn-start />
      </ng-template>
      <ng-template xuiDockContent="atlas">
        <ptn-paint-atlas />
      </ng-template>
      <ng-template xuiDockContent="image">
        <ptn-image-material />
      </ng-template>
      <ng-template xuiDockContent="sets">
        <ptn-texture-sets />
      </ng-template>
      <ng-template xuiDockContent="layers">
        <ptn-layer-stack />
      </ng-template>
      <ng-template xuiDockContent="shelf">
        <ptn-material-shelf />
      </ng-template>
      <ng-template xuiDockContent="library">
        <ptn-library />
      </ng-template>
      <ng-template xuiDockContent="viewport">
        <ptn-viewport />
      </ng-template>
      <ng-template xuiDockContent="texture">
        <ptn-texture-view />
      </ng-template>
      <ng-template xuiDockContent="graph">
        <ptn-graph />
      </ng-template>
      <ng-template xuiDockContent="nodes">
        <ptn-node-library />
      </ng-template>
      <ng-template xuiDockContent="inspector">
        <ptn-inspector />
      </ng-template>
      <ng-template xuiDockContent="paint">
        <ptn-paint />
      </ng-template>
    </xui-dock-manager>

    <ptn-project-settings />

    @if (failure(); as reason) {
      <div
        class="bg-error-subtle flex shrink-0 items-center gap-2 px-3 py-1 text-sm"
        role="alert"
        data-testid="app-failure"
      >
        <span class="min-w-0 flex-1 truncate" [title]="reason">{{ reason }}</span>
        @if (reason.includes('no texture coordinates')) {
          <button
            type="button"
            xuiButton
            size="sm"
            data-testid="app-unwrap"
            [disabled]="project.unwrapping()"
            (click)="project.applySettings({ autoUnwrap: true })"
          >
            {{ project.unwrapping() ? 'Unwrapping…' : 'Unwrap Mesh' }}
          </button>
        }
        <button
          type="button"
          xuiButton
          size="sm"
          variant="ghost"
          data-testid="app-failure-dismiss"
          (click)="dismissFailure()"
        >
          Dismiss
        </button>
      </div>
    }

    <footer class="chrome-bar flex h-7 shrink-0 items-center gap-4 px-3" role="status" aria-label="Server status">
      @let current = status.state();
      @switch (current.kind) {
        @case ('connecting') {
          <span class="readout">Starting…</span>
        }
        @case ('online') {
          <span class="readout shrink-0">Patina {{ current.system.version }}</span>
          <span class="readout truncate">{{ current.system.library ?? 'No library open' }}</span>
          <span class="readout shrink-0">GPU {{ current.system.worker.connected ? 'ready' : 'starting' }}</span>
          @if (project.notice(); as notice) {
            <span class="readout truncate" data-testid="app-notice">{{ notice }}</span>
          }
        }
        @case ('offline') {
          <span class="readout text-error truncate">The engine stopped: {{ current.reason }}</span>
        }
      }
    </footer>

    <xui-dialog class="max-w-5xl" size="lg" title="Bake" [(open)]="baking">
      <xui-dialog-body>
        <div class="flex h-[70vh] min-h-0 flex-col"><ptn-bake /></div>
      </xui-dialog-body>
    </xui-dialog>
    <xui-dialog size="lg" title="Export" [(open)]="exporting">
      <xui-dialog-body>
        <div class="flex h-[70vh] min-h-0 flex-col"><ptn-export /></div>
      </xui-dialog-body>
    </xui-dialog>

    <xui-dialog
      size="sm"
      title="Unsaved changes"
      [open]="project.closing() !== null"
      (openChange)="$event || project.resolveClosing('cancel')"
    >
      <xui-dialog-body data-testid="project-leaving">This document has changes that are not saved.</xui-dialog-body>
      <xui-dialog-footer>
        <button type="button" xuiButton variant="ghost" (click)="project.resolveClosing('cancel')">Cancel</button>
        <button
          type="button"
          xuiButton
          variant="ghost"
          data-testid="project-discard"
          (click)="project.resolveClosing('discard')"
        >
          Don't Save
        </button>
        <button
          type="button"
          xuiButton
          color="primary"
          data-testid="project-save"
          (click)="project.resolveClosing('save')"
        >
          Save
        </button>
      </xui-dialog-footer>
    </xui-dialog>

    <xui-dialog
      size="sm"
      title="Reload Mesh"
      [open]="project.reloading() !== null"
      (openChange)="$event || project.resolveReloading(false)"
    >
      <xui-dialog-body data-testid="mesh-reload">
        The mesh file changed. Open documents close and lose their undo history. Bakes made on the old mesh go out of
        date.
      </xui-dialog-body>
      <xui-dialog-footer>
        <button type="button" xuiButton variant="ghost" (click)="project.resolveReloading(false)">Cancel</button>
        <button
          type="button"
          xuiButton
          color="primary"
          data-testid="mesh-reload-confirm"
          (click)="project.resolveReloading(true)"
        >
          Reload
        </button>
      </xui-dialog-footer>
    </xui-dialog>

    <ptn-file-conflict
      [name]="project.active()?.name ?? ''"
      [open]="paint.saveConflict()"
      (cancelled)="paint.saveConflict.set(false)"
      (overwrite)="paint.act('save', true)"
    />

    <ptn-diagnostics [(open)]="diagnosing" />

    <xui-dialog size="sm" title="Keyboard Shortcuts" [(open)]="keys">
      <xui-dialog-body>
        <div class="flex flex-col gap-1" data-testid="shortcuts">
          @for (shortcut of shortcuts.all(); track shortcut.id) {
            <div class="flex items-center gap-2">
              <span class="min-w-0 flex-1 text-sm">{{ shortcut.label }}</span>
              <button
                type="button"
                xuiButton
                size="sm"
                [attr.aria-label]="'Change the key for ' + shortcut.label"
                (keydown)="bind($event, shortcut.id)"
              >
                {{ shortcut.key.toUpperCase() }}
              </button>
            </div>
          }
        </div>
      </xui-dialog-body>
      <xui-dialog-footer>
        <button type="button" xuiButton variant="ghost" (click)="shortcuts.reset()">Reset</button>
      </xui-dialog-footer>
    </xui-dialog>

    <xui-dialog size="sm" title="Save As" [open]="project.savingAs()" (openChange)="project.savingAs.set($event)">
      <form (submit)="saveAs($event, saveName.value)">
        <xui-dialog-body>
          <input
            #saveName
            class="w-full"
            xuiInput
            aria-label="New name"
            autocomplete="off"
            data-testid="save-as-name"
          />
        </xui-dialog-body>
        <xui-dialog-footer>
          <button type="button" xuiButton variant="ghost" (click)="project.savingAs.set(false)">Cancel</button>
          <button type="submit" xuiButton color="primary" data-testid="save-as-save">Save</button>
        </xui-dialog-footer>
      </form>
    </xui-dialog>

    <xui-dialog
      size="sm"
      title="Recover unsaved work?"
      [open]="project.recoverable() !== null"
      (openChange)="$event || project.dismissRecovery()"
    >
      <xui-dialog-body data-testid="app-recovery">
        Patina kept work on this document that was never saved, from {{ project.recoverable()?.saved | date: 'short' }}.
        Closing this keeps it, and asks again next time.
      </xui-dialog-body>
      <xui-dialog-footer>
        <button
          type="button"
          xuiButton
          variant="ghost"
          data-testid="recovery-discard"
          (click)="project.resolveRecovery(false)"
        >
          Discard
        </button>
        <button
          type="button"
          xuiButton
          color="primary"
          data-testid="recovery-restore"
          (click)="project.resolveRecovery(true)"
        >
          Restore
        </button>
      </xui-dialog-footer>
    </xui-dialog>

    <xui-dialog
      size="sm"
      title="Quit Patina?"
      [open]="project.quitting()"
      (openChange)="$event || project.resolveQuitting('cancel')"
    >
      <xui-dialog-body data-testid="app-quitting"
        >{{ project.unsavedTabs().length }} document(s) have unsaved changes.</xui-dialog-body
      >
      <xui-dialog-footer>
        <button type="button" xuiButton variant="ghost" (click)="project.resolveQuitting('cancel')">Cancel</button>
        <button type="button" xuiButton variant="ghost" (click)="project.resolveQuitting('discard')">Don't Save</button>
        <button type="button" xuiButton color="primary" (click)="project.resolveQuitting('save')">Save All</button>
      </xui-dialog-footer>
    </xui-dialog>
  `
})
export class App {
  protected readonly status = inject(SystemStatus);
  protected readonly project = inject(ProjectStore);
  private readonly bakes = inject(BakeStore);
  private readonly edit = inject(EditCommands);
  private readonly options = inject(ViewOptions);
  protected readonly shortcuts = inject(Shortcuts);
  protected readonly keys = signal(false);
  protected readonly diagnosing = signal(false);
  protected readonly paint = inject(PaintStore);
  /** The one thing that went wrong last, which stays until it is read. */
  protected readonly failure = computed(() => this.paint.failure() ?? this.project.failure());
  protected readonly baking = signal(false);
  protected readonly exporting = signal(false);
  /** The layout of what is in front. Each kind of document keeps its own (ADR 0012). */
  protected readonly layout = linkedSignal(() => layoutOf(this.project.workspace()));
  /** What is open, which is also what the window says. */
  protected readonly title = computed(() => {
    const state = this.status.state();
    const library =
      state.kind === 'online'
        ? state.system.library
            ?.split(/[\\/]/)
            .pop()
            ?.replace(/\.ptlib$/, '')
        : null;
    const name = this.project.active()?.name;
    const dirty = this.project.active()?.dirty ? ' •' : '';
    return [name ? `${name}${dirty}` : null, library].filter(Boolean).join(' — ');
  });
  private readonly fullScreen = signal(false);

  /** Room for macOS's traffic lights, which the desktop app keeps over the title bar and full screen hides. */
  protected readonly traffic = computed(() => desktop()?.platform === 'darwin' && !this.fullScreen());

  constructor() {
    // A picture chosen in the Bake dialog is one the user wants to look at.
    effect(() => {
      if (this.bakes.shown()) {
        this.baking.set(false);
        this.layout.update(layout => showing(layout, 'texture'));
      }
    });

    effect(() => {
      if (this.project.bakeAsked() > 0) {
        this.baking.set(true);
      }
    });

    const bridge = desktop();
    bridge?.onFullScreen(fullScreen => this.fullScreen.set(fullScreen));
    bridge?.onCommand(command => {
      if (this.edit.run(command)) {
        return;
      }

      if (command === 'save') {
        void this.project.saveActive();
      } else if (command === 'saveAs') {
        this.project.savingAs.set(true);
      } else if (command === 'import') {
        void bridge.openFile().then(path => (path ? this.project.importFiles([path]) : undefined));
      } else if (command === 'reloadMesh') {
        void this.project.reloadMesh();
      } else if (command === 'projectSettings') {
        this.project.settingsOpen.set(this.project.current() !== null);
      } else if (command === 'toggleStats') {
        this.options.toggleStats();
      } else if (command === 'resetLayout') {
        this.layout.set(resetLayout(this.project.workspace()));
      } else if (command === 'shortcuts') {
        this.keys.set(true);
      } else if (command === 'diagnostics') {
        this.diagnosing.set(true);
      }
    });
    if (bridge) {
      effect(() => {
        bridge.setTitle(this.title());
      });
    }
  }

  /** The next key pressed in a row of the shortcuts dialog becomes that row's. */
  protected bind(event: KeyboardEvent, id: string): void {
    if (event.key.length === 1) {
      event.preventDefault();
      event.stopPropagation();
      this.shortcuts.rebind(id, event.key);
    }
  }

  /** Files from outside the app can land anywhere on the window. */
  protected allowFiles(event: DragEvent): void {
    if (event.dataTransfer?.types.includes('Files')) {
      event.preventDefault();
    }
  }

  protected dropFiles(event: DragEvent): void {
    const bridge = desktop();
    const files = [...(event.dataTransfer?.files ?? [])];
    if (bridge && files.length > 0) {
      event.preventDefault();
      void this.project.importFiles(files.map(file => bridge.pathOf(file)));
    }
  }

  protected dismissFailure(): void {
    this.paint.failure.set(null);
    this.project.clearFailure();
  }

  protected saveAs(event: Event, name: string): void {
    event.preventDefault();
    if (name.trim()) {
      this.project.savingAs.set(false);
      void this.project.saveAs(name.trim());
    }
  }

  protected keep(layout: XuiDockManagerLayout): void {
    keepLayout(this.project.workspace(), layout);
    this.layout.set(layout);
  }
}
