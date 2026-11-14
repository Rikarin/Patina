import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import type { CheckProblem, ProjectCheckSettings, StackChecks } from '@patina/api-client';
import { unwrap } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiInputImports } from '@xui/input';
import { XuiNonIdealStateImports } from '@xui/non-ideal-state';
import { Commands } from '../core/commands';
import { checkWords, messageOf } from '../core/messages';
import { bytes } from '../core/numbers';
import { LibraryStore } from '../library/library-store';
import { ProjectStore } from '../library/project-store';
import { PaintStore } from '../paint/paint-store';
import { ViewportJump } from '../viewport/viewport-jump';

/** A project that has said nothing holds its sets to nothing but the library's density. */
const NoLimits: ProjectCheckSettings = { memoryBudget: 0, maxSize: 0, texelDensity: 0, densityTolerance: 0.25 };

/** One field of the project's checks, as the pane edits it. */
interface Limit {
  readonly key: keyof ProjectCheckSettings;
  readonly label: string;
  readonly title: string;
  /** What the field shows a value as, and what the project keeps from what is typed. */
  readonly scale: number;
}

const Limits: readonly Limit[] = [
  {
    key: 'memoryBudget',
    label: 'Budget (MB)',
    title: 'The most GPU memory one texture set’s exported files may take, or 0 for no limit',
    scale: 1
  },
  {
    key: 'maxSize',
    label: 'Largest side',
    title: 'The largest side a texture set’s maps may have, in texels, or 0 for no limit',
    scale: 1
  },
  {
    key: 'texelDensity',
    label: 'Texel density',
    title: 'Texels per unit of the model (a metre in glTF) every set is held to, or 0 for the library’s',
    scale: 1
  },
  {
    key: 'densityTolerance',
    label: 'Tolerance (%)',
    title: 'How far a set’s density may stray from it before it is flagged',
    scale: 100
  }
];

/**
 * What the texture set in front would get wrong in an export (#159), as the Server finds it: budgets and limits the
 * project sets, the maps its preset reads, its texel density, its UV layout, and values no physical material has. Each
 * problem goes to where it is: the view that shows it, and on the mesh a camera that looks at it. Checking reads every
 * map once, so it runs when asked and says when an edit has left it behind.
 */
@Component({
  selector: 'ptn-checks',
  imports: [XuiButtonImports, XuiInputImports, XuiNonIdealStateImports],
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
          title="Checks the texture set in front against the project and its export preset"
          (click)="run()"
        >
          {{ running() ? 'Checking…' : 'Check' }}
        </button>
        <span class="min-w-0 flex-1 truncate text-sm">{{ state.set || state.name }}</span>
        @if (stale()) {
          <span class="text-foreground-muted text-xs" data-testid="checks-stale">Edited since</span>
        }
      </div>
      @if (result(); as checks) {
        <dl class="grid shrink-0 grid-cols-[auto_1fr] gap-x-2 px-2 py-1 text-xs" data-testid="checks-summary">
          <dt class="text-foreground-muted">Texel density</dt>
          <dd class="readout" data-testid="checks-density">{{ density(checks) }}</dd>
          <dt class="text-foreground-muted">GPU memory</dt>
          <dd class="readout" data-testid="checks-memory">{{ memory(checks) }}</dd>
        </dl>
        <ol class="min-h-0 flex-1 overflow-y-auto py-1" aria-label="Problems" data-testid="checks-list">
          @for (problem of checks.problems; track $index) {
            <li>
              <button
                type="button"
                class="hover:bg-hover flex w-full items-start gap-2 px-2 py-1 text-left text-sm"
                data-testid="checks-problem"
                [attr.data-code]="problem.code"
                [title]="goes(problem)"
                (click)="go(problem)"
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
      } @else {
        <p class="text-foreground-muted px-2 py-1 text-sm">
          {{ running() ? 'Reading the maps…' : 'Check to measure the set before it is exported.' }}
        </p>
      }
      @if (failure(); as reason) {
        <p class="text-error px-2 py-1 text-sm" role="alert" data-testid="checks-failure">{{ reason }}</p>
      }
      @if (projectPath()) {
        <details class="border-hairline shrink-0 border-t px-2 py-1" data-testid="checks-limits">
          <summary class="text-foreground-muted cursor-pointer text-xs">Project limits</summary>
          @for (limit of limits; track limit.key) {
            <label class="mt-1 flex items-center gap-2 text-xs" [title]="limit.title">
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

  protected readonly limits = Limits;
  protected readonly stack = this.paint.state;
  protected readonly result = signal<StackChecks | null>(null);
  protected readonly running = signal(false);
  protected readonly failure = signal<string | null>(null);
  /** The revision of the stack the result was measured at. */
  private readonly measuredAt = signal<{ id: string; revision: number } | null>(null);
  protected readonly stale = computed(() => {
    const state = this.stack();
    const at = this.measuredAt();
    return state !== null && at !== null && at.id === state.id && at.revision !== state.revision;
  });

  protected readonly projectPath = computed(() => this.projects.active()?.project ?? null);
  private readonly settings = computed(() => this.projects.current()?.checks ?? NoLimits);

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
  }

  /** Measures the set in front now. */
  async run(): Promise<void> {
    const state = this.stack();
    if (state === null || this.running()) {
      return;
    }

    this.running.set(true);
    this.failure.set(null);
    try {
      const client = await this.library.api();
      const checks = unwrap(await client.GET('/api/v1/stacks/{id}/checks', { params: { path: { id: state.id } } }));
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

  protected words(problem: CheckProblem): string {
    return checkWords(problem.code, problem.values, problem.detail);
  }

  /** What a click on a problem does, for its tooltip. */
  protected goes(problem: CheckProblem): string {
    const target = problem.target;
    if (target.camera) {
      return 'Looks at it on the mesh';
    }

    if (target.view) {
      return 'Shows it in the viewport';
    }

    if (target.setting === 'bake') {
      return 'Opens the Bake dialog';
    }

    return target.setting?.startsWith('export.') ? 'Opens the export settings' : 'Shows the project’s limit';
  }

  /** Goes to a problem: the view and the camera that show it, or the setting that decides it. */
  protected go(problem: CheckProblem): void {
    const target = problem.target;
    if (target.view || target.camera) {
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

  protected shownLimit(limit: Limit): number {
    return Math.round(this.settings()[limit.key] * limit.scale * 1000) / 1000;
  }

  /** Keeps a limit the project holds its sets to, then checks again against it. */
  protected async setLimit(limit: Limit, event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const path = this.projectPath();
    const value = Number(input.value) / limit.scale;
    if (path === null || !Number.isFinite(value) || value < 0) {
      input.value = String(this.shownLimit(limit));
      return;
    }

    const refused = await this.projects.keepChecks(path, { ...this.settings(), [limit.key]: value });
    this.failure.set(refused);
    if (refused === null && this.result() !== null) {
      await this.run();
    }
  }
}
