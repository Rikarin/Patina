import { ChangeDetectionStrategy, Component, computed, inject, model } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { XuiDialogImports } from '@xui/dialog';
import { Links } from './help-links';
import { SystemStatus } from './system-status';

/** What a version is called beside its number, read from the browser the app runs in, when it says. */
function versionIn(agent: string, product: string): string | null {
  return new RegExp(`${product}/([\\d.]+)`, 'u').exec(agent)?.[1] ?? null;
}

/**
 * Help > About Patina: which Patina this is and what runs it, the licences it and its parts come under, and the plain
 * statement that what a person makes with it is theirs (plan §11): studios ask.
 */
@Component({
  selector: 'ptn-about',
  imports: [XuiButtonImports, XuiDialogImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <xui-dialog size="md" title="About Patina" [(open)]="open">
      <xui-dialog-body>
        <div class="flex flex-col gap-4 text-sm" data-testid="about">
          <p class="text-foreground font-semibold" data-testid="about-ownership">
            Your work is yours. Libraries, projects and the textures you export belong to you, and Patina’s licence does
            not reach them.
          </p>
          <dl class="grid grid-cols-[8rem_minmax(0,1fr)] gap-x-3 gap-y-1">
            @for (line of versions(); track line.name) {
              <dt class="text-foreground-muted">{{ line.name }}</dt>
              <dd class="readout" [attr.data-testid]="'about-version-' + line.key">{{ line.value }}</dd>
            }
          </dl>
          <section class="flex flex-col gap-1">
            <h3 class="section-label">Licences</h3>
            <p>Patina is free software under the GNU General Public License, version 3 or later.</p>
            <p class="text-foreground-muted">
              The graph, paint and bake engine beneath it, Vixen, and its controls, xUI, are under the Apache License
              2.0. The starter materials, brushes and nodes are CC0, so textures made from them can ship in anything.
            </p>
          </section>
        </div>
      </xui-dialog-body>
      <xui-dialog-footer>
        <a xuiButton variant="ghost" target="_blank" rel="noopener" data-testid="about-licence" [href]="links.licence">
          Licence
        </a>
        <a xuiButton variant="ghost" target="_blank" rel="noopener" [href]="links.notices">Third-Party Notices</a>
        <button type="button" xuiButton color="primary" (click)="open.set(false)">Close</button>
      </xui-dialog-footer>
    </xui-dialog>
  `
})
export class AboutDialog {
  private readonly status = inject(SystemStatus);
  readonly open = model(false);
  protected readonly links = Links;

  /** The versions there are to say: the app's always, the engine's while it answers, and what it runs on. */
  protected readonly versions = computed(() => {
    const state = this.status.state();
    const system = state.kind === 'online' ? state.system : null;
    const agent = globalThis.navigator.userAgent;
    const lines: { key: string; name: string; value: string | null | undefined }[] = [
      { key: 'patina', name: 'Patina', value: system?.version },
      { key: 'gpu', name: 'GPU engine', value: system?.worker.version },
      { key: 'vixen', name: 'Vixen', value: system?.vixen },
      { key: 'api', name: 'API', value: system ? String(system.apiVersion) : null },
      { key: 'runtime', name: 'Runtime', value: system?.host.runtime },
      { key: 'system', name: 'System', value: system ? `${system.host.os}, ${system.host.architecture}` : null },
      { key: 'electron', name: 'Electron', value: versionIn(agent, 'Electron') },
      { key: 'chromium', name: 'Chromium', value: versionIn(agent, 'Chrome') }
    ];
    return lines.flatMap(line => (line.value ? [{ ...line, value: line.value }] : []));
  });
}
