import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import type {
  CheckFile,
  CheckProblem,
  ExportPresetInfo,
  PlatformCheckLimits,
  ProjectCheckSettings,
  ProjectChecks,
  StackChecks
} from '@patina/api-client';
import { unwrap } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiInputImports } from '@xui/input';
import { XuiNonIdealStateImports } from '@xui/non-ideal-state';
import { XuiSegmentedControlImports } from '@xui/segmented-control';
import { XuiSelectImports } from '@xui/select';
import { Commands } from '../core/commands';
import { checkWords, messageOf } from '../core/messages';
import { bytes, count } from '../core/numbers';
import { LibraryStore } from '../library/library-store';
import { ProjectStore } from '../library/project-store';
import { PaintStore } from '../paint/paint-store';
import { ViewportJump } from '../viewport/viewport-jump';

/** A project that has said nothing holds its sets to nothing but the library's density. */
const NoLimits: ProjectCheckSettings = {
  memoryBudget: 0,
  maxSize: 0,
  texelDensity: 0,
  densityTolerance: 0.25,
  platforms: []
};

/** The platform choice that is the project's own preset and limits, which no preset id can be. */
const Own = '*project';

/** What a check covers: the set in front, or every set of the project. */
type Scope = 'set' | 'all';

const Scopes: readonly { value: Scope; label: string }[] = [
  { value: 'set', label: 'This set' },
  { value: 'all', label: 'All sets' }
];

/** One field of the project's checks, as the pane edits it. */
interface Limit {
  readonly key: 'memoryBudget' | 'maxSize' | 'texelDensity' | 'densityTolerance';
  readonly label: string;
  readonly title: string;
  /** What the field shows a value as, and what the project keeps from what is typed. */
  readonly scale: number;
  /** Whether a platform keeps one of its own. */
  readonly perPlatform: boolean;
}

const Limits: readonly Limit[] = [
  {
    key: 'memoryBudget',
    label: 'Budget (MB)',
    title: 'The most GPU memory one texture set’s exported files may take, or 0 for no limit',
    scale: 1,
    perPlatform: true
  },
  {
    key: 'maxSize',
    label: 'Largest side',
    title: 'The largest side a texture set’s maps may have, in texels, or 0 for no limit',
    scale: 1,
    perPlatform: true
  },
  {
    key: 'texelDensity',
    label: 'Texel density',
    title: 'Texels per unit of the model (a metre in glTF) every set is held to, or 0 for the library’s',
    scale: 1,
    perPlatform: true
  },
  {
    key: 'densityTolerance',
    label: 'Tolerance (%)',
    title: 'How far a set’s density may stray from it before it is flagged',
    scale: 100,
    perPlatform: false
  }
];

/**
 * What a texture set would get wrong in an export (#159), as the Server finds it: budgets and limits the project sets,
 * the maps its preset reads, its texel density, its UV layout, and values no physical material has. It checks the set
 * in front or every set of the project at once, for the project's own preset or for one platform it ships to, whose
 * limits the project keeps beside its own. Each problem goes to where it is: the view that shows it, and on the mesh a
 * camera that looks at it. Checking reads every map once, so it runs when asked and says when an edit has left it
 * behind.
 */
