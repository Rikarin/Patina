import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  Injector,
  type OnInit,
  computed,
  effect,
  inject,
  signal,
  untracked
} from '@angular/core';
import type { BuildRequest, ExportPresetFile, ExportPresetInfo } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiCheckboxImports } from '@xui/checkbox';
import { XuiColorPickerImports } from '@xui/color-picker';
import { XuiSegmentedControlImports, type XuiSegmentedOption } from '@xui/segmented-control';
import { XuiSelectImports } from '@xui/select';
import { XuiSliderImports } from '@xui/slider';
import { XuiSwitchImports } from '@xui/switch';
import { desktop } from '../core/desktop';
import { jobStateName, labelIn, termName } from '../core/labels';
import { messageOf } from '../core/messages';
import { percent, toPercent } from '../core/numbers';
import { SystemStatus } from '../core/system-status';
import { LibrarySelection } from '../library/library-selection';
import { ProjectStore } from '../library/project-store';
import { PaintStore } from '../paint/paint-store';
import { CompressionView } from './compression-view';
import {
  type ExportOwner,
  type ExportScope,
  type ExportSettings,
  ExportStore,
  type FileChoice,
  requestFor,
  sameOwner,
  settingsFor,
  settingsOf,
  usesDistance
} from './export-store';
import { PresetEditor } from './preset-editor';

const SaveDelay = 400;
/** The documents whose maps an export can take, each as a texture set of its own. */
const exportedKinds: ReadonlySet<string> = new Set(['texturing', 'graph', 'layered-material', 'image-material']);
const StatusNames: Readonly<Record<string, string>> = {
  built: 'written',
  reused: 'copied from an earlier export',
  fresh: 'already up to date',
  failed: 'failed'
};

/** A colour as the colour picker holds it, from lanes 0 to 1. */
function hexOf(colour: readonly number[]): string {
  const lane = (at: number, fallback: number) =>
    Math.round(Math.min(1, Math.max(0, colour[at] ?? fallback)) * 255)
      .toString(16)
      .padStart(2, '0');
  return `#${lane(0, 0)}${lane(1, 0)}${lane(2, 0)}${lane(3, 1)}`;
}

function lanesOf(hex: string): number[] {
  const digits = hex.replace('#', '');
  const lanes = [0, 2, 4, 6].map(at => Number.parseInt(digits.slice(at, at + 2) || 'ff', 16) / 255);
  return lanes.map(lane => Math.round(lane * 1000) / 1000);
}

/**
 * The Export dialog: what to export (the document in front, its project, or the library), through which preset, at what
 * size, padded how, into which folder, and which of the preset's files in which formats. The Server plans from build
 * keys, so what Preview says is what runs, and everything sent is an id, a path or a setting, as for any client.
 */
