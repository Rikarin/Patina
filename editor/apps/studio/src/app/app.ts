import { ChangeDetectionStrategy, Component, computed, effect, inject, linkedSignal, signal } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { XuiDialogImports } from '@xui/dialog';
import { type XuiDockManagerLayout, XuiDockManagerImports } from '@xui/dock-manager';
import { BakePane } from './bake/bake-pane';
import { BakeStore } from './bake/bake-store';
import { ExportPane } from './export/export-pane';
import { desktop } from './core/desktop';
import { SystemStatus } from './core/system-status';
import { Inspector } from './graph/graph-inspector';
import { GraphPane } from './graph/graph-pane';
import { ImageMaterialPane } from './graph/image-material-pane';
import { TextureView } from './graph/texture-view';
import { LibraryPane } from './library/library-pane';
import { ProjectStore } from './library/project-store';
import { keepLayout, layoutOf, showing } from './core/workspaces';
import { StartPane } from './core/start-pane';
import { MaterialShelf } from './paint/material-shelf';
import { PaintAtlas } from './paint/paint-atlas';
import { PaintPane } from './paint/paint-pane';
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
    LibraryPane,
    MaterialShelf,
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
  host: { class: 'flex h-full flex-col' },
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
            <button type="button" [attr.aria-label]="'Close ' + tab.name" data-testid="document-tab-close" (click)="project.closeTab(tab.path)">
              ×
            </button>
          </div>
        }
      </div>
      <div class="flex shrink-0 items-center gap-1">
        <button type="button" xuiButton size="sm" variant="ghost" data-testid="open-bake" (click)="baking.set(true)">Bake…</button>
        <button type="button" xuiButton size="sm" variant="ghost" data-testid="open-export" (click)="exporting.set(true)">Export…</button>
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
      <ng-template xuiDockContent="inspector">
        <ptn-inspector />
      </ng-template>
      <ng-template xuiDockContent="paint">
        <ptn-paint />
      </ng-template>
    </xui-dock-manager>

    <footer class="chrome-bar flex h-7 shrink-0 items-center gap-4 px-3" role="status" aria-label="Server status">
      @let current = status.state();
      @switch (current.kind) {
        @case ('connecting') {
          <span class="readout">Connecting to the Server…</span>
        }
        @case ('online') {
          <span class="readout shrink-0">Patina {{ current.system.version }}</span>
          <span class="readout truncate">{{ current.system.library ?? 'No library open' }}</span>
          <span class="readout shrink-0"
            >Worker {{ current.system.worker.connected ? 'connected' : 'not connected' }}</span
          >
        }
        @case ('offline') {
          <span class="readout text-error truncate">Server unreachable: {{ current.reason }}</span>
        }
      }
    </footer>

    <xui-dialog size="lg" title="Bake" [(open)]="baking">
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
        <button type="button" xuiButton variant="ghost" data-testid="project-discard" (click)="project.resolveClosing('discard')">
          Don't Save
        </button>
        <button type="button" xuiButton color="primary" data-testid="project-save" (click)="project.resolveClosing('save')">
          Save
        </button>
      </xui-dialog-footer>
    </xui-dialog>

    <xui-dialog
      size="sm"
      title="Quit Patina?"
      [open]="project.quitting()"
      (openChange)="$event || project.resolveQuitting('cancel')"
    >
      <xui-dialog-body data-testid="app-quitting">{{ project.unsavedTabs().length }} document(s) have unsaved changes.</xui-dialog-body>
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
  protected readonly baking = signal(false);
  protected readonly exporting = signal(false);
  /** The layout of what is in front. Each kind of document keeps its own (ADR 0012). */
  protected readonly layout = linkedSignal(() => layoutOf(this.project.workspace()));
  /** What is open, which is also what the window says. */
  protected readonly title = computed(() => {
    const state = this.status.state();
    const library = state.kind === 'online' ? state.system.library?.split(/[\\/]/).pop()?.replace(/\.ptlib$/, '') : null;
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

    const bridge = desktop();
    bridge?.onFullScreen(fullScreen => this.fullScreen.set(fullScreen));
    bridge?.onCommand(command => {
      if (command === 'save') {
        void this.project.saveActive();
      }
    });
    if (bridge) {
      effect(() => {
        bridge.setTitle(this.title());
      });
    }
  }

  protected keep(layout: XuiDockManagerLayout): void {
    keepLayout(this.project.workspace(), layout);
    this.layout.set(layout);
  }
}
