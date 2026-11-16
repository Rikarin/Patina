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
import { XuiCalloutImports } from '@xui/callout';
import { XuiDialog, XuiDialogBody } from '@xui/dialog';
import { type XuiDockManagerLayout, XuiDockManagerImports } from '@xui/dock-manager';
import { BakePane } from './bake/bake-pane';
import { BakeSettings } from './bake/bake-settings';
import { BakeStore } from './bake/bake-store';
import { AboutDialog } from './core/about-dialog';
import { Activity } from './core/activity';
import { ActivityPanel } from './core/activity-panel';
import { menuModel } from './core/app-menu';
import { type CommandHandler, Commands } from './core/commands';
import { desktop } from './core/desktop';
import { DiagnosticsDialog } from './core/diagnostics-dialog';
import { EditCommands } from './core/edit-commands';
import { FilePicker } from './core/file-picker';
import { Links } from './core/help-links';
import { HistoryPane } from './core/history-pane';
import { KeyHelper } from './core/key-helper';
import { percent } from './core/numbers';
import { PreferencesDialog } from './core/preferences-dialog';
import { SessionDialogs } from './core/session-dialogs';
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
import { GraphBeside } from './graph/graph-beside';
import { Inspector } from './graph/graph-inspector';
import { GraphPane } from './graph/graph-pane';
import { GraphStore } from './graph/graph-store';
import { FrontGraph, GraphViews } from './graph/graph-views';
import { ImageMaterialPane } from './graph/image-material-pane';
import { NodeLibrary } from './graph/node-library';
import { TextureView } from './graph/texture-view';
import { LibraryActions } from './library/library-actions';
import { LibraryPane } from './library/library-pane';
import { LibrarySettingsDialog } from './library/library-settings-dialog';
import { LibraryTreeStore } from './library/library-tree-store';
import { MeshReloadDialog } from './library/mesh-reload-dialog';
import { PickFileDialog } from './library/pick-file-dialog';
import { ProjectCommands } from './library/project-commands';
import { ProjectSettingsDialog } from './library/project-settings-dialog';
import { ProjectStore } from './library/project-store';
import { BrushStore } from './paint/brush-store';
import { LayerStack } from './paint/layer-stack';
import { MaterialShelf } from './paint/material-shelf';
import { PaintAtlas } from './paint/paint-atlas';
import { PaintCommands } from './paint/paint-commands';
import { PaintPane } from './paint/paint-pane';
import { PaintStore } from './paint/paint-store';
import { TextureSets } from './paint/texture-sets';
import { ViewportPane } from './viewport/viewport-pane';