@Component({
  selector: 'ptn-export',
  imports: [
    CompressionView,
    PresetEditor,
    XuiButtonImports,
    XuiCalloutImports,
    XuiCheckboxImports,
    XuiColorPickerImports,
    XuiSegmentedControlImports,
    XuiSelectImports,
    XuiSliderImports,
    XuiSwitchImports
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col' },
  template: `
    @if (editing()) {
      <ptn-preset-editor
        class="min-h-0 flex-1"
        [project]="target()?.path ?? null"
        [projectName]="target()?.name ?? null"
        [start]="settings().preset"
        (closed)="closeEditor($event)"
      />
    } @else {
      <div class="grid min-h-0 flex-1 grid-cols-[19rem_minmax(0,1fr)] gap-3 overflow-hidden p-3">
        <div class="flex min-h-0 flex-col gap-3 overflow-y-auto pr-1" data-testid="export-settings">
          <section class="flex flex-col gap-1.5">
            <span class="text-foreground-muted text-xs font-medium">Export</span>
            <xui-segmented-control
              size="sm"
              fill
              aria-label="What to export"
              data-testid="export-scope-choice"
              [options]="scopes()"
              [value]="scope()"
              (valueChange)="chooseScope($event)"
            />
            <span class="text-foreground-muted truncate text-xs" data-testid="export-scope" [title]="scopeText()">
              {{ scopeText() }}
            </span>
          </section>

          <section class="flex flex-col gap-1.5">
            <span class="text-foreground-muted text-xs font-medium">Preset</span>
            <xui-select
              class="w-full"
              aria-label="Preset"
              data-testid="export-preset"
              [filterable]="false"
              [itemText]="presetName"
              [items]="presetIds()"
              [value]="settings().preset"
              (valueChange)="choosePreset($event)"
            />
            @if (chosen(); as info) {
              <span class="text-foreground-muted text-xs" data-testid="export-preset-info">
                {{ info.engine }} · normal map {{ greenName(info.normal) }} · {{ originName(info.origin) }}
                @if (isDefault()) {
                  · default
                }
              </span>
            }
            <div class="flex gap-1">
              <button
                type="button"
                xuiButton
                size="sm"
                variant="ghost"
                data-testid="export-edit-presets"
                (click)="editing.set(true)"
              >
                Edit Presets…
              </button>
              @if (chosen() && !isDefault() && chosen()?.origin !== 'project') {
                <button
                  type="button"
                  xuiButton
                  size="sm"
                  variant="ghost"
                  data-testid="export-make-default"
                  title="Every export that has kept no preset of its own starts from this one, in any project"
                  (click)="makeDefault()"
                >
                  Make Default
                </button>
              }
            </div>
          </section>

          <section class="flex flex-col gap-1.5">
            <span class="text-foreground-muted text-xs font-medium">Output folder</span>
            <span class="readout truncate text-xs" data-testid="export-folder" [title]="settings().folder ?? ''">
              {{ settings().folder ?? 'The library’s exports folder' }}
            </span>
            <div class="flex gap-1">
              <button type="button" xuiButton size="sm" data-testid="export-folder-choose" (click)="chooseFolder()">
                Choose…
              </button>
              @if (settings().folder) {
                <button
                  type="button"
                  xuiButton
                  size="sm"
                  variant="ghost"
                  data-testid="export-folder-default"
                  (click)="change({ folder: null })"
                >
                  Use Default
                </button>
              }
              @if (revealable(); as path) {
                <button
                  type="button"
                  xuiButton
                  size="sm"
                  variant="ghost"
                  data-testid="export-reveal"
                  (click)="reveal(path)"
                >
                  Reveal
                </button>
              }
            </div>
          </section>

          <section class="flex flex-col gap-1.5">
            <span class="text-foreground-muted text-xs font-medium">Size</span>
            <div class="flex gap-1">
              <xui-select
                class="min-w-0 flex-1"
                aria-label="Size"
                data-testid="export-size"
                [filterable]="false"
                [itemText]="sizeName"
                [items]="sizes()"
                [value]="settings().size === null ? 'native' : '' + settings().size"
                (valueChange)="chooseSize($event)"
              />
              <xui-select
                class="min-w-0 flex-1"
                aria-label="Resize filter"
                data-testid="export-filter"
                [disabled]="settings().size === null"
                [filterable]="false"
                [itemText]="filterName"
                [items]="store.filters()"
                [value]="settings().filter"
                (valueChange)="change({ filter: $event ?? 'box' })"
              />
            </div>
          </section>

          <section class="flex flex-col gap-1.5">
            <span class="text-foreground-muted text-xs font-medium">Padding</span>
            <xui-select
              class="w-full"
              aria-label="Padding"
              data-testid="export-padding"
              [filterable]="false"
              [itemText]="paddingName"
              [items]="store.paddings()"
              [value]="settings().padding"
              (valueChange)="change({ padding: $event ?? 'none' })"
            />
            @if (distanced()) {
              <div class="flex items-center gap-2">
                <span class="text-foreground-muted w-16 shrink-0 text-xs">Distance</span>
                <xui-slider
                  class="min-w-0 flex-1"
                  labelStepSize="0"
                  [attr.aria-label]="'Padding distance'"
                  data-testid="export-distance"
                  [max]="64"
                  [min]="0"
                  [stepSize]="1"
                  [value]="settings().distance"
                  (valueChange)="change({ distance: $event })"
                />
                <span class="readout w-12 shrink-0 text-right text-xs" data-testid="export-distance-readout">
                  {{ settings().distance }} px
                </span>
              </div>
            }
            @if (settings().padding === 'colour') {
              <div class="flex items-center gap-2">
                <span class="text-foreground-muted w-16 shrink-0 text-xs">Color</span>
                <xui-color-picker showAlpha [value]="paddingHex()" (valueChange)="change({ colour: lanes($event) })" />
              </div>
            }
            <span class="text-foreground-muted text-xs">
              Fills what the mesh's layout leaves empty, so mips and filtering read the islands. A material with no mesh
              has nothing to fill.
            </span>
          </section>

          <section class="flex items-center justify-between gap-2">
            <span
              class="text-xs"
              title="An 8-bit file rounds up as often as its value is past the level below, so gradients do not band"
            >
              Dither 8-bit files
            </span>
            <xui-switch
              aria-label="Dither 8-bit files"
              data-testid="export-dither"
              [checked]="settings().dither"
              (checkedChange)="change({ dither: $event })"
            />
          </section>
        </div>

        <div class="flex min-h-0 flex-col gap-2.5 overflow-y-auto">
          @for (problem of store.problems(); track problem) {
            <xui-callout color="warning" compact>{{ problem }}</xui-callout>
          }

          @if (setNames().length > 0) {
            <section class="well flex flex-col gap-1 p-2" data-testid="export-sets">
              <div class="flex items-center gap-1">
                <span class="text-foreground-muted min-w-0 flex-1 text-xs font-medium">Texture sets</span>
                <button
                  type="button"
                  xuiButton
                  size="sm"
                  variant="ghost"
                  data-testid="export-sets-all"
                  (click)="checkSets('all')"
                >
                  Check All
                </button>
                <button
                  type="button"
                  xuiButton
                  size="sm"
                  variant="ghost"
                  data-testid="export-sets-none"
                  (click)="checkSets('none')"
                >
                  Uncheck All
                </button>
                <button
                  type="button"
                  xuiButton
                  size="sm"
                  variant="ghost"
                  data-testid="export-sets-invert"
                  (click)="checkSets('invert')"
                >
                  Invert
                </button>
              </div>
              @for (set of setNames(); track set) {
                <div class="flex items-center gap-2 text-xs" data-testid="export-set">
                  <xui-checkbox
                    class="min-w-0 flex-1"
                    [aria-label]="'Export ' + set"
                    [checked]="!settings().skipped.includes(set)"
                    (checkedChange)="toggleSet(set)"
                  >
                    <span class="truncate">{{ set }}</span>
                  </xui-checkbox>
                  <xui-select
                    class="w-32 shrink-0"
                    data-testid="export-set-size"
                    title="The longest side this texture set's files are written at. The set keeps its own size."
                    [aria-label]="'Export size of ' + set"
                    [filterable]="false"
                    [itemText]="setSizeName"
                    [items]="setSizes()"
                    [value]="setSizeOf(set)"
                    (valueChange)="chooseSetSize(set, $event)"
                  />
                </div>
              }
              <div class="flex items-center justify-between gap-2 pt-1">
                <span
                  class="text-xs"
                  title="Sets that share one UV square go into one set of files, each where its layout puts it. A set's own size does not apply."
                >
                  Merge into one set of files
                </span>
                <xui-switch
                  aria-label="Merge texture sets into one set of files"
                  data-testid="export-merge"
                  [checked]="settings().merge"
                  (checkedChange)="change({ merge: $event })"
                />
              </div>
            </section>
          }

          @if (chosen(); as info) {
            <section class="well flex flex-col p-2" data-testid="export-files">
              <div
                class="text-foreground-muted grid grid-cols-[1.25rem_minmax(0,1fr)_11rem] gap-x-2 pb-1 text-xs font-medium"
              >
                <span></span>
                <span>File</span>
                <span>Format</span>
              </div>
              @for (file of info.files; track file.template) {
                @let choice = choiceOf(file);
                <div
                  class="grid grid-cols-[1.25rem_minmax(0,1fr)_11rem] items-center gap-x-2 py-0.5 text-xs"
                  data-testid="export-file"
                  [class.opacity-50]="!choice.enabled"
                >
                  <xui-checkbox
                    data-testid="export-file-on"
                    [aria-label]="'Write ' + file.template"
                    [checked]="choice.enabled"
                    (checkedChange)="chooseFile(file, { enabled: !choice.enabled })"
                  />
                  <span class="min-w-0 truncate" [title]="mapsOf(file)">
                    <span class="readout">{{ file.template }}</span>
                    <span class="text-foreground-muted"> · {{ mapsOf(file) }}</span>
                  </span>
                  <xui-select
                    class="w-full"
                    data-testid="export-file-format"
                    [aria-label]="'Format of ' + file.template"
                    [filterable]="false"
                    [itemText]="formatName"
                    [items]="formatsFor(file)"
                    [value]="choice.format"
                    (valueChange)="chooseFile(file, { format: $event ?? file.format })"
                  />
                </div>
                @if (choice.enabled && (isContainer(choice.format) || choice.format === 'jpeg')) {
                  <div
                    class="grid grid-cols-[1.25rem_minmax(0,1fr)_auto] items-center gap-x-2 pb-1 text-xs"
                    data-testid="export-file-options"
                  >
                    <span></span>
                    <span class="flex min-w-0 items-center gap-1.5">
                      @if (isContainer(choice.format)) {
                        <xui-select
                          class="w-40"
                          data-testid="export-file-compression"
                          [aria-label]="'Compression of ' + file.template"
                          [filterable]="false"
                          [itemText]="compressionName"
                          [items]="compressionsFor(file)"
                          [value]="compressionOf(file, choice)"
                          (valueChange)="chooseFile(file, { compression: $event ?? 'none' })"
                        />
                        @if (searched(compressionOf(file, choice))) {
                          <xui-select
                            class="w-28"
                            data-testid="export-file-quality"
                            [aria-label]="'Search of ' + file.template"
                            [filterable]="false"
                            [itemText]="qualityName"
                            [items]="searches"
                            [value]="qualityOf(file, choice)"
                            (valueChange)="chooseFile(file, { quality: $event ?? 'balanced' })"
                          />
                        }
                        <xui-checkbox
                          data-testid="export-file-mips"
                          [aria-label]="'Mips of ' + file.template"
                          [checked]="choice.mips ?? file.mips"
                          (checkedChange)="chooseFile(file, { mips: !(choice.mips ?? file.mips) })"
                        >
                          Mips
                        </xui-checkbox>
                      } @else {
                        <span class="text-foreground-muted">Quality</span>
                        <xui-slider
                          class="w-28"
                          labelStepSize="0"
                          data-testid="export-file-jpeg"
                          [attr.aria-label]="'JPEG quality of ' + file.template"
                          [min]="1"
                          [max]="100"
                          [stepSize]="1"
                          [value]="jpegQuality(file, choice)"
                          (valueChange)="chooseFile(file, { quality: '' + $event })"
                        />
                        <span class="readout w-8 text-right">{{ jpegQuality(file, choice) }}</span>
                      }
                    </span>
                    <button
                      type="button"
                      xuiButton
                      size="sm"
                      variant="ghost"
                      data-testid="export-file-preview"
                      [disabled]="!target()"
                      [title]="'See what ' + formatName(choice.format) + ' does to this file'"
                      (click)="previewing.set(file.template)"
                    >
                      See Compression
                    </button>
                  </div>
                }
              }
            </section>
          }

          @if (previewing(); as template) {
            @if (target(); as project) {
              <ptn-compression-view
                [project]="project.path"
                [projectId]="project.id"
                [preset]="settings().preset ?? ''"
                [file]="template"
                [set]="previewSet()"
                [options]="previewOptions()"
                (closed)="previewing.set(null)"
              />
            }
          }

          @if (unsaved().length > 0) {
            <xui-callout color="warning" compact data-testid="export-unsaved">
              <span class="flex items-center gap-2">
                <span class="min-w-0 flex-1">Export reads saved files, so {{ unsavedText() }}.</span>
                <button
                  type="button"
                  xuiButton
                  size="sm"
                  data-testid="export-save-all"
                  (click)="project.saveTabs(unsaved())"
                >
                  {{ unsaved().length === 1 ? 'Save' : 'Save All' }}
                </button>
              </span>
            </xui-callout>
          }

          @if (paint.state(); as stack) {
            <div class="flex items-center gap-2 text-xs" data-testid="export-document">
              <span class="text-foreground-muted min-w-0 flex-1">Or write this document's maps as they are, as</span>
              <xui-select
                class="w-36"
                size="sm"
                aria-label="Format of the document's maps"
                data-testid="export-stack-format"
                [filterable]="false"
                [itemText]="stackFormatName"
                [items]="stackFormats()"
                [value]="stackFormat()"
                (valueChange)="stackFormat.set($event ?? onScreen)"
              />
              <button
                type="button"
                xuiButton
                size="sm"
                variant="ghost"
                data-testid="export-stack"
                (click)="exportStack(stack.id)"
              >
                Export Maps…
              </button>
              @if (store.exported().length > 0) {
                <span class="readout" data-testid="export-stack-done">{{ store.exported().length }} maps written</span>
              }
            </div>
          }

          @if (store.preview(); as preview) {
            <div class="well grid grid-cols-[1fr_auto] gap-x-3 p-2 text-xs" data-testid="export-impact">
              <span>Projects to touch</span>
              <span class="readout" data-testid="export-impact-projects">{{ preview.projects }}</span>
              <span>Maps to make</span>
              <span class="readout" data-testid="export-impact-rebuilds">{{ preview.rebuilds }}</span>
              <span>Of those, made once for several projects</span>
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
              <xui-callout color="warning" compact data-testid="export-nothing">{{ nothing }}</xui-callout>
            }
          }

          @if (confirming()) {
            <div class="well flex flex-col gap-2 p-2 text-xs" data-testid="export-confirm">
              <span>
                Export every project in the library through {{ chosen()?.name }}? It can take a while, and it replaces
                what earlier exports wrote there.
              </span>
              <div class="flex gap-1">
                <button
                  type="button"
                  xuiButton
                  size="sm"
                  color="primary"
                  data-testid="export-confirm-yes"
                  (click)="run()"
                >
                  Export Library
                </button>
                <button type="button" xuiButton size="sm" variant="ghost" (click)="confirming.set(false)">
                  Cancel
                </button>
              </div>
            </div>
          }

          <!-- As the Bake dialog's: what runs it is primary and last, at the right (#200). -->
          <div class="flex justify-end gap-1">
            @if (running()) {
              <button type="button" xuiButton size="sm" variant="ghost" data-testid="export-cancel" (click)="cancel()">
                Stop
              </button>
            }
            <button
              type="button"
              xuiButton
              size="sm"
              data-testid="export-preview"
              [disabled]="!chosen()"
              (click)="ask()"
            >
              Preview
            </button>
            <button
              type="button"
              xuiButton
              size="sm"
              color="primary"
              data-testid="export-start"
              [disabled]="!chosen() || running() || nothingChosen()"
              (click)="start()"
            >
              Export
            </button>
          </div>

          @if (error(); as message) {
            <xui-callout color="error" compact data-testid="export-error">{{ message }}</xui-callout>
          }

          @if (store.build(); as build) {
            <div class="flex flex-col gap-1.5" data-testid="export-progress">
              <div class="flex items-center gap-2 text-xs">
                <span class="readout" data-testid="export-state">{{ stateName(build.state) }}</span>
                <span class="text-foreground-muted min-w-0 flex-1 truncate" data-testid="export-current">
                  {{ running() ? (build.report?.current ?? '') : '' }}
                </span>
                <span class="readout" data-testid="export-percent">{{ percentText() }}</span>
              </div>
              <div
                class="bg-surface-sunken h-1.5 overflow-hidden rounded-sm"
                role="progressbar"
                aria-label="Export progress"
                [attr.aria-valuenow]="percent()"
                aria-valuemin="0"
                aria-valuemax="100"
              >
                <div class="bg-primary h-full" [style.width.%]="percent()"></div>
              </div>
            </div>
            @if (build.error) {
              <xui-callout color="error" compact data-testid="export-build-error">{{ build.error }}</xui-callout>
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
              <xui-callout color="warning" compact data-testid="export-report-nothing">{{ nothing }}</xui-callout>
            }
            @for (done of store.projects(); track done.id) {
              <div class="flex flex-col gap-1 text-xs" data-testid="export-report-project">
                <span class="font-semibold">{{ done.name }}</span>
                @for (output of done.outputs; track output.name) {
                  @if (output.error; as why) {
                    <xui-callout color="error" compact data-testid="export-report-error">
                      {{ output.name }}: {{ why }}
                    </xui-callout>
                  }
                }
                @for (written of done.exports; track written.preset) {
                  <div class="flex items-center gap-2">
                    <span class="text-foreground-muted min-w-0 flex-1 truncate" [title]="written.folder ?? ''">
                      {{ presetName(written.preset) }}: {{ written.files.length }}
                      {{ written.files.length === 1 ? 'file' : 'files' }},
                      {{ statusName(written.status) }}
                    </span>
                    @if (written.folder) {
                      <button
                        type="button"
                        xuiButton
                        size="sm"
                        variant="ghost"
                        data-testid="export-report-reveal"
                        (click)="reveal(written.folder)"
                      >
                        Reveal
                      </button>
                    }
                  </div>
                  @if (written.error; as why) {
                    <xui-callout color="error" compact data-testid="export-report-error">
                      {{ presetName(written.preset) }}: {{ why }}
                    </xui-callout>
                  }
                  @for (file of written.skipped; track file) {
                    <span class="text-foreground-muted truncate" data-testid="export-report-skipped" [title]="file">
                      Left out, no map for it: {{ file }}
                    </span>
                  }
                  @for (warning of written.warnings; track warning) {
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
      </div>
    }
  `
})
export class ExportPane implements OnInit {
  protected readonly store = inject(ExportStore);
  protected readonly paint = inject(PaintStore);
  protected readonly project = inject(ProjectStore);
  private readonly selection = inject(LibrarySelection);
  private readonly status = inject(SystemStatus);
  private readonly injector = inject(Injector);

  /** The scope chosen by hand, which holds until it stops being possible. */
  private readonly picked = signal<ExportScope | null>(null);
  protected readonly settings = signal<ExportSettings>(settingsFor(null));
  protected readonly error = signal<string | null>(null);
  protected readonly confirming = signal(false);
  /** Whether the preset editor is showing in place of the export's settings. */
  protected readonly editing = signal(false);
  /** The file whose compression is being looked at, by its template, or null. */
  protected readonly previewing = signal<string | null>(null);
  /** The texture sets of the project in front as the Server listed them, for one that is not open. */
  private readonly keptSets = signal<readonly string[]>([]);
  protected readonly searches = ['fast', 'balanced', 'best'];
  /** The Server's texture sides, after the maps' own size. */
  protected readonly sizes = computed(() => ['native', ...this.status.textureSides().map(String)]);

  /** The document's maps as the 8-bit RGBA PNGs the screen shows, which is what Export Maps writes unless told. */
  protected readonly onScreen = 'screen';
  protected readonly stackFormat = signal<string>(this.onScreen);
  protected readonly stackFormats = computed(() => [this.onScreen, ...this.store.formats().map(format => format.id)]);
  protected readonly stackFormatName = (format: string): string =>
    format === this.onScreen ? 'PNG, as on screen' : this.formatName(format);
  protected readonly presetIds = computed(() => this.store.presets().map(preset => preset.id));
  protected readonly chosen = computed<ExportPresetInfo | null>(
    () => this.store.presets().find(preset => preset.id === this.settings().preset) ?? null
  );
  /** Whether the chosen preset is the one exports start from. */
  protected readonly isDefault = computed(() => {
    const chosen = this.chosen();
    return chosen !== null && chosen.id === this.store.defaultPreset();
  });
  protected readonly stateName = jobStateName;
  protected readonly running = computed(() => ['queued', 'running'].includes(this.store.build()?.state ?? ''));
  protected readonly percent = computed(() => toPercent(this.store.build()?.progress ?? 0));
  protected readonly percentText = computed(() => percent(this.store.build()?.progress ?? 0));
  protected readonly distanced = computed(() => usesDistance(this.settings().padding));
  protected readonly paddingHex = computed(() => hexOf(this.settings().colour));

  protected readonly presetName = (id: string): string =>
    this.store.presets().find(preset => preset.id === id)?.name ?? id;
  protected readonly sizeName = (size: string): string => (size === 'native' ? 'Maps’ size' : `${size} px`);
  protected readonly filterName = (filter: string): string => labelIn(this.store.filterTerms(), filter);
  protected readonly paddingName = (mode: string): string => labelIn(this.store.paddingTerms(), mode);
  protected readonly formatName = (format: string): string =>
    this.store.formats().find(known => known.id === format)?.label ?? format;
  protected readonly lanes = lanesOf;
  protected readonly compressionName = (compression: string): string =>
    labelIn(this.store.compressionTerms(), compression);
  protected readonly qualityName = (quality: string): string =>
    ({ fast: 'Fast', balanced: 'Balanced', best: 'Best' })[quality] ?? quality;
  protected readonly setSizeName = (size: string): string => (size === 'export' ? 'Export’s size' : `${size} px`);
  /** The sides a texture set can take of its own, after the export's. */
  protected readonly setSizes = computed(() => ['export', ...this.status.textureSides().map(String)]);

  /** The project the dialog is about: the one in front, or the one selected in the Library. */
  protected readonly target = computed<{ id: string; name: string; path: string } | null>(() => {
    const open = this.project.current();
    if (open) {
      return { id: open.id, name: open.name, path: open.path };
    }
    const item = this.selection.item();
    return item?.kind === 'project' ? { id: item.project.id, name: item.project.name, path: item.project.path } : null;
  });

  /**
   * The document in front as an export takes it: its texture set, and its file below the project when the project does
   * not export it as it is, such as a graph in a texturing project, which then exports on its own.
   */
  private readonly documentExport = computed<{ set: string; file: string | null } | null>(() => {
    const document = this.project.document();
    const open = this.project.current();
    if (!document || !open) {
      return null;
    }
    const set = document.material ?? document.name;
    if (open.exportSets.includes(set)) {
      return { set, file: null };
    }
    const below = `projects/${open.path}/`;
    return document.id && exportedKinds.has(document.kind) && document.path.startsWith(below)
      ? { set, file: document.path.slice(below.length) }
      : null;
  });

  /** The texture set of the document in front, when an export can write it. */
  private readonly documentSet = computed(() => this.documentExport()?.set ?? null);

  /** A material selected in the Library narrows a library export to what reads it, which is the impact of editing it. */
  private readonly asset = computed(() => {
    const item = this.selection.item();
    return item?.kind === 'asset' ? item.asset : null;
  });

  /** What the dialog exports: what was picked, or else the narrowest thing that is open. */
  protected readonly scope = computed<ExportScope>(() => {
    const possible = this.scopes()
      .filter(option => !option.disabled)
      .map(option => option.value);
    const picked = this.picked();
    return picked && possible.includes(picked) ? picked : (possible[0] ?? 'library');
  });

  protected readonly scopes = computed<XuiSegmentedOption<ExportScope>[]>(() => [
    { value: 'document', label: 'Document', disabled: this.documentSet() === null },
    { value: 'project', label: 'Project', disabled: this.target() === null },
    { value: 'library', label: 'Library' }
  ]);

  protected readonly scopeText = computed(() => {
    switch (this.scope()) {
      case 'document':
        return this.documentExport()?.file
          ? `${this.documentSet() ?? ''} on its own, as a texture set of ${this.target()?.name ?? ''}`
          : `The texture set ${this.documentSet() ?? ''} of ${this.target()?.name ?? ''}`;
      case 'project':
        return `Every texture set of ${this.target()?.name ?? ''}`;
      default: {
        const asset = this.asset();
        return asset ? `Every project that reads ${asset.name}` : 'Every project in the library';
      }
    }
  });

  /**
   * The sets the dialog offers: the project's, for a project export of a project that is open and has more than one.
   * A document export is its own set, and a library export takes every project's.
   */
  protected readonly setNames = computed(() => {
    if (this.scope() !== 'project') {
      return [];
    }
    const open = this.project.current();
    const sets = open && open.id === this.target()?.id ? open.exportSets : this.keptSets();
    return sets.length > 1 ? sets : [];
  });

  protected readonly nothingChosen = computed(
    () =>
      (this.scope() === 'project' &&
        this.setNames().length > 0 &&
        this.setNames().every(set => this.settings().skipped.includes(set))) ||
      (this.chosen()?.files.every(file => !this.choiceOf(file).enabled) ?? false)
  );

  /** Documents of what is being exported that have changes the export will not see. */
  protected readonly unsaved = computed(() => {
    const tabs = this.project.unsavedTabs();
    if (this.scope() === 'library') {
      return tabs;
    }
    const path = this.project.current()?.path;
    return this.project.current()?.id === this.target()?.id ? tabs.filter(tab => tab.project === path) : [];
  });

  protected readonly unsavedText = computed(() => {
    const tabs = this.unsaved();
    const [only] = tabs;
    return tabs.length === 1 && only
      ? `${only.name} goes out without its unsaved changes`
      : `${tabs.length} documents go out without their unsaved changes`;
  });

  /** A folder Reveal can show: the one chosen, or where the last export put its files. */
  protected readonly revealable = computed(() => {
    const folder = this.settings().folder;
    if (folder) {
      return folder;
    }
    return (
      this.store
        .projects()
        .flatMap(done => done.exports)
        .find(written => written.folder)?.folder ?? null
    );
  });

  private readonly stack = computed(() => this.paint.state()?.id ?? null);

  /**
   * Whose settings are being edited: the project's, by path, or for a library export the library's, which belong to
   * no project. Null with neither, which keeps nothing.
   */
  private readonly settingsOwner = computed<ExportOwner | null>(
    () => {
      if (this.scope() === 'library') {
        return 'library';
      }
      const path = this.target()?.path;
      return path === undefined ? null : { project: path };
    },
    { equal: sameOwner }
  );

  /** A build that has stopped is read once, whichever way it stopped. */
  private read = '';
  /** Whose settings the dialog holds, which is the only owner a change may be written to. */
  private restored: ExportOwner | null = null;
  private saving: ReturnType<typeof setTimeout> | null = null;
  private pending: { owner: ExportOwner; settings: ExportSettings } | null = null;

  constructor() {
    // A change made just before the dialog closes is still kept.
    inject(DestroyRef).onDestroy(() => this.flush());
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
      this.scope();
      this.asset();
      this.settings();
      this.store.preview.set(null);
      this.confirming.set(false);
    });
    effect(() => {
      this.stack();
      this.store.exported.set([]);
    });

    // A project's export starts where its last one ended, from its project.yaml. The list of presets read again,
    // as it is after the preset editor, does not take the owner back to what it kept: that would undo a choice made
    // since, whose write is still on its way.
    effect(() => {
      const owner = this.settingsOwner();
      const presets = this.store.presets();
      if (presets.length > 0 && !(this.restored !== null && sameOwner(this.restored, owner))) {
        void untracked(() => this.attempt(() => this.restore(owner, presets)));
      }
    });
  }

  ngOnInit(): void {
    // The presets the project in front can use: the library's and its own.
    effect(
      () => {
        const project = this.target()?.path ?? null;
        void untracked(() => this.attempt(() => this.store.load(project)));
      },
      { injector: this.injector }
    );
  }

  /** The texture set a compression preview shows: the document's, or the first that is written. */
  protected readonly previewSet = computed(() => {
    if (this.scope() === 'document') {
      return this.documentSet();
    }
    return this.setNames().find(set => !this.settings().skipped.includes(set)) ?? null;
  });

  /** What an export would change of the preset, which a preview must show the file under. */
  protected readonly previewOptions = computed(() => this.request().options ?? null);

  protected originName(origin: string): string {
    return origin === 'factory' ? 'Starter' : origin === 'project' ? 'this project' : 'this library';
  }

  protected isContainer(format: string): boolean {
    return this.store.formats().find(known => known.id === format)?.container ?? false;
  }

  /** What a container file of this many lanes can hold. */
  protected compressionsFor(file: ExportPresetFile): string[] {
    const fits: Readonly<Record<string, readonly number[]>> = {
      bc1: [3],
      bc3: [3, 4],
      bc4: [1],
      bc5: [2],
      bc6h: [3],
      bc7: [3, 4]
    };
    return this.store
      .compressionTerms()
      .map(term => term.value)
      .filter(compression => fits[compression]?.includes(file.lanes) ?? true);
  }

  protected compressionOf(file: ExportPresetFile, choice: FileChoice): string {
    return choice.compression ?? (this.isContainer(file.format) ? file.compression : 'none');
  }

  protected searched(compression: string): boolean {
    return compression === 'bc6h' || compression === 'bc7';
  }

  protected qualityOf(file: ExportPresetFile, choice: FileChoice): string {
    const quality = choice.quality ?? file.quality;
    return this.searches.includes(quality) ? quality : 'balanced';
  }

  protected jpegQuality(file: ExportPresetFile, choice: FileChoice): number {
    const quality = Number(choice.quality ?? (file.format === 'jpeg' ? file.quality : '90'));
    return Number.isFinite(quality) ? quality : 90;
  }

  protected setSizeOf(set: string): string {
    const size = this.settings().perSet[set]?.size;
    return size ? String(size) : 'export';
  }

  protected chooseSetSize(set: string, size: string | null): void {
    const kept = Object.entries(this.settings().perSet).filter(([name]) => name !== set);
    const own = !size || size === 'export' ? [] : [[set, { size: Number(size) }] as const];
    this.change({ perSet: Object.fromEntries([...kept, ...own]) });
  }

  /** Back from the preset editor, choosing the preset it was left on. */
  protected async closeEditor(chosen: string | null): Promise<void> {
    this.editing.set(false);
    await this.attempt(() => this.store.load(this.target()?.path ?? null));
    if (chosen && chosen !== this.settings().preset) {
      this.choosePreset(chosen);
    }
  }

  protected greenName(normal: string): string {
    return normal === 'y-up' ? 'OpenGL (Y+)' : 'DirectX (Y−)';
  }

  protected statusName(status: string): string {
    return StatusNames[status] ?? status;
  }

  protected mapsOf(file: ExportPresetFile): string {
    return file.maps.map(termName).join(', ') || 'Constants';
  }

  protected choiceOf(file: ExportPresetFile): FileChoice {
    const preset = this.settings().preset ?? '';
    return this.settings().files[preset]?.[file.template] ?? { enabled: true, format: file.format };
  }

  /** The formats a file can be written in: those that hold its lane count. */
  protected formatsFor(file: ExportPresetFile): string[] {
    return this.store
      .formats()
      .filter(format => format.lanes.includes(file.lanes))
      .map(format => format.id);
  }

  protected chooseScope(scope: ExportScope | null): void {
    if (scope) {
      this.picked.set(scope);
    }
  }

  /** Makes the chosen preset the library's default, where every export that kept none starts (#240). */
  protected makeDefault(): Promise<void> {
    const chosen = this.chosen();
    return chosen ? this.attempt(() => this.store.keepDefault(chosen.id)) : Promise.resolve();
  }

  protected choosePreset(id: string | null): void {
    const preset = this.store.presets().find(known => known.id === id) ?? null;
    if (preset) {
      this.settings.set(settingsFor(preset, this.settings()));
      this.keep();
    }
  }

  protected chooseSize(size: string | null): void {
    this.change({ size: !size || size === 'native' ? null : Number(size) });
  }

  protected change(change: Partial<ExportSettings>): void {
    this.settings.update(settings => ({ ...settings, ...change }));
    this.keep();
  }

  protected chooseFile(file: ExportPresetFile, change: Partial<FileChoice>): void {
    const preset = this.settings().preset;
    if (!preset) {
      return;
    }
    const current = this.choiceOf(file);
    const reformatted = change.format !== undefined && change.format !== current.format;
    const next = { ...current, ...(reformatted ? { compression: null, quality: null, mips: null } : {}), ...change };
    this.settings.update(settings => ({
      ...settings,
      files: { ...settings.files, [preset]: { ...settings.files[preset], [file.template]: next } }
    }));
    this.keep();
  }

  protected toggleSet(set: string): void {
    const skipped = this.settings().skipped;
    this.change({ skipped: skipped.includes(set) ? skipped.filter(name => name !== set) : [...skipped, set] });
  }

  protected checkSets(how: 'all' | 'none' | 'invert'): void {
    const sets = this.setNames();
    const skipped = this.settings().skipped;
    this.change({
      skipped: how === 'all' ? [] : how === 'none' ? [...sets] : sets.filter(set => !skipped.includes(set))
    });
  }

  protected async chooseFolder(): Promise<void> {
    const folder = await desktop()?.openFolder();
    if (folder) {
      this.change({ folder });
    }
  }

  protected reveal(path: string): void {
    void desktop()?.reveal(path);
  }

  /**
   * Painted work reaches a file with no build behind it: the stack that is open, in a folder chosen now, as the PNGs the
   * screen shows or in another format (#47).
   */
  protected async exportStack(id: string): Promise<void> {
    const folder = await desktop()?.openFolder();
    const format = this.stackFormat();
    if (folder) {
      await this.attempt(() => this.store.exportStack(id, folder, format === this.onScreen ? null : format));
    }
  }

  protected ask(): Promise<void> {
    return this.attempt(() => this.store.ask(this.request()));
  }

  /** A library export asks first: it touches every project and replaces what they last exported. */
  protected start(): Promise<void> {
    if (this.scope() === 'library' && !this.asset()) {
      this.confirming.set(true);
      return Promise.resolve();
    }
    return this.run();
  }

  protected run(): Promise<void> {
    this.confirming.set(false);
    return this.attempt(() => this.store.start(this.request()));
  }

  protected cancel(): Promise<void> {
    return this.attempt(() => this.store.cancel());
  }

  private request(): BuildRequest {
    const preset = this.chosen();
    if (!preset) {
      return { presets: [] };
    }
    const target = this.target();
    switch (this.scope()) {
      case 'document': {
        const document = this.documentExport();
        return requestFor(this.settings(), preset, {
          projects: target ? [target.id] : [],
          sets: document?.file === null ? [document.set] : null,
          documents: document?.file ? [document.file] : null
        });
      }
      case 'project': {
        const sets = this.setNames();
        const skipped = this.settings().skipped.filter(set => sets.includes(set));
        return requestFor(this.settings(), preset, {
          projects: target ? [target.id] : [],
          sets: skipped.length > 0 ? sets.filter(set => !skipped.includes(set)) : null
        });
      }
      default:
        return requestFor(this.settings(), preset, { asset: this.asset()?.id ?? null });
    }
  }

  /**
   * Takes up what an owner's export was last set to. One that has kept nothing starts from the default preset alone:
   * nothing of another project's, or of the library's, carries into it (#227).
   */
  private async restore(owner: ExportOwner | null, presets: readonly ExportPresetInfo[]): Promise<void> {
    this.flush();
    this.restored = null;
    const read = owner ? await this.store.settings(owner) : null;
    if (!sameOwner(owner, this.settingsOwner())) {
      return; // Another project came to the front while this one was read.
    }
    const saved = read?.kept ?? null;
    this.keptSets.set(read?.sets ?? []);
    const preset =
      presets.find(known => known.id === saved?.preset) ??
      presets.find(known => known.id === this.store.defaultPreset()) ??
      presets[0];
    if (!preset) {
      return;
    }
    this.settings.set(saved ? settingsOf(saved, preset) : settingsFor(preset));
    this.restored = owner;
  }

  /**
   * Writes the settings to their owner's file, once the changes pause: a slider dragged across its range is one write
   * and not sixty. What is written is taken now, with its owner, so a write still waiting when another owner comes to
   * the front goes out at once with this owner's settings, not the next one's.
   */
  private keep(): void {
    const owner = this.restored;
    if (owner === null || !sameOwner(owner, this.settingsOwner())) {
      return;
    }
    this.pending = { owner, settings: this.settings() };
    if (this.saving) {
      clearTimeout(this.saving);
    }
    this.saving = setTimeout(() => this.flush(), SaveDelay);
  }

  /** Sends the write that is waiting, if one is. */
  private flush(): void {
    if (this.saving) {
      clearTimeout(this.saving);
      this.saving = null;
    }
    const pending = this.pending;
    this.pending = null;
    if (pending) {
      void this.attempt(() => this.store.keep(pending.owner, pending.settings));
    }
  }

  private async attempt(work: () => Promise<void>): Promise<void> {
    this.error.set(null);
    try {
      await work();
    } catch (error) {
      this.error.set(messageOf(error));
    }
  }
}
