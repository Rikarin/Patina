import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { XuiDialogImports } from '@xui/dialog';
import { type XuiDockManagerLayout, XuiDockManagerImports } from '@xui/dock-manager';
import { BakePane } from './bake/bake-pane';
import { ExportPane } from './export/export-pane';
import { desktop } from './core/desktop';
import { SystemStatus } from './core/system-status';
import { Inspector } from './graph/graph-inspector';
import { GraphPane } from './graph/graph-pane';
import { TextureView } from './graph/texture-view';
import { LibraryPane } from './library/library-pane';
import { ProjectStore } from './library/project-store';
import { GraphStore } from './graph/graph-store';
import { PaintStore } from './paint/paint-store';
import { PaintPane } from './paint/paint-pane';
import { ViewportPane } from './viewport/viewport-pane';

/** The panes cannot be closed yet: nothing could bring one back until there is a Window menu. */
const fixedPane = { allowClose: false } as const;

/** Where Paint sits in its tab group, which opening a project brings to the front. */
const PaintTab = 1;

const initialLayout: XuiDockManagerLayout = {
  rootPane: {
    type: 'splitPane',
    orientation: 'horizontal',
    panes: [
      { ...fixedPane, type: 'contentPane', contentId: 'library', header: 'Library', size: 1 },
      {
        type: 'splitPane',
        orientation: 'vertical',
        size: 3,
        panes: [
          {
            type: 'splitPane',
            orientation: 'horizontal',
            size: 2,
            panes: [
              { ...fixedPane, type: 'contentPane', contentId: 'viewport', header: 'Viewport', size: 2 },
              { ...fixedPane, type: 'contentPane', contentId: 'texture', header: 'Texture', size: 1 }
            ]
          },
          { ...fixedPane, type: 'contentPane', contentId: 'graph', header: 'Graph', size: 3 }
        ]
      },
      {
        type: 'tabGroupPane',
        size: 1,
        panes: [
          { ...fixedPane, type: 'contentPane', contentId: 'inspector', header: 'Inspector' },
          { ...fixedPane, type: 'contentPane', contentId: 'paint', header: 'Paint' },
          { ...fixedPane, type: 'contentPane', contentId: 'bake', header: 'Bake' },
          { ...fixedPane, type: 'contentPane', contentId: 'export', header: 'Export' }
        ]
      }
    ]
  }
};

/** The app shell: a title bar, the docked panes and the status bar. `chrome-bar` and `readout` are bezel's. */
@Component({
  selector: 'ptn-root',
  imports: [
    BakePane,
    ExportPane,
    GraphPane,
    Inspector,
    LibraryPane,
    PaintPane,
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
      <span class="font-semibold" data-testid="app-title">{{ title() || 'Patina' }}</span>
    </header>

    <xui-dock-manager class="min-h-0 flex-1" [(layout)]="layout">
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
      <ng-template xuiDockContent="bake">
        <ptn-bake />
      </ng-template>
      <ng-template xuiDockContent="export">
        <ptn-export />
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

    <xui-dialog
      size="sm"
      title="Unsaved changes"
      [open]="project.leaving() !== null"
      (openChange)="$event || project.resolveLeaving('cancel')"
    >
      <xui-dialog-body data-testid="project-leaving">
        {{ project.current()?.name }} has paint that is not saved.
      </xui-dialog-body>
      <xui-dialog-footer>
        <button type="button" xuiButton variant="ghost" (click)="project.resolveLeaving('cancel')">Cancel</button>
        <button type="button" xuiButton variant="ghost" data-testid="project-discard" (click)="project.resolveLeaving('discard')">
          Don't Save
        </button>
        <button type="button" xuiButton color="primary" data-testid="project-save" (click)="project.resolveLeaving('save')">
          Save
        </button>
      </xui-dialog-footer>
    </xui-dialog>
  `
})
export class App {
  protected readonly status = inject(SystemStatus);
  protected readonly project = inject(ProjectStore);
  private readonly paint = inject(PaintStore);
  private readonly graph = inject(GraphStore);
  protected readonly layout = signal(initialLayout);
  /** What is open, which is also what the window says. */
  protected readonly title = computed(() => {
    const state = this.status.state();
    const library = state.kind === 'online' ? state.system.library?.split(/[\\/]/).pop()?.replace(/\.ptlib$/, '') : null;
    const name = this.project.current()?.name;
    const dirty = this.paint.state()?.history.dirty ? ' •' : '';
    return [name ? `${name}${dirty}` : null, library].filter(Boolean).join(' — ');
  });
  private readonly fullScreen = signal(false);

  /** Room for macOS's traffic lights, which the desktop app keeps over the title bar and full screen hides. */
  protected readonly traffic = computed(() => desktop()?.platform === 'darwin' && !this.fullScreen());

  constructor() {
    const bridge = desktop();
    bridge?.onFullScreen(fullScreen => this.fullScreen.set(fullScreen));
    bridge?.onCommand(command => {
      if (command === 'save') {
        void (this.paint.state()?.history.dirty ? this.paint.act('save') : this.graph.save());
      }
    });
    if (bridge) {
      effect(() => {
        bridge.setTitle(this.title());
      });
    }

    // Opening a project is asking to paint it.
    let seen = 0;
    effect(() => {
      const opened = this.project.opened();
      if (opened !== seen) {
        seen = opened;
        untracked(() => this.showPaint());
      }
    });
  }

  private showPaint(): void {
    const layout = this.layout();
    const root = layout.rootPane;
    if (root.type !== 'splitPane') {
      return;
    }

    this.layout.set({
      ...layout,
      rootPane: {
        ...root,
        panes: root.panes.map(pane => (pane.type === 'tabGroupPane' ? { ...pane, selectedIndex: PaintTab } : pane))
      }
    });
  }
}
