import { ChangeDetectionStrategy, Component, computed, effect, inject, model, signal, untracked } from '@angular/core';
import { type components, unwrap } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiCheckboxImports } from '@xui/checkbox';
import { XuiDialogImports } from '@xui/dialog';
import { XuiInputImports } from '@xui/input';
import { XuiNumericInputImports } from '@xui/numeric-input';
import { Activity } from '../core/activity';
import { messageOf } from '../core/messages';
import { bytes, count } from '../core/numbers';
import { HeldFiles } from './held-files';
import { LibraryStore } from './library-store';

type LibraryHealth = components['schemas']['LibraryHealth'];
type CacheInfo = components['schemas']['CacheInfo'];
type ExportPresetInfo = components['schemas']['ExportPresetInfo'];
type UnusedUploadList = components['schemas']['UnusedUploadList'];

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

/** What the dialog may be busy with. */
type Work = 'check' | 'purge' | 'prune' | 'clean' | 'uploads' | 'settings';

/** How long a built object may go unused before Prune takes it, by default, as `patina cache prune` does. */
const PruneDays = 30;

/**
 * Library Settings (#192): the open library's settings, which no other view shows, and the care it takes. Check Library
 * reads every project and material and says what is wrong, as `patina library check` does, and removes the kept
 * versions it found nothing uses, as `--purge-revisions` does; Prune takes built objects
 * nothing has used for a while out of the cache; Clean takes away the copies of paint a new UV layout moved (#162);
 * Clear Uploads moves the files sent from another computer that nothing uses any more to the bin (#297). Each runs in
 * the Server, which holds the library, and says what it did in Activity too.
 */
