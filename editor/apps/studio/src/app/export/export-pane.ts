import { ChangeDetectionStrategy, Component, type OnInit, computed, effect, inject, signal } from '@angular/core';
import type { BuildRequest } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiSelectImports } from '@xui/select';
import { desktop } from '../core/desktop';
import { jobStateName } from '../core/labels';
import { LibrarySelection } from '../library/library-selection';
import { PaintStore } from '../paint/paint-store';
import { ExportStore } from './export-store';

/**
 * Exports the library: choose a preset, see what would change, run it, read what it did. The panel only asks. The Server
 * plans from build keys, so what the preview says is what runs, and everything sent is an id or a path, as for any client.
 */
@Component({
  selector: 'ptn-export',
  imports: [XuiButtonImports, XuiCalloutImports, XuiSelectImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col' },
  template: `
    <div class="flex min-h-0 flex-1 flex-col gap-2.5 overflow-x-hidden overflow-y-auto p-3">
      <div class="flex flex-col gap-1">
        <span class="text-foreground-muted text-xs">Preset</span>
        <xui-select
          class="w-full"
          ariaLabel="Preset"
          data-testid="export-preset"
          [itemText]="presetName"
          [items]="presetIds()"
          [value]="preset()"
          (valueChange)="choose($event)"
        />
      </div>

      @if (chosen(); as info) {
        <div class="well flex flex-col gap-1 p-2 text-xs" data-testid="export-preset-info">
          <span>{{ info.engine }}, normal {{ info.normal }}, {{ info.origin }}</span>
          @for (file of info.files; track file.template) {
            <span class="readout truncate"
              >{{ file.template }} · {{ file.format
              }}{{ file.compression === 'none' ? '' : ' ' + file.compression }}</span
            >
          }
        </div>
      }

      @for (problem of store.problems(); track problem) {
        <xui-callout color="warning">{{ problem }}</xui-callout>
      }

      @if (paint.state(); as stack) {
        <div class="well flex flex-col gap-2 p-2 text-xs" data-testid="export-document">
          <span>This document's maps, as they are now.</span>
          <button type="button" xuiButton size="sm" data-testid="export-stack" (click)="exportStack(stack.id)">
            Export Maps…
          </button>
          @if (store.exported().length > 0) {
            <span class="readout" data-testid="export-stack-done">{{ store.exported().length }} map(s) written</span>
          }
        </div>
      }

      <span class="text-foreground-muted text-xs" data-testid="export-scope">
        {{ asset() ? 'Only what reads ' + asset()?.name : 'The whole library' }}
      </span>

      @if (store.preview(); as preview) {
        <div class="well grid grid-cols-[1fr_auto] gap-x-3 p-2 text-xs" data-testid="export-impact">
          <span>Projects to touch</span>
          <span class="readout" data-testid="export-impact-projects">{{ preview.projects }}</span>
          <span>Outputs to build</span>
          <span class="readout" data-testid="export-impact-rebuilds">{{ preview.rebuilds }}</span>
          <span>Of those, shared work</span>
          <span class="readout">{{ preview.shared }}</span>
          <span>Up to date</span>
          <span class="readout">{{ preview.fresh }}</span>
          <span>Exports to write</span>
          <span class="readout" data-testid="export-impact-exports">{{ preview.exports }}</span>
          @if (preview.unresolved > 0) {
            <span>Cannot be built</span>
            <span class="readout">{{ preview.unresolved }}</span>
          }
        </div>
        @if (preview.nothing; as nothing) {
          <xui-callout color="warning" data-testid="export-nothing">{{ nothing }}</xui-callout>
        }
      }

      <div class="flex gap-1">
        <button type="button" xuiButton size="sm" data-testid="export-preview" [disabled]="!preset()" (click)="ask()">
          Preview
        </button>
        <button
          type="button"
          xuiButton
          size="sm"
          data-testid="export-start"
          [disabled]="!preset() || running()"
          (click)="start()"
        >
          Export
        </button>
        @if (running()) {
          <button type="button" xuiButton size="sm" variant="ghost" data-testid="export-cancel" (click)="cancel()">
            Stop
          </button>
        }
      </div>

      @if (error(); as message) {
        <xui-callout color="error" data-testid="export-error">{{ message }}</xui-callout>
      }

      @if (store.build(); as build) {
        <div class="flex items-center justify-between text-xs">
          <span class="readout" data-testid="export-state">{{ stateName(build.state) }}</span>
          <span class="readout" data-testid="export-percent">{{ percent() }}%</span>
        </div>
        @if (build.error) {
          <xui-callout color="error" data-testid="export-build-error">{{ build.error }}</xui-callout>
        }
      }

      @if (store.report(); as report) {
        <div class="well grid grid-cols-[1fr_auto] gap-x-3 p-2 text-xs" data-testid="export-report">
          <span>Projects touched</span>
          <span class="readout" data-testid="export-report-projects">{{ report.projects }}</span>
          <span>Built</span>
          <span class="readout" data-testid="export-report-built">{{ report.built }}</span>
          <span>Reused</span>
          <span class="readout">{{ report.reused }}</span>
          <span>Files written</span>
          <span class="readout" data-testid="export-report-files">{{ report.files }}</span>
          <span>Failed</span>
          <span class="readout" data-testid="export-report-failed">{{ report.failed }}</span>
        </div>
        @if (report.nothing; as nothing) {
          <xui-callout color="warning" data-testid="export-report-nothing">{{ nothing }}</xui-callout>
        }
        @for (project of store.projects(); track project.id) {
          <div class="flex flex-col gap-1 text-xs" data-testid="export-report-project">
            <span class="font-semibold">{{ project.name }}</span>
            @for (output of project.outputs; track output.name) {
              @if (output.error; as why) {
                <xui-callout color="error" compact data-testid="export-report-error">
                  {{ output.name }}: {{ why }}
                </xui-callout>
              }
            }
            @for (done of project.exports; track done.preset) {
              <span class="text-foreground-muted truncate" [title]="done.folder ?? ''">
                {{ done.preset }}: {{ done.files.length }} files, {{ done.status }}
              </span>
              @if (done.error; as why) {
                <xui-callout color="error" compact data-testid="export-report-error">
                  {{ done.preset }}: {{ why }}
                </xui-callout>
              }
              @for (file of done.skipped; track file) {
                <span class="text-foreground-muted truncate" data-testid="export-report-skipped" [title]="file">
                  Left out, no map for it: {{ file }}
                </span>
              }
              @for (warning of done.warnings; track warning) {
                <span class="text-foreground-muted truncate" [title]="warning">{{ warning }}</span>
              }
            }
          </div>
        }
        @if (store.projectCount() > store.projects().length) {
          <span class="text-foreground-muted text-xs">
            {{ store.projectCount() - store.projects().length }} more in {{ report.folder }}
          </span>
        }
      }
    </div>
  `
})
export class ExportPane implements OnInit {
  protected readonly store = inject(ExportStore);
  protected readonly paint = inject(PaintStore);
  private readonly selection = inject(LibrarySelection);
  protected readonly preset = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly presetIds = computed(() => this.store.presets().map(preset => preset.id));
  protected readonly chosen = computed(() => this.store.presets().find(preset => preset.id === this.preset()) ?? null);
  protected readonly presetName = (id: string): string =>
    this.store.presets().find(preset => preset.id === id)?.name ?? id;
  protected readonly stateName = jobStateName;
  protected readonly running = computed(() => ['queued', 'running'].includes(this.store.build()?.state ?? ''));
  protected readonly percent = computed(() => Math.round((this.store.build()?.progress ?? 0) * 100));

  /** A material selected in the Library narrows the build to what reads it, which is the impact of editing it. */
  protected readonly asset = computed(() => {
    const item = this.selection.item();
    return item?.kind === 'asset' ? item.asset : null;
  });

  private readonly stack = computed(() => this.paint.state()?.id ?? null);

  /** A build that has stopped is read once, whichever way it stopped. */
  private read = '';

  constructor() {
    effect(() => {
      const build = this.store.build();
      if (build && ['done', 'failed', 'cancelled'].includes(build.state) && this.read !== build.id) {
        this.read = build.id;
        // A build refused before it began has no report, and its own error says why.
        void (build.state === 'done'
          ? this.attempt(() => this.store.read(build.id))
          : this.store.read(build.id).catch(() => undefined));
      }
    });

    // Numbers that answered another question say nothing about this one.
    effect(() => {
      this.asset();
      this.store.preview.set(null);
    });
    effect(() => {
      this.stack();
      this.store.exported.set([]);
    });
  }

  ngOnInit(): void {
    void this.attempt(async () => {
      await this.store.load();
      this.preset.update(preset => preset ?? this.presetIds()[0] ?? null);
    });
  }

  /** Painted work reaches a file with no build behind it: the stack that is open, in a folder chosen now. */
  protected async exportStack(id: string): Promise<void> {
    const folder = await desktop()?.openFolder();
    if (folder) {
      try {
        await this.store.exportStack(id, folder);
      } catch (failure) {
        this.error.set(failure instanceof Error ? failure.message : String(failure));
      }
    }
  }

  protected choose(preset: string | null): void {
    this.preset.set(preset);
    this.store.preview.set(null);
  }

  protected ask(): Promise<void> {
    return this.attempt(() => this.store.ask(this.request()));
  }

  protected start(): Promise<void> {
    return this.attempt(() => this.store.start(this.request()));
  }

  protected cancel(): Promise<void> {
    return this.attempt(() => this.store.cancel());
  }

  private request(): BuildRequest {
    const preset = this.preset();
    const asset = this.asset();
    return { presets: preset ? [preset] : [], ...(asset ? { asset: asset.id } : {}) };
  }

  private async attempt(work: () => Promise<void>): Promise<void> {
    this.error.set(null);
    try {
      await work();
    } catch (error) {
      this.error.set(error instanceof Error ? error.message : String(error));
    }
  }
}
