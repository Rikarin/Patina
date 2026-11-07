import { ChangeDetectionStrategy, Component, effect, model, signal, untracked } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { XuiDialogImports } from '@xui/dialog';
import { type SidecarStatus, desktop } from './desktop';

const Names: Readonly<Record<SidecarStatus['name'], string>> = { server: 'Library engine', worker: 'GPU engine' };

/** What is running behind the app, what it last said, and a way to start it again. */
@Component({
  selector: 'ptn-diagnostics',
  imports: [XuiButtonImports, XuiDialogImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <xui-dialog size="lg" title="Diagnostics" [(open)]="open">
      <xui-dialog-body>
        <div class="flex flex-col gap-3" data-testid="diagnostics">
          @for (part of parts(); track part.name) {
            <section class="flex flex-col gap-1">
              <h3 class="section-label">
                {{ nameOf(part) }}: {{ part.state }}{{ part.pid ? ' (' + part.pid + ')' : '' }}
              </h3>
              @if (part.detail) {
                <p class="text-xs">{{ part.detail }}</p>
              }
              <pre class="well max-h-48 overflow-auto p-2 text-xs select-text">{{
                logs()[part.name]?.join(
                  '
'
                )
              }}</pre>
            </section>
          }
        </div>
      </xui-dialog-body>
      <xui-dialog-footer>
        <button type="button" xuiButton data-testid="diagnostics-restart" [disabled]="restarting()" (click)="restart()">
          Restart Engine
        </button>
      </xui-dialog-footer>
    </xui-dialog>
  `
})
export class DiagnosticsDialog {
  readonly open = model(false);
  protected readonly parts = signal<readonly SidecarStatus[]>([]);
  protected readonly logs = signal<Partial<Record<SidecarStatus['name'], string[]>>>({});
  protected readonly restarting = signal(false);

  constructor() {
    effect(() => {
      if (this.open()) {
        untracked(() => void this.read());
      }
    });
  }

  protected nameOf(part: SidecarStatus): string {
    return Names[part.name];
  }

  protected async restart(): Promise<void> {
    this.restarting.set(true);
    try {
      await desktop()?.restartEngine();
      await this.read();
    } finally {
      this.restarting.set(false);
    }
  }

  private async read(): Promise<void> {
    const bridge = desktop();
    if (bridge) {
      this.parts.set(await bridge.sidecars());
      this.logs.set(await bridge.logs());
    }
  }
}
