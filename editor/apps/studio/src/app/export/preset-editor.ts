import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  type OnInit,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked
} from '@angular/core';
import { provideIcons } from '@ng-icons/core';
import { matArrowUpward, matClose } from '@ng-icons/material-icons/baseline';
import type {
  ExportPresetCheck,
  ExportPresetDocument,
  ExportPresetSource,
  ExportPresetTextureDocument
} from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiCheckboxImports } from '@xui/checkbox';
import { XuiInputImports } from '@xui/input';
import { XuiNumericInputImports } from '@xui/numeric-input';
import { XuiSegmentedControlImports, type XuiSegmentedOption } from '@xui/segmented-control';
import { XuiSelectImports } from '@xui/select';
import { XuiSwitchImports } from '@xui/switch';
import { desktop } from '../core/desktop';
import { IconImports } from '../core/icons';
import { labelIn } from '../core/labels';
import { messageOf } from '../core/messages';
import { SystemStatus } from '../core/system-status';
import { ExportStore } from './export-store';

const CheckDelay = 250;
const LaneNames = ['r', 'g', 'b', 'a'] as const;

/** One lane of a file as the editor shows it: a lane of a map, or one minus it, or a number. */
export type Lane = { kind: 'map'; map: string; lane: number; inverted: boolean } | { kind: 'constant'; value: number };

/** A lane as the preset language writes it (ADR 0019), read: `roughness.r`, `1-roughness.r` or `0.5`. */
export function laneOf(text: string): Lane {
  const read = /^(1-)?([A-Za-z]+)(?:\.([rgba]))?$/u.exec(text.trim());
  if (read?.[2]) {
    return { kind: 'map', map: read[2], lane: LaneNames.indexOf((read[3] ?? 'r') as 'r'), inverted: !!read[1] };
  }
  const value = Number(text);
  return { kind: 'constant', value: Number.isFinite(value) ? value : 0 };
}

/** A lane as the preset language writes it. */
export function textOf(lane: Lane): string {
  return lane.kind === 'map'
    ? `${lane.inverted ? '1-' : ''}${lane.map}.${LaneNames[lane.lane] ?? 'r'}`
    : String(Math.round(lane.value * 1000) / 1000);
}

/** A preset nobody has written yet: one base colour file. */
function blank(id: string): ExportPresetDocument {
  return {
    id,
    name: 'New Preset',
    engine: '',
    normal: 'y-up',
    filter: 'box',
    dither: false,
    padding: { mode: 'infinite', distance: 0, colour: [] },
    materials: [],
    textures: [
      {
        file: '{project}_{set}_baseColor',
        format: 'png8',
        space: 'srgb',
        compression: 'none',
        mips: false,
        scale: 1,
        channels: ['baseColor.r', 'baseColor.g', 'baseColor.b']
      }
    ]
  };
}

/**
 * The preset editor (#155, ADR 0019). A preset is a file in the library's or a project's export-presets folder, so
 * this edits a document and the Server writes the file, through the same reader a hand-written one goes through:
 * nothing kept here is a preset a file could not say. A built-in preset is read-only and is changed by duplicating it.
 * While a preset is edited, the Server checks it and names every file it would write for the project in front.
 */
