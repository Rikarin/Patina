import { ChangeDetectionStrategy, Component, computed, effect, inject, model, signal, untracked } from '@angular/core';
import { type components, unwrap } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiDialogImports } from '@xui/dialog';
import { XuiNumericInputImports } from '@xui/numeric-input';
import { Activity } from '../core/activity';
import { messageOf } from '../core/messages';
import { bytes, count, figure } from '../core/numbers';
import { LibraryStore } from './library-store';

type LibraryHealth = components['schemas']['LibraryHealth'];
type CacheInfo = components['schemas']['CacheInfo'];

/** What each kind of finding is called, as `patina library check` names them in its report. */
const FindingWords: Readonly<Record<string, string>> = {
  Unreadable: 'Files that do not read',
  NewerSchema: 'Files from a newer Patina',
  SchemaDrift: 'Files at an older format',
  DuplicateId: 'Projects or materials with one ID',
  MissingReference: 'Missing materials',
  MissingRevision: 'Missing pinned versions',
  MissingAssetFile: 'Materials whose file has gone',
  UnidentifiedFile: 'Shared files with no description',
  DependencyCycle: 'Materials that use each other',
  MissingInput: 'Missing inputs',
  StaleOutput: 'Outputs to build again',
  UnresolvedOutput: 'Outputs that cannot build',
  UnusedRevision: 'Kept versions nothing uses'
};

/** How long a built object may go unused before Prune takes it, by default, as `patina cache prune` does. */
const PruneDays = 30;

/**
 * Library Settings (#192): the open library's settings, which no other view shows, and the care it takes. Check Library
 * reads every project and material and says what is wrong, as `patina library check` does; Prune takes built objects
 * nothing has used for a while out of the cache; Clean takes away the copies of paint a new UV layout moved (#162).
 * Each runs in the Server, which holds the library, and says what it did in Activity too.
 */
