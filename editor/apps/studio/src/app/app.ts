import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  linkedSignal,
  signal,
  untracked
} from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { XuiDialogImports } from '@xui/dialog';
import { type XuiDockManagerLayout, XuiDockManagerImports } from '@xui/dock-manager';
import { XuiInputImports } from '@xui/input';
import { BakePane } from './bake/bake-pane';
import { BakeSettings } from './bake/bake-settings';
import { BakeStore } from './bake/bake-store';
import { menuModel } from './core/app-menu';
import { type CommandHandler, Commands } from './core/commands';
import { desktop } from './core/desktop';
import { DiagnosticsDialog } from './core/diagnostics-dialog';
import { EditCommands } from './core/edit-commands';
import { PaintCommands } from './paint/paint-commands';
import { FileConflictDialog } from './core/file-conflict-dialog';
import { HistoryPane } from './core/history-pane';
import { PreferencesDialog } from './core/preferences-dialog';
import { ShortcutsDialog } from './core/shortcuts-dialog';
import { StartPane } from './core/start-pane';
import { SystemStatus } from './core/system-status';
import { ViewOptions } from './core/view-options';
import {
  chooseLayout,
  chosenLayout,
  holds,
  keepLayout,
  layoutNames,
  layoutOf,
  resetLayout,
  showing
} from './core/workspaces';
import { ChecksPane } from './export/checks-pane';
import { ExportPane } from './export/export-pane';
import { Inspector } from './graph/graph-inspector';
import { GraphPane } from './graph/graph-pane';
import { GraphStore } from './graph/graph-store';
import { ImageMaterialPane } from './graph/image-material-pane';
import { NodeLibrary } from './graph/node-library';
import { TextureView } from './graph/texture-view';
import { LibraryPane } from './library/library-pane';
import { ProjectSettingsDialog } from './library/project-settings-dialog';
import { TextureSets } from './paint/texture-sets';
import { ProjectCommands } from './library/project-commands';
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
    ChecksPane,
    ExportPane,
    GraphPane,
    HistoryPane,
    PreferencesDialog,
    ShortcutsDialog,
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
    '(document:keydown)': 'commands.press($event)',
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
        <button
          type="button"
          xuiButton
          size="sm"
          variant="ghost"
          data-testid="open-bake"
          [disabled]="!commands.enabled('bake')"
          [title]="commands.tooltip('bake')"
          (click)="commands.run('bake')"
        >
          Bake…
        </button>
        <button
          type="button"
          xuiButton
          size="sm"
          variant="ghost"
          data-testid="quick-bake"
          [disabled]="bakeSettings.blocked() !== null"
          [title]="bakeSettings.blocked() ?? commands.tooltip('quickBake')"
          (click)="commands.run('quickBake')"
        >
          {{ bakes.busy() ? 'Baking ' + bakePercent() + '%' : 'Quick Bake' }}
        </button>
        <button
          type="button"
          xuiButton
          size="sm"
          variant="ghost"
          data-testid="open-export"
          [title]="commands.tooltip('export')"
          (click)="commands.run('export')"
        >
          Export…
        </button>
      </div>
    </header>

    <!--
      Only what the start shows first is in the first bundle: the Start page and the Library. Every other pane, the
      Inspector included since it starts with nothing to show, and every dialog loads the first time it is shown (#221),
      from disk, so it costs a moment once. A pane deferred here must not be imported by anything the first bundle
      holds, or it lands back in it.
    -->
    <xui-dock-manager class="min-h-0 flex-1" [layout]="layout()" (layoutChange)="keep($event)">
      <ng-template xuiDockContent="start">
        <ptn-start />
      </ng-template>
      <ng-template xuiDockContent="atlas">
        @defer (on immediate) {
          <ptn-paint-atlas />
        }
      </ng-template>
      <ng-template xuiDockContent="image">
        @defer (on immediate) {
          <ptn-image-material />
        }
      </ng-template>
      <ng-template xuiDockContent="sets">
        @defer (on immediate) {
          <ptn-texture-sets />
        }
      </ng-template>
      <ng-template xuiDockContent="layers">
        @defer (on immediate) {
          <ptn-layer-stack />
        }
      </ng-template>
      <ng-template xuiDockContent="shelf">
        @defer (on immediate) {
          <ptn-material-shelf />
        }
      </ng-template>
      <ng-template xuiDockContent="library">
        <ptn-library />
      </ng-template>
      <ng-template xuiDockContent="viewport">
        @defer (on immediate) {
          <ptn-viewport />
        }
      </ng-template>
      <ng-template xuiDockContent="texture">
        @defer (on immediate) {
          <ptn-texture-view />
        }
      </ng-template>
      <ng-template xuiDockContent="graph">
        @defer (on immediate) {
          <ptn-graph />
        }
      </ng-template>
      <ng-template xuiDockContent="nodes">
        @defer (on immediate) {
          <ptn-node-library />
        }
      </ng-template>
      <ng-template xuiDockContent="inspector">
        @defer (on immediate) {
          <ptn-inspector />
        }
      </ng-template>
      <ng-template xuiDockContent="paint">
        @defer (on immediate) {
          <ptn-paint />
        }
      </ng-template>
      <ng-template xuiDockContent="history">
        @defer (on immediate) {
          <ptn-history />
        }
      </ng-template>
      <ng-template xuiDockContent="checks">
        @defer (on immediate) {
          <ptn-checks />
        }
      </ng-template>
    </xui-dock-manager>

    @defer (when project.settingsOpen()) {
      <ptn-project-settings />
    }

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

    @if (paint.notice(); as notice) {
      <div
        class="bg-warning-subtle flex shrink-0 items-center gap-2 px-3 py-1 text-sm"
        role="status"
        data-testid="paint-notice"
      >
        <span class="min-w-0 flex-1 truncate" [title]="notice">{{ notice }}</span>
        <button type="button" xuiButton size="sm" variant="ghost" (click)="paint.notice.set(null)">Dismiss</button>
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
        @defer (when baking()) {
          <div class="flex h-[70vh] min-h-0 flex-col"><ptn-bake /></div>
        }
      </xui-dialog-body>
    </xui-dialog>
    <xui-dialog class="max-w-5xl" size="lg" title="Export" [(open)]="exporting">
      <xui-dialog-body>
        @defer (when exporting()) {
          <div class="flex h-[75vh] min-h-0 flex-col"><ptn-export /></div>
        }
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

    @defer (when diagnosing()) {
      <ptn-diagnostics [(open)]="diagnosing" />
    }

    @defer (when keys()) {
      <ptn-shortcuts [(open)]="keys" />
    }

    @defer (when preferencesOpen()) {
      <ptn-preferences [(open)]="preferencesOpen" />
    }

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

    <xui-dialog
      size="sm"
      title="Close Project?"
      [open]="project.closingProject() !== null"
      (openChange)="$event || project.resolveClosingProject('cancel')"
    >
      <xui-dialog-body data-testid="project-closing"
        >Some documents of this project have changes that are not saved.</xui-dialog-body
      >
      <xui-dialog-footer>
        <button type="button" xuiButton variant="ghost" (click)="project.resolveClosingProject('cancel')">Cancel</button>
        <button type="button" xuiButton variant="ghost" (click)="project.resolveClosingProject('discard')">
          Don't Save
        </button>
        <button type="button" xuiButton color="primary" (click)="project.resolveClosingProject('save')">Save All</button>
      </xui-dialog-footer>
    </xui-dialog>
  `
})
export class App {
  protected readonly status = inject(SystemStatus);
  protected readonly project = inject(ProjectStore);
  protected readonly bakes = inject(BakeStore);
  protected readonly bakeSettings = inject(BakeSettings);
  /** How far the jobs of the last bake are, together. */
  protected readonly bakePercent = computed(() => {
    const jobs = this.bakes.batch();
    return jobs.length === 0 ? 0 : Math.round((jobs.reduce((sum, job) => sum + job.progress, 0) / jobs.length) * 100);
  });
  private readonly edit = inject(EditCommands);
  private readonly options = inject(ViewOptions);
  protected readonly commands = inject(Commands);
  private readonly graph = inject(GraphStore);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly keys = signal(false);
  protected readonly preferencesOpen = signal(false);
  protected readonly diagnosing = signal(false);
  protected readonly paint = inject(PaintStore);
  /** The one thing that went wrong last, which stays until it is read. */
  protected readonly failure = computed(() => this.paint.failure() ?? this.project.failure());
  protected readonly baking = signal(false);

  /** Bakes with the project's settings. A refusal opens the dialog, which says why. */
  private async quickBake(): Promise<void> {
    if (!(await this.bakeSettings.bake())) {
      this.baking.set(true);
    }
  }
  protected readonly exporting = signal(false);
  /** Which of its named layouts the workspace in front is in. */
  private readonly chosen = linkedSignal(() => chosenLayout(this.project.workspace()));
  /** The layout of what is in front. Each kind of document keeps its own (ADR 0012). */
  protected readonly layout = linkedSignal(() => layoutOf(this.project.workspace(), this.chosen()));
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
    // The labels every list shows come with the stack vocabulary, so it is read as soon as there is a Server to ask.
    effect(() => {
      if (this.status.state().kind === 'online') {
        untracked(() => void this.paint.loadVocabulary().catch(() => undefined)); // a stack that opens asks again
      }
    });

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
    this.edit.install();
    inject(PaintCommands).install();
    this.installCommands();
    bridge?.onCommand((command, accelerator) => this.commands.run(command, accelerator ? 'accelerator' : 'menu'));
    // The native menus are the command table's, with what is on now: sent whenever an answer changes (#188).
    let sent = '';
    effect(() => {
      const menus = menuModel(this.commands, {
        stack: this.paint.state() !== null && this.project.active() !== null && !this.project.graphInFront(),
        graph: this.project.graphInFront() && this.graph.document() !== null,
        viewport: holds(this.layout(), 'viewport')
      });
      const text = JSON.stringify(menus);
      if (text !== sent) {
        sent = text;
        bridge?.setMenu(menus);
      }
    });
    if (bridge) {
      effect(() => {
        bridge.setTitle(this.title());
      });
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
    keepLayout(this.project.workspace(), layout, this.chosen());
    this.layout.set(layout);
  }

  /**
   * The commands the shell answers: dialogs, layouts and help. The File commands about the document in front are
   * {@link ProjectCommands}'.
   */
  private installCommands(): void {
    const project = this.project;
    const handle = (id: string, handler: CommandHandler) => this.commands.handle(id, handler, this.destroyRef);
    const bridge = desktop();
    inject(ProjectCommands).install();

    if (bridge) {
      handle('newLibrary', { run: () => bridge.newLibrary() });
      handle('openLibrary', { run: () => bridge.openLibrary() });
    }

    handle('export', { run: () => this.exporting.set(true) });
    // With no project there is no mesh to bake, and the dialog would only say so.
    handle('bake', { enabled: () => project.current() !== null, run: () => this.baking.set(true) });
    handle('quickBake', { enabled: () => this.bakeSettings.blocked() === null, run: () => this.quickBake() });
    handle('toggleStats', { checked: () => this.options.stats(), run: () => this.options.toggleStats() });
    for (const [at, id] of ['layout1', 'layout2', 'layout3'].entries()) {
      handle(id, {
        enabled: () => at < layoutNames(project.workspace()).length,
        label: () => layoutNames(project.workspace())[at] ?? null,
        checked: () => this.chosen() === at && at < layoutNames(project.workspace()).length,
        run: () => {
          chooseLayout(project.workspace(), at);
          this.chosen.set(at);
        }
      });
    }

    handle('resetLayout', { run: () => this.layout.set(resetLayout(project.workspace(), this.chosen())) });
    handle('showHistory', {
      enabled: () => holds(this.layout(), 'history'),
      run: () => this.layout.update(layout => showing(layout, 'history'))
    });
    handle('showChecks', {
      enabled: () => holds(this.layout(), 'checks'),
      run: () => this.layout.update(layout => showing(layout, 'checks'))
    });
    handle('shortcuts', { run: () => this.keys.set(true) });
    handle('preferences', { run: () => this.preferencesOpen.set(true) });
    handle('diagnostics', { run: () => this.diagnosing.set(true) });
    handle('reportProblem', {
      run: () => globalThis.open('https://github.com/Rikarin/Patina/issues/new', '_blank')
    });
  }
}
