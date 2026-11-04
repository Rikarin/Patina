import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { type XuiDockManagerLayout, XuiDockManagerImports } from '@xui/dock-manager';
import { desktop } from './core/desktop';
import { SystemStatus } from './core/system-status';

/** The panes cannot be closed yet: nothing could bring one back until there is a Window menu. */
const fixedPane = { allowClose: false } as const;

const initialLayout: XuiDockManagerLayout = {
  rootPane: {
    type: 'splitPane',
    orientation: 'horizontal',
    panes: [
      { ...fixedPane, type: 'contentPane', contentId: 'library', header: 'Library', size: 1 },
      { ...fixedPane, type: 'contentPane', contentId: 'viewport', header: 'Viewport', size: 3 },
      { ...fixedPane, type: 'contentPane', contentId: 'inspector', header: 'Inspector', size: 1 }
    ]
  }
};

/** The app shell: a title bar, the docked panes and the status bar. `chrome-bar` and `readout` are bezel's. */
@Component({
  selector: 'ptn-root',
  imports: [XuiDockManagerImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full flex-col' },
  template: `
    <header class="chrome-bar window-drag flex h-10 shrink-0 items-center gap-2 px-3" [class.pl-20]="traffic()">
      <span class="font-semibold">Patina</span>
    </header>

    <xui-dock-manager class="min-h-0 flex-1" [(layout)]="layout">
      <ng-template xuiDockContent="library">
        <p class="text-foreground-muted p-3">No projects yet.</p>
      </ng-template>
      <ng-template xuiDockContent="viewport">
        <p class="text-foreground-muted p-3">The viewport arrives with the Worker's renderer.</p>
      </ng-template>
      <ng-template xuiDockContent="inspector">
        <p class="text-foreground-muted p-3">Nothing selected.</p>
      </ng-template>
    </xui-dock-manager>

    <footer class="chrome-bar flex h-7 shrink-0 items-center gap-4 px-3" role="status" aria-label="Server status">
      @let current = status.state();
      @switch (current.kind) {
        @case ('connecting') {
          <span class="readout">Connecting to the Server…</span>
        }
        @case ('online') {
          <span class="readout">Patina {{ current.system.version }}</span>
          <span class="readout truncate">{{ current.system.library ?? 'No library open' }}</span>
          <span class="readout">Worker {{ current.system.worker.connected ? 'connected' : 'not connected' }}</span>
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