@Component({
  selector: 'ptn-checks',
  imports: [
    NgTemplateOutlet,
    XuiButtonImports,
    XuiInputImports,
    XuiNonIdealStateImports,
    XuiSegmentedControlImports,
    XuiSelectImports
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col', 'data-testid': 'checks' },
  template: `
    @if (stack(); as state) {
      <div class="pane-bar gap-2 px-2" role="toolbar" aria-label="Checks">
        <button
          type="button"
          xuiButton
          size="sm"
          data-testid="checks-run"
          [disabled]="running()"
          [title]="scope() === 'all' ? 'Checks every set of the project' : 'Checks the texture set in front'"
          (click)="run()"
        >
          {{ running() ? 'Checking…' : 'Check' }}
        </button>
        @if (projectPath()) {
          <xui-segmented-control
            size="sm"
            aria-label="What to check"
            data-testid="checks-scope"
            [options]="scopes"
            [value]="scope()"
            (valueChange)="chooseScope($event)"
          />
        }
        <span class="min-w-0 flex-1 truncate text-sm">{{ scope() === 'all' ? '' : state.set || state.name }}</span>
        @if (stale()) {
          <span class="text-foreground-muted text-xs" data-testid="checks-stale">Edited since</span>
        }
      </div>
      @if (projectPath() && presets().length > 0) {
        <label class="flex shrink-0 items-center gap-2 px-2 py-1 text-xs" title="The platform to check the sets for">
          <span class="text-foreground-muted w-24 shrink-0">Platform</span>
          <xui-select
            class="min-w-0 flex-1"
            aria-label="Platform"
            data-testid="checks-platform"
            [filterable]="false"
            [itemText]="platformName"
            [items]="platforms()"
            [value]="platform()"
            (valueChange)="choosePlatform($event)"
          />
        </label>
      }
      <ng-template #problemList let-checks let-here="here">
        <ol class="py-1" aria-label="Problems" data-testid="checks-list">
          @for (problem of checks.problems; track $index) {
            <li>
              <button
                type="button"
                class="hover:bg-hover flex w-full items-start gap-2 px-2 py-1 text-left text-sm"
                data-testid="checks-problem"
                [attr.data-code]="problem.code"
                [title]="goes(problem, here)"
                (click)="go(problem, here)"
              >
                <span
                  class="mt-1 size-2 shrink-0 rounded-full"
                  [class.bg-error]="problem.severity === 'error'"
                  [class.bg-warning]="problem.severity !== 'error'"
                  [attr.aria-label]="problem.severity === 'error' ? 'Error' : 'Warning'"
                ></span>
                <span class="min-w-0 flex-1">{{ words(problem) }}</span>
              </button>
            </li>
          } @empty {
            <li class="text-foreground-muted px-2 py-1 text-sm" data-testid="checks-clean">Nothing found.</li>
          }
        </ol>
      </ng-template>
      @if (scope() === 'set' && result(); as checks) {
        <dl class="grid shrink-0 grid-cols-[auto_1fr] gap-x-2 px-2 py-1 text-xs" data-testid="checks-summary">
          <dt class="text-foreground-muted">Texel density</dt>
          <dd class="readout" data-testid="checks-density">{{ density(checks) }}</dd>
          <dt class="text-foreground-muted">GPU memory</dt>
          <dd class="readout" data-testid="checks-memory">{{ memory(checks) }}</dd>
        </dl>
        @if (checks.files.length > 0) {
          <details class="shrink-0 px-2 pb-1 text-xs" data-testid="checks-files">
            <summary class="text-foreground-muted cursor-pointer">Memory by file</summary>
            <ul class="mt-1" aria-label="Memory by file">
              @for (file of checks.files; track file.file) {
                <li class="flex items-baseline gap-2" data-testid="checks-file" [title]="held(file)">
                  <span class="min-w-0 flex-1 truncate">{{ file.file }}</span>
                  <span class="text-foreground-muted readout">{{ file.width }} × {{ file.height }}</span>
                  <span class="readout w-16 text-right">{{ size(file.bytes) }}</span>
                </li>
              }
            </ul>
          </details>
        }
        <div class="min-h-0 flex-1 overflow-y-auto">
          <ng-container *ngTemplateOutlet="problemList; context: { $implicit: checks, here: true }" />
        </div>
      } @else if (scope() === 'all' && project(); as all) {
        <p class="readout shrink-0 px-2 py-1 text-xs" data-testid="checks-project-memory">{{ projectMemory(all) }}</p>
        <div class="min-h-0 flex-1 overflow-y-auto" data-testid="checks-sets">
          @for (set of all.sets; track set.document) {
            <section class="border-hairline border-b" data-testid="checks-set" [attr.data-document]="set.document">
              <h3 class="flex items-baseline gap-2 px-2 pt-1 text-xs font-medium">
                <span class="min-w-0 flex-1 truncate">{{ setName(set) }}</span>
                <span class="text-foreground-muted readout">{{ size(set.memoryBytes) }}</span>
                @if (!set.measured) {
                  <span
                    class="text-foreground-muted font-normal"
                    data-testid="checks-unmeasured"
                    title="Open the set to measure its texel density, UV layout and values"
                    >Not measured</span
                  >
                }
              </h3>
              <ng-container
                *ngTemplateOutlet="problemList; context: { $implicit: set, here: set.stack === state.id }"
              />
            </section>
          } @empty {
            <p class="text-foreground-muted px-2 py-1 text-sm">The project has no texture sets.</p>
          }
        </div>
      } @else {
        <p class="text-foreground-muted px-2 py-1 text-sm">
          {{ running() ? 'Reading the maps…' : 'Check to measure the sets before they are exported.' }}
        </p>
      }
      @if (failure(); as reason) {
        <p class="text-error px-2 py-1 text-sm" role="alert" data-testid="checks-failure">{{ reason }}</p>
      }
      @if (projectPath()) {
        <details class="border-hairline shrink-0 border-t px-2 py-1" data-testid="checks-limits">
          <summary class="text-foreground-muted cursor-pointer text-xs">{{ limitsTitle() }}</summary>
          @for (limit of shownLimits(); track limit.key) {
            <label class="mt-1 flex items-center gap-2 text-xs" [title]="limitTitle(limit)">
              <span class="text-foreground-muted w-24 shrink-0">{{ limit.label }}</span>
              <!-- A limit runs the checks again, so it is kept when it is done being typed, not at each key. -->
              <input
                type="number"
                min="0"
                class="min-w-0 flex-1"
                xuiInput
                size="sm"
                [attr.data-testid]="'checks-limit-' + limit.key"
                [value]="shownLimit(limit)"
                (change)="setLimit(limit, $event)"
              />
            </label>
          }
        </details>
      }
    } @else {
      <xui-non-ideal-state
        class="flex-1"
        title="Nothing to check"
        description="Open a texturing document or a layered material to check it before export."
      />
    }
  `
})
export class ChecksPane {
  private readonly paint = inject(PaintStore);
  private readonly projects = inject(ProjectStore);
  private readonly library = inject(LibraryStore);
  private readonly jump = inject(ViewportJump);
  private readonly commands = inject(Commands);

  protected readonly scopes = Scopes;
  protected readonly stack = this.paint.state;
  protected readonly scope = signal<Scope>('set');
  /** A preset id, or {@link Own} for the project's own preset and limits. */
  protected readonly platform = signal(Own);
  protected readonly presets = signal<readonly ExportPresetInfo[]>([]);
  protected readonly result = signal<StackChecks | null>(null);
  protected readonly project = signal<ProjectChecks | null>(null);
  protected readonly running = signal(false);
  protected readonly failure = signal<string | null>(null);
  /** The revision of the set the result was measured at. */
  private readonly measuredAt = signal<{ id: string; revision: number } | null>(null);
  protected readonly stale = computed(() => {
    const state = this.stack();
    const at = this.measuredAt();
    return this.scope() === 'set' && state !== null && at?.id === state.id && at.revision !== state.revision;
  });

  protected readonly projectPath = computed(() => this.projects.active()?.project ?? null);
  private readonly settings = computed(() => this.projects.current()?.checks ?? NoLimits);
  protected readonly platforms = computed(() => [Own, ...this.presets().map(preset => preset.id)]);
  protected readonly platformName = (id: string): string =>
    id === Own ? 'Project’s preset' : (this.presets().find(preset => preset.id === id)?.name ?? id);
  protected readonly shownLimits = computed(() =>
    this.platform() === Own ? Limits : Limits.filter(limit => limit.perPlatform)
  );
  protected readonly limitsTitle = computed(() =>
    this.platform() === Own ? 'Project limits' : `Limits for ${this.platformName(this.platform())}`
  );

  constructor() {
    // Another set in front is another set's checks: the old list would point at the wrong maps.
    effect(() => {
      const id = this.stack()?.id ?? null;
      untracked(() => {
        if (this.measuredAt()?.id !== id) {
          this.result.set(null);
          this.measuredAt.set(null);
          this.failure.set(null);
        }
      });
    });
    // The platforms are the presets the project can export with, its own among them.
    effect(() => {
      const project = this.projectPath();
      untracked(() => void this.loadPresets(project));
    });
  }

  /** Checks the set in front, or every set of the project, for the platform chosen. */
  async run(): Promise<void> {
    const state = this.stack();
    if (state === null || this.running()) {
      return;
    }

    this.running.set(true);
    this.failure.set(null);
    const preset = this.platform() === Own ? {} : { preset: this.platform() };
    try {
      const client = await this.library.api();
      const project = this.projectPath();
      if (this.scope() === 'all' && project !== null) {
        const query = { project, ...preset };
        this.project.set(unwrap(await client.GET('/api/v1/library/project/checks', { params: { query } })));
        return;
      }

      const params = { path: { id: state.id }, query: preset };
      const checks = unwrap(await client.GET('/api/v1/stacks/{id}/checks', { params }));
      if (this.stack()?.id === state.id) {
        this.result.set(checks);
        this.measuredAt.set({ id: state.id, revision: state.revision });
      }
    } catch (error) {
      this.failure.set(messageOf(error));
    } finally {
      this.running.set(false);
    }
  }

  protected chooseScope(scope: Scope | null): void {
    if (scope !== null && scope !== this.scope()) {
      this.scope.set(scope);
      this.failure.set(null);
    }
  }

  /** Another platform is other limits and another preset's files, so a result there is checked again for it. */
  protected async choosePlatform(id: string | null): Promise<void> {
    if (id === null || id === this.platform()) {
      return;
    }

    this.platform.set(id);
    if (this.result() !== null || this.project() !== null) {
      await this.run();
    }
  }

  protected words(problem: CheckProblem): string {
    return checkWords(problem.code, problem.values, problem.detail);
  }

  /** What a click on a problem does, for its tooltip. A view or a camera shows only the set in front. */
  protected goes(problem: CheckProblem, here: boolean): string {
    const target = problem.target;
    if (here && target.camera) {
      return 'Looks at it on the mesh';
    }

    if (here && target.view) {
      return 'Shows it in the viewport';
    }

    if (target.setting === 'bake') {
      return 'Opens the Bake dialog';
    }

    if (target.setting?.startsWith('export.')) {
      return 'Opens the export settings';
    }

    return target.setting ? 'Shows the project’s limit' : 'Open the set to see it';
  }

  /** Goes to a problem: the view and the camera that show it, or the setting that decides it. */
  protected go(problem: CheckProblem, here: boolean): void {
    const target = problem.target;
    if (here && (target.view || target.camera)) {
      this.jump.go({ view: target.view ?? null, camera: target.camera ?? null });
      return;
    }

    if (target.setting?.startsWith('export.')) {
      this.commands.run('export');
    } else if (target.setting === 'bake') {
      this.commands.run('bake');
    }
  }

  protected density(checks: StackChecks): string {
    const measured = Math.round(checks.texelDensity);
    return checks.targetDensity > 0
      ? `${measured} per unit, of ${Math.round(checks.targetDensity)}`
      : `${measured} per unit`;
  }

  protected memory(checks: StackChecks): string {
    const budget = checks.memoryBudget > 0 ? ` of ${bytes(checks.memoryBudget)}` : '';
    return `${bytes(checks.memoryBytes)}${budget} with ${checks.preset}`;
  }

  protected projectMemory(all: ProjectChecks): string {
    const sets = all.sets.length === 1 ? '1 set' : `${count(all.sets.length)} sets`;
    return `${bytes(all.memoryBytes)} for ${sets} with ${all.preset}`;
  }

  /** A set by its own name, or its file's for a set with none. */
  protected setName(checks: StackChecks): string {
    return checks.set || (checks.document.split('/').pop() ?? checks.document).replace(/\.\w+$/u, '');
  }

  protected size(value: number): string {
    return bytes(value);
  }

  /** How a file is held on a GPU, for its tooltip: its compression, and whether it carries mips. */
  protected held(file: CheckFile): string {
    const blocks = file.compression === 'none' ? 'Uncompressed' : `Compressed ${file.compression.toUpperCase()}`;
    return `${blocks}, ${file.mips ? 'with mips' : 'no mips'}`;
  }

  protected limitTitle(limit: Limit): string {
    return this.platform() === Own ? limit.title : `${limit.title}; 0 keeps the project’s`;
  }

  /** The limits being edited: the project's own, or the chosen platform's, which are zeros until it keeps some. */
  private edited(): ProjectCheckSettings | PlatformCheckLimits {
    const id = this.platform();
    return id === Own
      ? this.settings()
      : (this.settings().platforms.find(platform => platform.preset === id) ?? {
          preset: id,
          memoryBudget: 0,
          maxSize: 0,
          texelDensity: 0
        });
  }

  protected shownLimit(limit: Limit): number {
    const edited = this.edited();
    const value = limit.key in edited ? (edited as unknown as Record<string, number>)[limit.key] : 0;
    return Math.round((value ?? 0) * limit.scale * 1000) / 1000;
  }

  /** Keeps a limit the project holds its sets to, for itself or one platform, then checks again against it. */
  protected async setLimit(limit: Limit, event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const path = this.projectPath();
    const value = Number(input.value) / limit.scale;
    if (path === null || !Number.isFinite(value) || value < 0) {
      input.value = String(this.shownLimit(limit));
      return;
    }

    const refused = await this.projects.keepChecks(path, this.withLimit(limit, value));
    this.failure.set(refused);
    if (refused === null && (this.result() !== null || this.project() !== null)) {
      await this.run();
    }
  }

  private withLimit(limit: Limit, value: number): ProjectCheckSettings {
    const settings = this.settings();
    const id = this.platform();
    if (id === Own) {
      return { ...settings, [limit.key]: value };
    }

    const platform = { ...(this.edited() as PlatformCheckLimits), [limit.key]: value };
    const others = settings.platforms.filter(other => other.preset !== id);
    return { ...settings, platforms: [...others, platform] };
  }

  private async loadPresets(project: string | null): Promise<void> {
    try {
      const client = await this.library.api();
      const query = project ? { project } : {};
      this.presets.set(unwrap(await client.GET('/api/v1/export/presets', { params: { query } })).presets);
    } catch {
      // With no presets to choose from, the checks are for the project's own, as they were.
      this.presets.set([]);
    }
  }
}