@Component({
  selector: 'ptn-library-settings',
  imports: [XuiButtonImports, XuiCalloutImports, XuiDialogImports, XuiNumericInputImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <xui-dialog size="md" title="Library Settings" [(open)]="open">
      <xui-dialog-body>
        <div class="flex flex-col gap-4 text-sm" data-testid="library-settings">
          @if (info(); as library) {
            <section class="flex flex-col gap-2">
              <h3 class="section-label">Library</h3>
              <dl class="grid grid-cols-[9rem_minmax(0,1fr)] gap-x-3 gap-y-1">
                <dt class="text-foreground-muted">Name</dt>
                <dd data-testid="library-settings-name">{{ library.name }}</dd>
                <dt class="text-foreground-muted">Folder</dt>
                <dd class="truncate select-text" [title]="library.path">{{ library.path }}</dd>
                <dt class="text-foreground-muted">Projects</dt>
                <dd class="readout">{{ count(library.projectCount) }}</dd>
                <dt class="text-foreground-muted">Materials</dt>
                <dd class="readout">{{ count(library.assetCount) }}</dd>
                <dt class="text-foreground-muted">Texel density</dt>
                <dd class="readout" data-testid="library-settings-density">
                  {{ figure(library.texelDensity) }} texels per metre
                </dd>
                <dt class="text-foreground-muted">Channels</dt>
                <dd data-testid="library-settings-channels">{{ listed(library.channels) }}</dd>
                <dt class="text-foreground-muted">Export presets</dt>
                <dd data-testid="library-settings-targets">{{ listed(library.exportTargets) }}</dd>
                <dt class="text-foreground-muted">Shader</dt>
                <dd>{{ library.defaultShader }}</dd>
              </dl>
              <p class="text-foreground-muted text-xs">
                New projects start from these. They are kept in the library’s library.yaml.
              </p>
            </section>

            <section class="flex flex-col gap-2" data-testid="library-settings-check">
              <h3 class="section-label">Check</h3>
              <div class="flex items-center gap-3">
                <p class="text-foreground-muted min-w-0 flex-1 text-xs">
                  Reads every project and material and says what is wrong or out of date. It changes nothing.
                </p>
                <button
                  type="button"
                  xuiButton
                  size="sm"
                  data-testid="library-settings-check-run"
                  [disabled]="busy() !== null"
                  (click)="check()"
                >
                  {{ busy() === 'check' ? 'Checking…' : 'Check Library' }}
                </button>
              </div>
              @if (health(); as found) {
                <p
                  [class]="found.errors > 0 ? 'text-error' : 'text-foreground-muted'"
                  data-testid="library-settings-check-summary"
                >
                  {{ summary(found) }}
                </p>
                @if (found.groups.length > 0) {
                  <div class="well flex max-h-56 flex-col gap-2 overflow-y-auto p-2 text-xs">
                    @for (group of found.groups; track group.kind) {
                      <div class="flex flex-col gap-0.5" [attr.data-testid]="'library-settings-finding-' + group.kind">
                        <span [class]="group.isError ? 'text-error font-semibold' : 'font-semibold'">
                          {{ kindName(group.kind) }}, {{ count(group.count) }}
                        </span>
                        @for (finding of group.findings; track $index) {
                          <span class="text-foreground-muted truncate select-text" [title]="finding.detail">
                            {{ finding.path }}: {{ finding.detail }}
                          </span>
                        }
                        @if (group.count > group.findings.length) {
                          <span class="text-foreground-muted">
                            and {{ count(group.count - group.findings.length) }} more
                          </span>
                        }
                      </div>
                    }
                  </div>
                }
              }
            </section>

            <section class="flex flex-col gap-2" data-testid="library-settings-tidy">
              <h3 class="section-label">Tidy</h3>
              <div class="flex items-center gap-3">
                <span class="min-w-0 flex-1">
                  Cache
                  @if (cache(); as held) {
                    <span class="readout" data-testid="library-settings-cache-size">{{ bytes(held.bytes) }}</span>
                  }
                </span>
                <span class="text-foreground-muted text-xs">unused for</span>
                <xui-numeric-input
                  class="w-20"
                  size="sm"
                  aria-label="Days unused"
                  data-testid="library-settings-prune-days"
                  [min]="0"
                  [stepSize]="1"
                  [value]="days()"
                  (valueChange)="days.set(Math.max(0, Math.round($event ?? 0)))"
                />
                <span class="text-foreground-muted text-xs">days</span>
                <button
                  type="button"
                  xuiButton
                  size="sm"
                  data-testid="library-settings-prune"
                  [disabled]="busy() !== null"
                  [title]="'Removes built objects, bakes and reports nothing has used for this long'"
                  (click)="prune()"
                >
                  {{ busy() === 'prune' ? 'Pruning…' : 'Prune' }}
                </button>
              </div>
              <div class="flex items-center gap-3">
                <span class="text-foreground-muted min-w-0 flex-1 text-xs">
                  Each project keeps a copy of its paint whenever a new UV layout moves it. Clean removes those copies.
                </span>
                <button
                  type="button"
                  xuiButton
                  size="sm"
                  data-testid="library-settings-clean"
                  [disabled]="busy() !== null"
                  (click)="clean()"
                >
                  {{ busy() === 'clean' ? 'Cleaning…' : 'Clean' }}
                </button>
              </div>
              @if (tidied(); as said) {
                <p class="text-foreground-muted text-xs" data-testid="library-settings-tidied">{{ said }}</p>
              }
            </section>
          } @else {
            <p class="text-foreground-muted" data-testid="library-settings-none">
              No library is open. Open one, or make a new one, from the start page.
            </p>
          }

          @if (failure(); as reason) {
            <xui-callout color="error" compact data-testid="library-settings-failure">{{ reason }}</xui-callout>
          }
        </div>
      </xui-dialog-body>
      <xui-dialog-footer>
        <button type="button" xuiButton color="primary" data-testid="library-settings-done" (click)="open.set(false)">
          Done
        </button>
      </xui-dialog-footer>
    </xui-dialog>
  `
})
export class LibrarySettingsDialog {
  readonly open = model(false);
  private readonly library = inject(LibraryStore);
  private readonly activity = inject(Activity);

  protected readonly info = computed(() => {
    const state = this.library.state();
    return state.kind === 'ready' ? state.info : null;
  });
  protected readonly health = signal<LibraryHealth | null>(null);
  protected readonly cache = signal<CacheInfo | null>(null);
  protected readonly days = signal(PruneDays);
  protected readonly busy = signal<'check' | 'prune' | 'clean' | null>(null);
  protected readonly tidied = signal<string | null>(null);
  protected readonly failure = signal<string | null>(null);

  protected readonly count = count;
  protected readonly bytes = bytes;
  protected readonly figure = figure;
  protected readonly Math = Math;

  constructor() {
    effect(() => {
      if (this.open() && this.info()) {
        untracked(() => void this.readCache());
      }
    });
  }

  protected listed(names: readonly string[]): string {
    return names.length === 0 ? 'None' : names.join(', ');
  }

  protected kindName(kind: string): string {
    return FindingWords[kind] ?? kind;
  }

  protected summary(found: LibraryHealth): string {
    const checked = `${count(found.projects)} projects, ${count(found.assets)} materials and ${count(found.outputs)} outputs`;
    if (found.groups.length === 0) {
      return `Checked ${checked}: nothing to say.`;
    }

    return found.errors > 0
      ? `Checked ${checked}: ${count(found.errors)} problems to put right.`
      : `Checked ${checked}: nothing is wrong, and there is some tidying to do.`;
  }

  /** Reads every project and material, and says what it found. */
  async check(): Promise<void> {
    await this.run('check', async client => {
      const found = unwrap(await client.POST('/api/v1/library/check'));
      this.health.set(found);
      this.activity.report({
        tone: found.errors > 0 ? 'warning' : 'done',
        source: 'Library',
        text: this.summary(found)
      });
    });
  }

  /** Takes out of the cache what nothing has used for the days chosen. */
  async prune(): Promise<void> {
    await this.run('prune', async client => {
      const done = unwrap(await client.POST('/api/v1/library/cache/prune', { body: { days: this.days() } }));
      this.say(`Pruned ${count(done.removed)} cache entries, ${bytes(done.bytes)}.`);
      await this.readCache();
    });
  }

  /** Takes away the copies of moved paint. */
  async clean(): Promise<void> {
    await this.run('clean', async client => {
      const done = unwrap(await client.POST('/api/v1/library/clean'));
      this.say(
        done.removed === 0
          ? 'There were no copies of moved paint to remove.'
          : `Removed ${count(done.removed)} copies of moved paint, ${bytes(done.bytes)}.`
      );
    });
  }

  private say(text: string): void {
    this.tidied.set(text);
    this.activity.report({ tone: 'done', source: 'Library', text });
  }

  private async run(
    what: 'check' | 'prune' | 'clean',
    act: (client: Awaited<ReturnType<LibraryStore['api']>>) => Promise<void>
  ): Promise<void> {
    this.busy.set(what);
    this.failure.set(null);
    try {
      await act(await this.library.api());
    } catch (failure) {
      this.failure.set(messageOf(failure));
    } finally {
      this.busy.set(null);
    }
  }

  private async readCache(): Promise<void> {
    try {
      const client = await this.library.api();
      this.cache.set(unwrap(await client.GET('/api/v1/library/cache')));
    } catch {
      this.cache.set(null); // the size is a courtesy; Prune still says what it freed
    }
  }
}
