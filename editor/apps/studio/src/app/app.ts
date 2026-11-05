import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { type XuiDockManagerLayout, XuiDockManagerImports } from '@xui/dock-manager';
import { BakePane } from './bake/bake-pane';
import { ExportPane } from './export/export-pane';
import { desktop } from './core/desktop';
import { SystemStatus } from './core/system-status';
import { Inspector } from './graph/graph-inspector';
import { GraphPane } from './graph/graph-pane';
import { TextureView } from './graph/texture-view';
import { LibraryPane } from './library/library-pane';
import { PaintPane } from './paint/paint-pane';
import { ViewportPane } from './viewport/viewport-pane';

/** The panes cannot be closed yet: nothing could bring one back until there is a Window menu. */
const fixedPane = { allowClose: false } as const;

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
          { ...fixedPane, type: 'contentPane', contentId: 'bake', header: 'Bake' },
          { ...fixedPane, type: 'contentPane', contentId: 'export', header: 'Export' },
          { ...fixedPane, type: 'contentPane', contentId: 'paint', header: 'Paint' }
        ]
      }
    ]
  }
};

/** The app shell: a title bar, the docked panes and the status bar. `chrome-bar` and `readout` are bezel's. */
@Component({
  selector: 'ptn-root',
  imports: [BakePane, ExportPane, GraphPane, Inspector, LibraryPane, PaintPane, TextureView, ViewportPane, XuiDockManagerImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full flex-col' },
  template: `
    <header class="chrome-bar window-drag flex h-10 shrink-0 items-center gap-2 px-3" [class.pl-20]="traffic()">
      <span class="font-semibold">Patina</span>
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
  `
})
export class App {
  protected readonly status = inject(SystemStatus);
  protected readonly layout = signal(initialLayout);
  private readonly fullScreen = signal(false);

  /** Room for macOS's traffic lights, which the desktop app keeps over the title bar and full screen hides. */
  protected readonly traffic = computed(() => desktop()?.platform === 'darwin' && !this.fullScreen());

  constructor() {
    desktop()?.onFullScreen(fullScreen => this.fullScreen.set(fullScreen));
  }
}