@Component({
  selector: 'ptn-library-settings',
  imports: [
    XuiButtonImports,
    XuiCalloutImports,
    XuiCheckboxImports,
    XuiDialogImports,
    XuiInputImports,
    XuiNumericInputImports
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <xui-dialog size="md" title="Library Settings" [(open)]="open">
      <xui-dialog-body>
        <div class="flex flex-col gap-4 text-sm" data-testid="library-settings">
          @if (info(); as library) {
            <section class="flex flex-col gap-2">
              <h3 class="section-label">Library</h3>
              <dl class="grid grid-cols-[9rem_minmax(0,1fr)] items-center gap-x-3 gap-y-1">
                <dt class="text-foreground-muted">Name</dt>
                <dd>
                  <input
                    class="w-full"
                    xuiInput
                    size="sm"
                    autocomplete="off"
                    aria-label="Library name"
                    data-testid="library-settings-name"
                    [value]="library.name"
                    (change)="rename($event)"
                  />
                </dd>
                <dt class="text-foreground-muted">Folder</dt>
                <dd class="truncate select-text" [title]="library.path">{{ library.path }}</dd>
                <dt class="text-foreground-muted">Projects</dt>
                <dd class="readout">{{ count(library.projectCount) }}</dd>
                <dt class="text-foreground-muted">Materials</dt>
                <dd class="readout">{{ count(library.assetCount) }}</dd>
                <dt class="text-foreground-muted">Texel density</dt>
                <dd class="flex items-center gap-2">
                  <xui-numeric-input
                    class="w-28"
                    size="sm"
                    aria-label="Texel density in texels per metre"
                    data-testid="library-settings-density"
                    [min]="1"
                    [stepSize]="64"
                    [value]="library.texelDensity"
                    (valueChange)="keep(library.name, $event ?? library.texelDensity)"
                  />
                  <span class="text-foreground-muted text-xs">texels per metre</span>
                </dd>
                <dt class="text-foreground-muted">Channels</dt>
                <dd data-testid="library-settings-channels">{{ listed(library.channels) }}</dd>
                <dt class="text-foreground-muted">Shader</dt>
                <dd>{{ library.defaultShader }}</dd>
              </dl>
              <p class="text-foreground-muted text-xs">
                New projects start from these. They are kept in the library’s library.yaml, and its folder keeps its
                name.
              </p>
              <h3 class="section-label pt-1">Build for</h3>
              <div class="flex flex-col gap-1" data-testid="library-settings-targets">
                @for (preset of presets(); track preset.id) {
                  <xui-checkbox
                    data-testid="library-settings-target"
                    [attr.data-preset]="preset.id"
                    [checked]="library.exportTargets.includes(preset.id)"
                    (checkedChange)="target(preset.id, $event)"
                  >
                    {{ preset.name }}
                  </xui-checkbox>
                } @empty {
                  <span class="text-foreground-muted text-xs">{{ listed(library.exportTargets) }}</span>
                }
              </div>
              <p class="text-foreground-muted text-xs">
                The presets a build of the whole library writes when it names none, such as one for each platform the
                game ships to.
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
                        @if (group.kind === 'UnusedRevision') {
                          <button
                            type="button"
                            xuiButton
                            size="sm"
                            class="self-start"
                            data-testid="library-settings-purge-revisions"
                            [disabled]="busy() !== null"
                            [title]="'Removes the kept versions no snapshot and no pin uses'"
                            (click)="purgeRevisions()"
                          >
                            {{ busy() === 'purge' ? 'Removing…' : 'Remove Them' }}
                          </button>
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
                  Each project keeps a copy of its paint whenever a new UV layout moves it. Clean removes those copies,
                  and moves the pictures no document uses, saved or open, to the bin.
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
              <div class="flex items-center gap-3" data-testid="library-settings-uploads">
                <span class="text-foreground-muted min-w-0 flex-1 text-xs">
                  Files sent from another computer are kept in the library’s uploads.
                  @if (uploads(); as unused) {
                    @if (unused.files.length === 0) {
                      Every one is in use.
                    } @else {
                      <span data-testid="library-settings-uploads-unused">
                        {{ count(unused.files.length) }} nothing uses any more,
                        <span class="readout">{{ bytes(unused.bytes) }}</span
                        >.
                      </span>
                    }
                  }
                  Clear Uploads moves those nothing uses to the bin.
                </span>
                <button
                  type="button"
                  xuiButton
                  size="sm"
                  data-testid="library-settings-clear-uploads"
                  [disabled]="busy() !== null || (uploads()?.files?.length ?? 0) === 0"
                  (click)="clearUploads()"
                >
                  {{ busy() === 'uploads' ? 'Clearing…' : 'Clear Uploads' }}
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
  private readonly held = inject(HeldFiles);

  protected readonly info = computed(() => {
    const state = this.library.state();
    return state.kind === 'ready' ? state.info : null;
  });
  protected readonly health = signal<LibraryHealth | null>(null);
  protected readonly cache = signal<CacheInfo | null>(null);
  protected readonly days = signal(PruneDays);
  protected readonly busy = signal<Work | null>(null);
  protected readonly uploads = signal<UnusedUploadList | null>(null);
  protected readonly presets = signal<readonly ExportPresetInfo[]>([]);
  protected readonly tidied = signal<string | null>(null);
  protected readonly failure = signal<string | null>(null);

  protected readonly count = count;
  protected readonly bytes = bytes;
  protected readonly Math = Math;

  constructor() {
    effect(() => {
      if (this.open() && this.info()) {
        untracked(() => {
          void this.readCache();
          void this.readPresets();
          void this.readUploads();
        });
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

  /** Removes the kept versions no snapshot and no pin uses, then checks again so the list says so (#292). */
  async purgeRevisions(): Promise<void> {
    await this.run('purge', async client => {
      const done = unwrap(await client.POST('/api/v1/library/revisions/purge'));
      this.say(`Removed ${count(done.removed)} kept versions nothing used, ${bytes(done.bytes)}.`);
      this.health.set(unwrap(await client.POST('/api/v1/library/check')));
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

  /** Takes away the copies of moved paint, and moves the pictures nothing uses to the bin (#162). */
  async clean(): Promise<void> {
    await this.run('clean', async client => {
      const done = unwrap(await client.POST('/api/v1/library/clean'));
      const said = [
        done.removed === 0 ? '' : `Removed ${count(done.removed)} copies of moved paint, ${bytes(done.bytes)}.`,
        done.pictures === 0
          ? ''
          : `Moved ${count(done.pictures)} pictures nothing uses to the bin, ${bytes(done.pictureBytes)}.`
      ].filter(part => part.length > 0);
      this.say(said.length === 0 ? 'There was nothing to clean.' : said.join(' '));
    });
  }

  /** Moves the files sent from another computer that nothing uses any more to the bin (#297). */
  async clearUploads(): Promise<void> {
    // What is held now, as Clear was pressed: a dialog may write it while the Server is asked.
    const held = this.held.all();
    await this.run('uploads', async client => {
      const done = unwrap(await client.POST('/api/v1/library/uploads/clear', { body: { held } }));
      this.say(
        done.count === 0
          ? 'Every upload is in use.'
          : `Moved ${count(done.count)} uploads nothing uses to the bin, ${bytes(done.bytes)}.`
      );
      await this.readUploads();
    });
  }

  /** Renames the library, as its file has it: the folder keeps its name. */
  protected rename(event: Event): void {
    const name = (event.target as HTMLInputElement).value.trim();
    const library = this.info();
    if (library && name.length > 0 && name !== library.name) {
      void this.keep(name, library.texelDensity);
    } else if (library) {
      (event.target as HTMLInputElement).value = library.name;
    }
  }

  /** Keeps the library's name and texel density in its library.yaml (#192). */
  async keep(name: string, texelDensity: number): Promise<void> {
    const library = this.info();
    if (!library || (name === library.name && texelDensity === library.texelDensity)) {
      return;
    }

    await this.run('settings', async client => {
      this.library.settled(unwrap(await client.PUT('/api/v1/library/settings', { body: { name, texelDensity } })));
    });
  }

  /** Adds a preset to what a build of the whole library writes, or takes it out. */
  async target(preset: string, on: boolean): Promise<void> {
    const library = this.info();
    if (!library) {
      return;
    }

    const kept = library.exportTargets.filter(id => id !== preset);
    const presets = on ? [...kept, preset] : kept;
    await this.run('settings', async client => {
      this.library.settled(unwrap(await client.PUT('/api/v1/library/export/targets', { body: { presets } })));
    });
  }

  private say(text: string): void {
    this.tidied.set(text);
    this.activity.report({ tone: 'done', source: 'Library', text });
  }

  private async run(
    what: Work,
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

  private async readPresets(): Promise<void> {
    try {
      const client = await this.library.api();
      this.presets.set(unwrap(await client.GET('/api/v1/export/presets', { params: { query: {} } })).presets);
    } catch {
      this.presets.set([]); // the targets are still listed by id
    }
  }

  private async readUploads(): Promise<void> {
    try {
      const held = this.held.all();
      const client = await this.library.api();
      this.uploads.set(unwrap(await client.GET('/api/v1/library/uploads/unused', { params: { query: { held } } })));
    } catch {
      this.uploads.set(null); // Clear Uploads still says what it moved
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