/** The app shell: a title bar, the docked panes and the status bar. `chrome-bar` and `readout` are bezel's. */
@Component({
  selector: 'ptn-root',
  imports: [
    AboutDialog,
    ActivityPanel,
    BakePane,
    ChecksPane,
    ExportPane,
    FrontGraph,
    GraphBeside,
    GraphPane,
    HistoryPane,
    PreferencesDialog,
    LibrarySettingsDialog,
    KeyHelper,
    SessionDialogs,
    ShortcutsDialog,
    ImageMaterialPane,
    Inspector,
    NodeLibrary,
    ProjectSettingsDialog,
    MeshReloadDialog,
    TextureSets,
    LayerStack,
    LibraryPane,
    DiagnosticsDialog,
    MaterialShelf,
    PaintAtlas,
    PaintPane,
    PickFileDialog,
    StartPane,
    TextureView,
    ViewportPane,
    XuiButtonImports,
    XuiCalloutImports,
    XuiDialog,
    XuiDialogBody,
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
              {{ tab.name }}
              @if (tab.dirty) {
                <span
                  class="text-foreground-muted"
                  title="Unsaved changes"
                  [attr.data-testid]="tab.path === project.active()?.path ? 'active-unsaved' : 'tab-unsaved'"
                  >&nbsp;•</span
                >
              }
            </button>
            <button
              type="button"
              xuiButton
              size="sm"
              variant="ghost"
              class="size-5 px-0"
              data-testid="document-tab-close"
              [attr.aria-label]="'Close ' + tab.name"
              [title]="'Close ' + tab.name"
              (click)="project.closeTab(tab.path)"
            >
              <svg viewBox="0 0 16 16" class="size-3" aria-hidden="true">
                <path d="M4 4l8 8M12 4l-8 8" stroke="currentColor" stroke-width="1.4" stroke-linecap="round" />
              </svg>
            </button>
          </div>
        }
      </div>
      <!-- Undo, Redo and Save are the app's, for whichever document is in front, so no pane has its own (#200). The
           title bar draws its few icons itself: ng-icon in the first bundle would cost it 11 kB. -->
      <div class="flex shrink-0 items-center" role="toolbar" aria-label="Document">
        <button
          type="button"
          xuiButton
          size="sm"
          variant="ghost"
          class="px-1.5"
          data-testid="app-undo"
          [attr.aria-label]="commands.label('undo')"
          [disabled]="!commands.enabled('undo')"
          [title]="commands.tooltip('undo')"
          (click)="commands.run('undo')"
        >
          <svg viewBox="0 0 16 16" class="size-4" aria-hidden="true">
            <path
              d="M5.5 3 2.5 6l3 3M2.5 6h7a4 4 0 0 1 0 8H7"
              stroke="currentColor"
              stroke-width="1.4"
              stroke-linecap="round"
              stroke-linejoin="round"
              fill="none"
            />
          </svg>
        </button>
        <button
          type="button"
          xuiButton
          size="sm"
          variant="ghost"
          class="px-1.5"
          data-testid="app-redo"
          [attr.aria-label]="commands.label('redo')"
          [disabled]="!commands.enabled('redo')"
          [title]="commands.tooltip('redo')"
          (click)="commands.run('redo')"
        >
          <svg viewBox="0 0 16 16" class="size-4" aria-hidden="true">
            <path
              d="M10.5 3l3 3-3 3M13.5 6h-7a4 4 0 0 0 0 8H9"
              stroke="currentColor"
              stroke-width="1.4"
              stroke-linecap="round"
              stroke-linejoin="round"
              fill="none"
            />
          </svg>
        </button>
        <button
          type="button"
          xuiButton
          size="sm"
          variant="ghost"
          class="px-1.5"
          aria-label="Save"
          data-testid="app-save"
          [disabled]="!commands.enabled('save') || !unsaved()"
          [title]="commands.tooltip('save')"
          (click)="commands.run('save')"
        >
          <svg viewBox="0 0 16 16" class="size-4" aria-hidden="true">
            <path
              d="M2.5 2.5h9l2 2v9h-11zM5 2.5v3.5h5V2.5M4.5 13.5V9h7v4.5"
              stroke="currentColor"
              stroke-width="1.2"
              stroke-linejoin="round"
              fill="none"
            />
          </svg>
        </button>
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
          {{ bakes.busy() ? 'Baking ' + bakePercent() : 'Quick Bake' }}
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
      <!--
        The panes that follow a graph show the one touched last, of the graph in front and a second beside it (#40):
        each is made again, with that graph's store, when the other is touched.
      -->
      <ng-template xuiDockContent="texture">
        @for (front of [graphs.front()]; track front) {
          <ng-container ptnFrontGraph>
            @defer (on immediate) {
              <ptn-texture-view />
            }
          </ng-container>
        }
      </ng-template>
      <ng-template xuiDockContent="graph">
        @defer (on immediate) {
          <ptn-graph />
        }
      </ng-template>
      <ng-template xuiDockContent="beside">
        @defer (on immediate) {
          <ptn-graph-beside />
        }
      </ng-template>
      <ng-template xuiDockContent="nodes">
        @for (front of [graphs.front()]; track front) {
          <ng-container ptnFrontGraph>
            @defer (on immediate) {
              <ptn-node-library />
            }
          </ng-container>
        }
      </ng-template>
      <ng-template xuiDockContent="inspector">
        @for (front of [graphs.front()]; track front) {
          <ng-container ptnFrontGraph>
            @defer (on immediate) {
              <ptn-inspector />
            }
          </ng-container>
        }
      </ng-template>
      <ng-template xuiDockContent="paint">
        @defer (on immediate) {
          <ptn-paint />
        }
      </ng-template>
      <ng-template xuiDockContent="history">
        @for (front of [graphs.front()]; track front) {
          <ng-container ptnFrontGraph>
            @defer (on immediate) {
              <ptn-history />
            }
          </ng-container>
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
      <xui-callout class="shrink-0 rounded-none" color="error" compact role="alert" data-testid="app-failure">
        <span class="flex items-center gap-2">
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
        </span>
      </xui-callout>
    }

    @if (paint.notice(); as notice) {
      <xui-callout class="shrink-0 rounded-none" color="warning" compact role="status" data-testid="paint-notice">
        <span class="flex items-center gap-2">
          <span class="min-w-0 flex-1 truncate" [title]="notice">{{ notice }}</span>
          <button type="button" xuiButton size="sm" variant="ghost" (click)="paint.notice.set(null)">Dismiss</button>
        </span>
      </xui-callout>
    }

    <footer class="chrome-bar flex h-7 shrink-0 items-center gap-4 px-3" role="status" aria-label="Engine status">
      @let current = status.state();
      @switch (current.kind) {
        @case ('connecting') {
          <span class="readout">Starting…</span>
        }
        @case ('online') {
          <span class="readout shrink-0">Patina {{ current.system.version }}</span>
          <span class="readout truncate" data-testid="status-library" [title]="current.system.library ?? ''">{{
            libraryName() ?? 'No library open'
          }}</span>
          <span class="readout shrink-0">GPU {{ current.system.worker.connected ? 'ready' : 'starting' }}</span>
          @if (project.notice(); as notice) {
            <span class="readout truncate" data-testid="app-notice">{{ notice }}</span>
          }
        }
        @case ('offline') {
          <span class="readout text-error truncate" data-testid="status-offline" [title]="current.detail ?? ''">{{
            current.reason
          }}</span>
        }
      }
      <!-- Every bake and export, and everything the app said, in one place (#192). -->
      <button
        type="button"
        class="readout hover:bg-hover ms-auto flex max-w-[50%] min-w-0 shrink-0 items-center gap-1.5 rounded px-1.5"
        data-testid="activity-readout"
        aria-haspopup="dialog"
        [attr.aria-expanded]="activity.open()"
        [title]="commands.tooltip('showActivity')"
        (click)="activity.toggle()"
      >
        @if (activity.readout(); as doing) {
          <span class="bg-primary size-2 shrink-0 animate-pulse rounded-full" aria-hidden="true"></span>
          <span class="truncate" data-testid="activity-doing">{{ doing }}</span>
        } @else {
          <span class="truncate">Activity</span>
        }
        @if (activity.unread() > 0) {
          <span
            class="bg-error text-error-foreground rounded-full px-1.5 text-xs"
            data-testid="activity-unread"
            [attr.aria-label]="activity.unread() + ' new'"
            >{{ activity.unread() }}</span
          >
        }
      </button>
    </footer>

    @defer (when activity.open()) {
      @if (activity.open()) {
        <ptn-activity />
      }
    }

    @defer (when baking()) {
      <xui-dialog class="max-w-5xl" size="lg" title="Bake" [(open)]="baking">
        <xui-dialog-body>
          <div class="flex h-[70vh] min-h-0 flex-col"><ptn-bake /></div>
        </xui-dialog-body>
      </xui-dialog>
    }
    @defer (when exporting()) {
      <xui-dialog class="max-w-5xl" size="lg" title="Export" [(open)]="exporting">
        <xui-dialog-body>
          <div class="flex h-[75vh] min-h-0 flex-col"><ptn-export /></div>
        </xui-dialog-body>
      </xui-dialog>
    }

    @defer (when asking()) {
      <ptn-session-dialogs />
    }

    @defer (when project.reloading() !== null) {
      <ptn-mesh-reload-dialog />
    }

    @defer (when diagnosing()) {
      <ptn-diagnostics [(open)]="diagnosing" />
    }

    @defer (when about()) {
      <ptn-about [(open)]="about" />
    }

    @defer (when picker.request()) {
      <ptn-pick-file />
    }

    @defer (when keys()) {
      <ptn-shortcuts [(open)]="keys" />
    }

    @defer (when helping()) {
      <ptn-key-helper [(open)]="helping" />
    }

    @defer (when preferencesOpen()) {
      <ptn-preferences [(open)]="preferencesOpen" />
    }

    @defer (when librarySettingsOpen()) {
      <ptn-library-settings [(open)]="librarySettingsOpen" />
    }
  `
})
export class App {
  protected readonly status = inject(SystemStatus);
  protected readonly project = inject(ProjectStore);
  protected readonly bakes = inject(BakeStore);
  protected readonly activity = inject(Activity);
  protected readonly bakeSettings = inject(BakeSettings);
  /** How far the jobs of the last bake are, together. */
  protected readonly bakePercent = computed(() => {
    const jobs = this.bakes.batch();
    return percent(jobs.length === 0 ? 0 : jobs.reduce((sum, job) => sum + job.progress, 0) / jobs.length);
  });
  private readonly edit = inject(EditCommands);
  private readonly options = inject(ViewOptions);
  protected readonly commands = inject(Commands);
  private readonly graph = inject(GraphStore);
  protected readonly graphs = inject(GraphViews);
  /** Whether Save has something to write: the document in front, or a graph beside it touched last (#40). */
  protected readonly unsaved = computed(() => {
    const front = this.graphs.front();
    return front !== this.graphs.main && front.document() !== null
      ? front.unsaved()
      : this.project.active()?.dirty === true;
  });
  private readonly destroyRef = inject(DestroyRef);
  protected readonly keys = signal(false);
  protected readonly helping = signal(false);
  protected readonly preferencesOpen = signal(false);
  protected readonly librarySettingsOpen = signal(false);
  protected readonly diagnosing = signal(false);
  protected readonly about = signal(false);
  protected readonly picker = inject(FilePicker);
  protected readonly paint = inject(PaintStore);
  /** The one thing that went wrong last, which stays until it is read. */
  protected readonly failure = computed(() => this.paint.failure() ?? this.project.failure());
  /** Whether the session asks a question, which first loads the dialogs that ask them (#301). */
  protected readonly asking = computed(
    () =>
      this.project.closing() !== null ||
      this.project.savingAs() ||
      this.project.recoverable() !== null ||
      this.project.quitting() ||
      this.project.closingProject() !== null ||
      this.paint.saveConflict()
  );
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
  /** The open library by its name, which the status bar shows with its whole path on hover (#200). */
  protected readonly libraryName = computed(() => {
    const state = this.status.state();
    return state.kind === 'online'
      ? (state.system.library
          ?.split(/[\\/]/)
          .pop()
          ?.replace(/\.ptlib$/, '') ?? null)
      : null;
  });
  /** What is open, which is also what the window says. */
  protected readonly title = computed(() => {
    const library = this.libraryName();
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

    this.hear();

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

  /** What each part of the app says goes to Activity too, so it can be read again once it has gone (#192). */
  private hear(): void {
    const activity = this.activity;
    activity.listen('Project', 'error', this.project.failure);
    activity.listen('Project', 'info', this.project.notice);
    activity.listen('Layers', 'error', this.paint.failure);
    activity.listen('Layers', 'warning', this.paint.notice);
    activity.listen('Graph', 'error', this.graph.problem);
    activity.listen('Graph', 'info', this.graph.notice);
    activity.heed('Autosave', this.paint.autosave);
    activity.heed('Autosave', this.graph.autosave);
    activity.listen('Bake', 'error', this.bakeSettings.failure);
    activity.listen('Brush', 'error', inject(BrushStore).failure);
    activity.listen('Library', 'error', inject(LibraryActions).failure);
    activity.listen('Library', 'error', inject(LibraryTreeStore).failure);
    activity.listen(
      'Engine',
      'error',
      computed(() => {
        const state = this.status.state();
        return state.kind === 'offline' ? state.reason : null;
      })
    );
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
    for (const [at, id] of ['layout1', 'layout2', 'layout3', 'layout4'].entries()) {
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
    handle('showActivity', { checked: () => this.activity.open(), run: () => this.activity.toggle() });
    handle('showLog', { run: () => this.activity.showLog() });
    handle('shortcuts', { run: () => this.keys.set(true) });
    handle('keyHelper', { run: () => this.helping.update(shown => !shown) });
    handle('preferences', { run: () => this.preferencesOpen.set(true) });
    handle('librarySettings', { run: () => this.librarySettingsOpen.set(true) });
    handle('diagnostics', { run: () => this.diagnosing.set(true) });
    handle('documentation', { run: () => globalThis.open(Links.documentation, '_blank') });
    handle('reportProblem', { run: () => globalThis.open(Links.issues, '_blank') });
    handle('about', { run: () => this.about.set(true) });
  }
}