@Component({
  selector: 'ptn-preset-editor',
  imports: [
    IconImports,
    XuiButtonImports,
    XuiCalloutImports,
    XuiCheckboxImports,
    XuiInputImports,
    XuiNumericInputImports,
    XuiSegmentedControlImports,
    XuiSelectImports,
    XuiSwitchImports
  ],
  viewProviders: [provideIcons({ matArrowUpward, matClose })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex min-h-0 flex-col', 'data-testid': 'preset-editor' },
  template: `
    <div class="grid min-h-0 flex-1 grid-cols-[14rem_minmax(0,1fr)] gap-3 overflow-hidden p-3">
      <div class="flex min-h-0 flex-col gap-2">
        <div class="well flex min-h-0 flex-1 flex-col overflow-y-auto p-1" data-testid="preset-list">
          @for (preset of store.presets(); track preset.id) {
            <button
              type="button"
              class="flex items-center gap-2 rounded-sm px-2 py-1 text-left text-xs"
              data-testid="preset-item"
              [class.bg-surface-raised]="preset.id === selected()"
              [attr.aria-pressed]="preset.id === selected()"
              (click)="select(preset.id)"
            >
              <span class="min-w-0 flex-1 truncate">{{ preset.name }}</span>
              <span class="text-foreground-muted shrink-0">{{ originName(preset.origin) }}</span>
            </button>
          }
          @if (creating()) {
            <span class="bg-surface-raised rounded-sm px-2 py-1 text-xs" data-testid="preset-item-new">
              {{ draft()?.name }} (not kept yet)
            </span>
          }
        </div>
        <div class="flex flex-wrap gap-1">
          <button type="button" xuiButton size="sm" data-testid="preset-new" (click)="create()">New</button>
          <button
            type="button"
            xuiButton
            size="sm"
            data-testid="preset-duplicate"
            [disabled]="!draft()"
            (click)="duplicate()"
          >
            Duplicate
          </button>
          <button
            type="button"
            xuiButton
            size="sm"
            variant="ghost"
            data-testid="preset-remove"
            [disabled]="!removable()"
            (click)="removing.set(true)"
          >
            Delete
          </button>
          <button type="button" xuiButton size="sm" variant="ghost" data-testid="preset-bring-in" (click)="bringIn()">
            Bring In…
          </button>
          @if (source()?.file; as file) {
            <button
              type="button"
              xuiButton
              size="sm"
              variant="ghost"
              data-testid="preset-reveal"
              title="Show the preset's file, to share it"
              (click)="reveal(file)"
            >
              Reveal File
            </button>
          }
        </div>
        @if (removing()) {
          <div class="well flex flex-col gap-2 p-2 text-xs" data-testid="preset-remove-confirm">
            <span>Delete {{ draft()?.name }}? Its file is deleted, and an export that names it can no longer run.</span>
            <div class="flex gap-1">
              <button
                type="button"
                xuiButton
                size="sm"
                color="error"
                data-testid="preset-remove-yes"
                (click)="remove()"
              >
                Delete
              </button>
              <button type="button" xuiButton size="sm" variant="ghost" (click)="removing.set(false)">Cancel</button>
            </div>
          </div>
        }
        <button
          type="button"
          xuiButton
          size="sm"
          variant="ghost"
          data-testid="preset-editor-close"
          (click)="closed.emit(selected())"
        >
          Back to Export
        </button>
      </div>

      @if (draft(); as preset) {
        <div class="flex min-h-0 flex-col gap-2.5 overflow-y-auto pr-1" data-testid="preset-form">
          @if (readOnly()) {
            <xui-callout color="primary" compact data-testid="preset-built-in">
              This is a Starter preset. Duplicate it to change a copy.
            </xui-callout>
          }

          <div class="grid grid-cols-[6rem_minmax(0,1fr)] items-center gap-x-2 gap-y-1.5 text-xs">
            <span class="text-foreground-muted">Name</span>
            <input
              xuiInput
              class="w-full"
              aria-label="Preset name"
              data-testid="preset-editor-name"
              [disabled]="readOnly()"
              [value]="preset.name"
              (input)="edit({ name: text($event) })"
            />
            <span class="text-foreground-muted">Id</span>
            <input
              xuiInput
              class="readout w-full"
              aria-label="Preset id"
              title="Lower-case letters, digits and hyphens: the file's name, and what an export names it by"
              data-testid="preset-id"
              [disabled]="readOnly()"
              [value]="preset.id"
              (input)="edit({ id: text($event) })"
            />
            <span class="text-foreground-muted">For</span>
            <input
              xuiInput
              class="w-full"
              aria-label="What it is for"
              data-testid="preset-engine"
              [disabled]="readOnly()"
              [value]="preset.engine"
              (input)="edit({ engine: text($event) })"
            />
            @if (project()) {
              <span class="text-foreground-muted">Kept in</span>
              <xui-segmented-control
                size="sm"
                aria-label="Where the preset is kept"
                data-testid="preset-keep"
                [disabled]="readOnly()"
                [options]="places()"
                [value]="keepIn()"
                (valueChange)="keepIn.set($event ?? 'library')"
              />
            }
            <span class="text-foreground-muted">Normal map</span>
            <xui-select
              aria-label="Normal map green"
              data-testid="preset-normal"
              [disabled]="readOnly()"
              [filterable]="false"
              [itemText]="greenName"
              [items]="greens"
              [value]="preset.normal"
              (valueChange)="edit({ normal: $event ?? 'y-up' })"
            />
            <span class="text-foreground-muted">Size</span>
            <xui-select
              aria-label="Preset size"
              data-testid="preset-size"
              [disabled]="readOnly()"
              [filterable]="false"
              [itemText]="sizeName"
              [items]="sizes()"
              [value]="preset.size ? '' + preset.size : 'native'"
              (valueChange)="edit({ size: !$event || $event === 'native' ? null : +$event })"
            />
            <span class="text-foreground-muted">Padding</span>
            <xui-select
              aria-label="Preset padding"
              data-testid="preset-padding"
              [disabled]="readOnly()"
              [filterable]="false"
              [itemText]="paddingName"
              [items]="store.paddings()"
              [value]="preset.padding.mode"
              (valueChange)="edit({ padding: { mode: $event ?? 'none', distance: 16, colour: [] } })"
            />
            <span class="text-foreground-muted">Dither</span>
            <xui-switch
              aria-label="Dither 8-bit files"
              data-testid="preset-dither"
              [disabled]="readOnly()"
              [checked]="preset.dither"
              (checkedChange)="edit({ dither: $event })"
            />
            <span class="text-foreground-muted">Material files</span>
            <span class="flex flex-wrap gap-3">
              @for (kind of store.materialTerms(); track kind.value) {
                <xui-checkbox
                  [attr.data-testid]="'preset-material-' + kind.value"
                  [disabled]="readOnly()"
                  [checked]="preset.materials.includes(kind.value)"
                  (checkedChange)="toggleMaterial(kind.value)"
                >
                  {{ kind.label }}
                </xui-checkbox>
              }
            </span>
          </div>

          <div class="text-foreground-muted flex flex-wrap items-center gap-1 text-xs" data-testid="preset-tokens">
            <span>Names a file can hold:</span>
            @for (token of store.tokens(); track token) {
              <button
                type="button"
                class="readout rounded-sm px-1"
                [disabled]="readOnly()"
                [title]="'Put {' + token + '} in the last file name you were in'"
                (click)="insertToken(token)"
              >
                {{ '{' + token + '}' }}
              </button>
            }
          </div>

          @for (texture of preset.textures; track $index; let at = $index) {
            <section class="well flex flex-col gap-1.5 p-2 text-xs" data-testid="preset-file">
              <div class="flex items-center gap-1.5">
                <input
                  xuiInput
                  class="readout min-w-0 flex-1"
                  data-testid="preset-file-name"
                  [attr.aria-label]="'Name of file ' + (at + 1)"
                  [disabled]="readOnly()"
                  [value]="texture.file"
                  (focus)="lastFile = at"
                  (input)="editFile(at, { file: text($event) })"
                />
                <button
                  type="button"
                  xuiButton
                  size="sm"
                  variant="ghost"
                  title="Move up"
                  [attr.aria-label]="'Move file ' + (at + 1) + ' up'"
                  [disabled]="readOnly() || at === 0"
                  (click)="moveFile(at, -1)"
                >
                  <ng-icon xui size="sm" name="matArrowUpward" />
                </button>
                <button
                  type="button"
                  xuiButton
                  size="sm"
                  variant="ghost"
                  data-testid="preset-file-remove"
                  [disabled]="readOnly() || preset.textures.length === 1"
                  (click)="removeFile(at)"
                >
                  Remove
                </button>
              </div>
              <div class="flex flex-wrap items-center gap-1.5">
                <xui-select
                  class="w-40"
                  data-testid="preset-file-format"
                  [aria-label]="'Format of file ' + (at + 1)"
                  [disabled]="readOnly()"
                  [filterable]="false"
                  [itemText]="formatName"
                  [items]="formatIds()"
                  [value]="texture.format"
                  (valueChange)="reformat(at, $event)"
                />
                <xui-select
                  class="w-24"
                  data-testid="preset-file-space"
                  [aria-label]="'Numbers of file ' + (at + 1)"
                  [disabled]="readOnly()"
                  [filterable]="false"
                  [itemText]="spaceName"
                  [items]="spaces"
                  [value]="texture.space"
                  (valueChange)="editFile(at, { space: $event ?? 'linear' })"
                />
                @if (isContainer(texture.format)) {
                  <xui-select
                    class="w-36"
                    data-testid="preset-file-compression"
                    [aria-label]="'Compression of file ' + (at + 1)"
                    [disabled]="readOnly()"
                    [filterable]="false"
                    [itemText]="compressionName"
                    [items]="compressions()"
                    [value]="texture.compression"
                    (valueChange)="editFile(at, { compression: $event ?? 'none', quality: null })"
                  />
                  <xui-checkbox
                    [aria-label]="'Mips of file ' + (at + 1)"
                    [disabled]="readOnly()"
                    [checked]="texture.mips"
                    (checkedChange)="editFile(at, { mips: !texture.mips })"
                  >
                    Mips
                  </xui-checkbox>
                }
                <xui-select
                  class="w-28"
                  data-testid="preset-file-scale"
                  [aria-label]="'Scale of file ' + (at + 1)"
                  [disabled]="readOnly()"
                  [filterable]="false"
                  [itemText]="scaleName"
                  [items]="scales"
                  [value]="'' + texture.scale"
                  (valueChange)="editFile(at, { scale: +($event ?? 1) })"
                />
                <xui-select
                  class="w-32"
                  data-testid="preset-file-dither"
                  [aria-label]="'Dither of file ' + (at + 1)"
                  [disabled]="readOnly()"
                  [filterable]="false"
                  [itemText]="ditherName"
                  [items]="dithers"
                  [value]="texture.dither === true ? 'on' : texture.dither === false ? 'off' : 'preset'"
                  (valueChange)="editFile(at, { dither: $event === 'on' ? true : $event === 'off' ? false : null })"
                />
              </div>
              <div class="flex flex-wrap items-stretch gap-1.5" data-testid="preset-lanes">
                @for (channel of texture.channels; track $index; let lane = $index) {
                  @let read = laneOf(channel);
                  <div
                    class="bg-surface-sunken flex items-center gap-1 rounded-sm p-1"
                    data-testid="preset-lane"
                    [attr.draggable]="!readOnly()"
                    [title]="'Lane ' + laneLabel(lane) + ': drag onto another lane to swap them'"
                    (dragstart)="dragging = { file: at, lane }"
                    (dragover)="$event.preventDefault()"
                    (drop)="dropLane(at, lane)"
                  >
                    <span class="readout w-3 text-center">{{ laneLabel(lane) }}</span>
                    <xui-select
                      class="w-32"
                      data-testid="preset-lane-map"
                      [aria-label]="'Lane ' + laneNames[lane] + ' of file ' + (at + 1)"
                      [disabled]="readOnly()"
                      [filterable]="true"
                      [itemText]="mapName"
                      [items]="mapChoices()"
                      [value]="read.kind === 'map' ? read.map : 'constant'"
                      (valueChange)="
                        setLane(
                          at,
                          lane,
                          $event === 'constant' || !$event
                            ? { kind: 'constant', value: 0 }
                            : { kind: 'map', map: $event, lane: 0, inverted: false }
                        )
                      "
                    />
                    @if (read.kind === 'map') {
                      <xui-select
                        class="w-14"
                        [aria-label]="'Which lane of ' + read.map"
                        [disabled]="readOnly()"
                        [filterable]="false"
                        [items]="laneNames"
                        [value]="laneNames[read.lane]"
                        (valueChange)="setLane(at, lane, { ...read, lane: laneNames.indexOf($event ?? 'r') })"
                      />
                      <xui-checkbox
                        title="One minus the map: smoothness from roughness"
                        [aria-label]="'Invert lane ' + laneNames[lane] + ' of file ' + (at + 1)"
                        [disabled]="readOnly()"
                        [checked]="read.inverted"
                        (checkedChange)="setLane(at, lane, { ...read, inverted: !read.inverted })"
                      >
                        1 −
                      </xui-checkbox>
                    } @else {
                      <xui-numeric-input
                        class="w-20"
                        size="sm"
                        [aria-label]="'Value of lane ' + laneNames[lane] + ' of file ' + (at + 1)"
                        [disabled]="readOnly()"
                        [min]="0"
                        [max]="1"
                        [stepSize]="0.05"
                        [value]="read.value"
                        (valueChange)="setLane(at, lane, { kind: 'constant', value: $event ?? 0 })"
                      />
                    }
                    @if (texture.channels.length > 1) {
                      <button
                        type="button"
                        class="text-foreground-muted px-0.5"
                        [attr.aria-label]="'Remove lane ' + laneNames[lane] + ' of file ' + (at + 1)"
                        [title]="'Remove lane ' + laneNames[lane] + ' of file ' + (at + 1)"
                        [disabled]="readOnly()"
                        (click)="removeLane(at, lane)"
                      >
                        <ng-icon xui size="sm" name="matClose" />
                      </button>
                    }
                  </div>
                }
                @if (texture.channels.length < 4) {
                  <button
                    type="button"
                    xuiButton
                    size="sm"
                    variant="ghost"
                    data-testid="preset-lane-add"
                    [disabled]="readOnly()"
                    (click)="addLane(at)"
                  >
                    Add Lane
                  </button>
                }
              </div>
            </section>
          }
          <button
            type="button"
            xuiButton
            size="sm"
            class="self-start"
            data-testid="preset-file-add"
            [disabled]="readOnly()"
            (click)="addFile()"
          >
            Add File
          </button>

          @if (checked(); as check) {
            @if (check.problem; as problem) {
              <xui-callout color="warning" compact data-testid="preset-problem">{{ problem }}</xui-callout>
            } @else {
              <section class="well flex flex-col gap-0.5 p-2 text-xs" data-testid="preset-files-written">
                <span class="text-foreground-muted font-medium">
                  {{ projectName() ? 'What it writes for ' + projectName() : 'What it writes' }}
                </span>
                @for (file of check.files; track file.set + file.template) {
                  <span class="readout truncate" data-testid="preset-file-written" [title]="file.name">{{
                    file.name
                  }}</span>
                }
              </section>
            }
            <details class="text-xs">
              <summary class="text-foreground-muted cursor-pointer">The file</summary>
              <pre class="readout well mt-1 overflow-x-auto p-2" data-testid="preset-text">{{ check.text }}</pre>
            </details>
          }

          @if (error(); as message) {
            <xui-callout color="error" compact data-testid="preset-error">{{ message }}</xui-callout>
          }

          @if (!readOnly()) {
            <div class="flex gap-1">
              <button
                type="button"
                xuiButton
                size="sm"
                color="primary"
                data-testid="preset-editor-save"
                [disabled]="!!checked()?.problem"
                (click)="save()"
              >
                Keep
              </button>
              @if (creating() || changed()) {
                <button type="button" xuiButton size="sm" variant="ghost" (click)="revert()">Discard Changes</button>
              }
            </div>
          }
        </div>
      }
    </div>
  `
})
export class PresetEditor implements OnInit {
  protected readonly store = inject(ExportStore);
  private readonly status = inject(SystemStatus);

  /** The project in front, below projects/, whose presets are listed with the library's and whose files are named. */
  readonly project = input<string | null>(null);
  readonly projectName = input<string | null>(null);
  /** The preset to start on. */
  readonly start = input<string | null>(null);
  /** Back to the export, with the preset left selected. */
  readonly closed = output<string | null>();

  protected readonly selected = signal<string | null>(null);
  protected readonly source = signal<ExportPresetSource | null>(null);
  protected readonly draft = signal<ExportPresetDocument | null>(null);
  protected readonly checked = signal<ExportPresetCheck | null>(null);
  protected readonly creating = signal(false);
  protected readonly keepIn = signal<'library' | 'project'>('library');
  protected readonly removing = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly laneOf = laneOf;
  protected readonly laneNames = LaneNames;
  protected readonly greens = ['y-up', 'y-down'];
  protected readonly spaces = ['srgb', 'linear'];
  protected readonly scales = ['1', '0.5', '0.25', '0.125', '0.0625'];
  protected readonly dithers = ['preset', 'on', 'off'];

  /** The last file name a person was in, which a name chip goes into. */
  protected lastFile = 0;
  /** The lane being dragged. */
  protected dragging: { file: number; lane: number } | null = null;

  protected readonly readOnly = computed(() => !this.creating() && this.source()?.origin === 'factory');
  protected readonly removable = computed(() => !this.creating() && !!this.source() && !this.readOnly());
  protected readonly changed = computed(
    () => JSON.stringify(this.draft()) !== JSON.stringify(this.source()?.document ?? null)
  );
  protected readonly places = computed<XuiSegmentedOption<'library' | 'project'>[]>(() => [
    { value: 'library', label: 'Library' },
    { value: 'project', label: this.projectName() ?? 'Project' }
  ]);
  protected readonly sizes = computed(() => ['native', ...this.status.textureSides().map(String)]);
  protected readonly formatIds = computed(() => this.store.formats().map(format => format.id));
  protected readonly compressions = computed(() => this.store.compressionTerms().map(term => term.value));
  protected readonly mapChoices = computed(() => [...this.store.mapTerms().map(term => term.value), 'constant']);

  protected readonly greenName = (green: string): string => (green === 'y-up' ? 'OpenGL (Y+)' : 'DirectX (Y−)');
  protected readonly sizeName = (size: string): string => (size === 'native' ? 'Maps’ size' : `${size} px`);
  protected readonly paddingName = (mode: string): string => labelIn(this.store.paddingTerms(), mode);
  protected readonly formatName = (format: string): string =>
    this.store.formats().find(known => known.id === format)?.label ?? format;
  protected readonly compressionName = (compression: string): string =>
    labelIn(this.store.compressionTerms(), compression);
  protected readonly spaceName = (space: string): string => (space === 'srgb' ? 'sRGB' : 'Linear');
  protected readonly scaleName = (scale: string): string =>
    ({ '1': 'Full size', '0.5': 'Half', '0.25': 'Quarter', '0.125': 'Eighth', '0.0625': 'Sixteenth' })[scale] ?? scale;
  protected readonly ditherName = (dither: string): string =>
    ({ preset: 'Preset’s dither', on: 'Dithered', off: 'Rounded' })[dither] ?? dither;
  protected readonly mapName = (map: string): string =>
    map === 'constant' ? 'A number' : labelIn(this.store.mapTerms(), map);

  private checking: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      if (this.checking) {
        clearTimeout(this.checking);
      }
    });
    // The Server checks what is being edited once typing pauses, and names the files it would write.
    effect(() => {
      const draft = this.draft();
      const project = this.project();
      untracked(() => this.schedule(draft, project));
    });
  }

  ngOnInit(): void {
    const start = this.start() ?? this.store.presets()[0]?.id ?? null;
    if (start) {
      void this.select(start);
    }
  }

  protected originName(origin: string): string {
    return origin === 'factory' ? 'Starter' : origin === 'project' ? 'project' : 'library';
  }

  protected laneLabel(lane: number): string {
    return (LaneNames[lane] ?? '').toUpperCase();
  }

  protected text(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  protected isContainer(format: string): boolean {
    return this.store.formats().find(known => known.id === format)?.container ?? false;
  }

  protected async select(id: string): Promise<void> {
    await this.attempt(async () => {
      const source = await this.store.preset(id, this.project());
      this.selected.set(id);
      this.source.set(source);
      this.draft.set(source.document);
      this.creating.set(false);
      this.removing.set(false);
      this.keepIn.set(source.origin === 'project' ? 'project' : 'library');
    });
  }

  protected create(): void {
    this.startNew(blank(this.freeId('new-preset')));
  }

  /** A copy of what is shown, under a new id, kept where the project's presets are when there is a project. */
  protected duplicate(): void {
    const draft = this.draft();
    if (draft) {
      this.startNew({ ...draft, id: this.freeId(`${draft.id}-copy`), name: `${draft.name} Copy` });
    }
  }

  protected edit(change: Partial<ExportPresetDocument>): void {
    this.draft.update(draft => (draft ? { ...draft, ...change } : draft));
  }

  protected editFile(at: number, change: Partial<ExportPresetTextureDocument>): void {
    this.draft.update(draft =>
      draft
        ? {
            ...draft,
            textures: draft.textures.map((texture, index) => (index === at ? { ...texture, ...change } : texture))
          }
        : draft
    );
  }

  /** Another format: a plain file holds no blocks or mips, and a float or 16-bit one holds linear numbers. */
  protected reformat(at: number, format: string | null): void {
    if (!format) {
      return;
    }
    const container = this.isContainer(format);
    const linear = this.store.formats().find(known => known.id === format)?.linear ?? false;
    const texture = this.draft()?.textures[at];
    this.editFile(at, {
      format,
      compression: container ? (texture?.compression ?? 'none') : 'none',
      mips: container && (texture?.mips ?? false),
      quality: null,
      ...(linear ? { space: 'linear' } : {})
    });
  }

  protected moveFile(at: number, by: number): void {
    this.draft.update(draft => {
      if (!draft) {
        return draft;
      }
      const textures = [...draft.textures];
      const [moved] = textures.splice(at, 1);
      if (moved) {
        textures.splice(at + by, 0, moved);
      }
      return { ...draft, textures };
    });
  }

  protected removeFile(at: number): void {
    this.draft.update(draft =>
      draft ? { ...draft, textures: draft.textures.filter((_, index) => index !== at) } : draft
    );
  }

  protected addFile(): void {
    this.draft.update(draft =>
      draft
        ? {
            ...draft,
            textures: [
              ...draft.textures,
              {
                file: '{project}_{set}_roughness',
                format: 'png8',
                space: 'linear',
                compression: 'none',
                mips: false,
                scale: 1,
                channels: ['roughness.r']
              }
            ]
          }
        : draft
    );
  }

  protected setLane(at: number, lane: number, value: Lane): void {
    const texture = this.draft()?.textures[at];
    if (texture) {
      this.editFile(at, {
        channels: texture.channels.map((channel, index) => (index === lane ? textOf(value) : channel))
      });
    }
  }

  protected addLane(at: number): void {
    const texture = this.draft()?.textures[at];
    if (texture && texture.channels.length < 4) {
      this.editFile(at, { channels: [...texture.channels, '1'] });
    }
  }

  protected removeLane(at: number, lane: number): void {
    const texture = this.draft()?.textures[at];
    if (texture) {
      this.editFile(at, { channels: texture.channels.filter((_, index) => index !== lane) });
    }
  }

  /** A lane dropped on another of the same file trades places with it, which is how a file is packed. */
  protected dropLane(at: number, lane: number): void {
    const from = this.dragging;
    this.dragging = null;
    const texture = this.draft()?.textures[at];
    if (!from || !texture || from.file !== at || from.lane === lane) {
      return;
    }
    const channels = [...texture.channels];
    const moved = channels[from.lane];
    const there = channels[lane];
    if (moved !== undefined && there !== undefined) {
      channels[from.lane] = there;
      channels[lane] = moved;
      this.editFile(at, { channels });
    }
  }

  protected toggleMaterial(kind: string): void {
    const materials = this.draft()?.materials ?? [];
    this.edit({
      materials: materials.includes(kind) ? materials.filter(known => known !== kind) : [...materials, kind]
    });
  }

  protected insertToken(token: string): void {
    const texture = this.draft()?.textures[this.lastFile];
    if (texture) {
      this.editFile(this.lastFile, { file: `${texture.file}{${token}}` });
    }
  }

  protected revert(): void {
    const source = this.source();
    if (this.creating() && this.selected()) {
      void this.select(this.selected() as string);
    } else {
      this.draft.set(source?.document ?? null);
    }
  }

  protected async save(): Promise<void> {
    const draft = this.draft();
    if (!draft) {
      return;
    }
    await this.attempt(async () => {
      const project = this.keepIn() === 'project' ? this.project() : null;
      const before = this.source();
      const samePlace = !this.creating() && before && (before.origin === 'project') === (this.keepIn() === 'project');
      const replaces = samePlace && before.document.id !== draft.id ? before.document.id : null;
      const kept = await this.store.save(draft, project, replaces);
      await this.store.load(this.project());
      this.selected.set(kept.document.id);
      this.source.set(kept);
      this.draft.set(kept.document);
      this.creating.set(false);
    });
  }

  protected async remove(): Promise<void> {
    const source = this.source();
    if (!source) {
      return;
    }
    await this.attempt(async () => {
      await this.store.remove(source.document.id, source.origin === 'project' ? this.project() : null);
      this.removing.set(false);
      await this.store.load(this.project());
      const next = this.store.presets()[0]?.id;
      if (next) {
        await this.select(next);
      }
    });
  }

  protected async bringIn(): Promise<void> {
    const file = await desktop()?.openPreset();
    if (!file) {
      return;
    }
    await this.attempt(async () => {
      const kept = await this.store.bringIn(file, this.keepIn() === 'project' ? this.project() : null);
      await this.store.load(this.project());
      await this.select(kept.document.id);
    });
  }

  protected reveal(file: string): void {
    void desktop()?.reveal(file);
  }

  private startNew(document: ExportPresetDocument): void {
    this.creating.set(true);
    this.removing.set(false);
    this.keepIn.set(this.project() ? 'project' : 'library');
    this.draft.set(document);
  }

  /** An id no listed preset has, from a wish: lower-case, hyphens, a number on the end if it is taken. */
  private freeId(wish: string): string {
    const base =
      wish
        .toLowerCase()
        .replace(/[^a-z0-9-]+/gu, '-')
        .replace(/^-+|-+$/gu, '') || 'preset';
    const taken = new Set(this.store.presets().map(preset => preset.id));
    let id = base;
    for (let count = 2; taken.has(id); count++) {
      id = `${base}-${count}`;
    }
    return id;
  }

  private schedule(draft: ExportPresetDocument | null, project: string | null): void {
    if (this.checking) {
      clearTimeout(this.checking);
    }
    if (!draft) {
      this.checked.set(null);
      return;
    }
    this.checking = setTimeout(() => {
      this.checking = null;
      void this.attempt(async () => {
        const check = await this.store.check(draft, project);
        if (this.draft() === draft) {
          this.checked.set(check);
        }
      });
    }, CheckDelay);
  }

  private async attempt(work: () => Promise<void>): Promise<void> {
    this.error.set(null);
    try {
      await work();
    } catch (failure) {
      this.error.set(messageOf(failure));
    }
  }
}
