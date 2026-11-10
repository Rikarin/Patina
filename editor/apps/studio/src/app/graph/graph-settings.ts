import { ChangeDetectionStrategy, Component, computed, inject, input, linkedSignal, signal } from '@angular/core';
import type {
  GraphInterfacePort,
  GraphPresetState,
  GraphSettingCondition,
  GraphSettingDefinition
} from '@patina/api-client';
import { AngleDial } from '@patina/studio-ui';
import { XuiButtonImports } from '@xui/button';
import { XuiInputImports } from '@xui/input';
import { XuiNumericInputImports } from '@xui/numeric-input';
import { XuiSelectImports } from '@xui/select';
import { XuiSliderImports } from '@xui/slider';
import { XuiSwitchImports } from '@xui/switch';
import { Commands } from '../core/commands';
import { choiceName, kindName } from '../core/labels';
import { SystemStatus } from '../core/system-status';
import { ProjectStore } from '../library/project-store';
import { FrameColours, frameColourName } from './frame-colours';
import { controlOf, freePresetName, grouped, holds, titleOf, widgetsFor } from './graph-authoring';
import { GraphStore } from './graph-store';

const Kinds = ['Float', 'Int', 'Bool', 'Text'];
const BitDepths = ['16', '32'];
const PortKinds = ['Image', 'Float', 'Int', 'Bool', 'Float2', 'Float3', 'Float4'];
const DefaultBitDepth = '16';
const NoBake = '';
const MeshMap = 'Source/Mesh Map';
const SliderSteps = 1000;
/** What a graph can be used as, which decides where the shelf puts it. */
const Roles = [
  { value: 'material', label: 'Material' },
  { value: 'mask', label: 'Mask' },
  { value: 'brush', label: 'Brush' },
  { value: 'filter', label: 'Filter' }
];

/** What a kind's default starts as, which the Server has to accept. */
function startOf(kind: string): string {
  switch (kind) {
    case 'Bool':
      return 'false';
    case 'Text':
      return '';
    default:
      return '0';
  }
}

/** The first name of the form the graph does not use yet. */
export function freeParameterName(taken: readonly GraphSettingDefinition[]): string {
  const names = new Set(taken.map(parameter => parameter.name));
  let number = names.size + 1;
  while (names.has(`Parameter${String(number)}`)) {
    number++;
  }

  return `Parameter${String(number)}`;
}

/** The first port name of the form the graph does not use yet, on one side. */
export function freePortName(taken: readonly GraphInterfacePort[], direction: string): string {
  const side = direction === 'Input' ? 'Input' : 'Output';
  const names = new Set(taken.filter(port => port.direction === direction).map(port => port.name));
  let number = names.size + 1;
  while (names.has(`${side}${String(number)}`)) {
    number++;
  }

  return `${side}${String(number)}`;
}

/** The graph itself in the Inspector: its size and seed, what it exposes, and its frames and notes. */
@Component({
  selector: 'ptn-graph-settings',
  imports: [
    AngleDial,
    XuiButtonImports,
    XuiInputImports,
    XuiNumericInputImports,
    XuiSelectImports,
    XuiSliderImports,
    XuiSwitchImports
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block h-full overflow-auto p-3',
    '(pointerup)': 'store.endGesture()',
    '(focusout)': 'store.endGesture()'
  },
  template: `
    @let document = store.document();
    <h2 class="truncate text-sm font-semibold" data-testid="inspector-title">{{ document?.name }}</h2>
    <p class="section-label mb-3">Graph</p>

    <div class="flex flex-col gap-2.5">
      @if (listed(); as listing) {
        <div class="flex flex-col gap-1" data-testid="setting-role">
          <span class="text-foreground-muted text-xs">Use as</span>
          <xui-select
            ariaLabel="Use as"
            title="Where the shelf offers it: a material fills a layer, a mask decides where one shows, a brush is stamped, and a filter changes what is under it."
            [filterable]="false"
            [itemText]="roleName"
            [items]="roles"
            [value]="listing.role ?? 'material'"
            (valueChange)="setRole($event)"
          />
        </div>
      }
      <div class="flex flex-col gap-1" data-testid="setting-size">
        <span class="text-foreground-muted text-xs">Size</span>
        <xui-select
          ariaLabel="Size"
          [disabled]="!store.editable()"
          [items]="sidesHolding(size())"
          [value]="size()"
          (valueChange)="setSide('size', $event)"
        />
      </div>
      <div class="flex flex-col gap-1" data-testid="setting-bitDepth">
        <span class="text-foreground-muted text-xs">Bit depth</span>
        <xui-select
          ariaLabel="Bit depth"
          [disabled]="!store.editable()"
          [items]="bitDepths"
          [value]="bitDepth()"
          (valueChange)="setBitDepth($event)"
        />
      </div>
      @if (bakes().length > 0) {
        <div class="flex flex-col gap-1" data-testid="setting-bake">
          <span class="text-foreground-muted text-xs">Bake</span>
          <xui-select
            ariaLabel="Bake"
            [disabled]="!store.editable()"
            [itemText]="bakeName"
            [items]="bakes()"
            [value]="bake()"
            (valueChange)="chooseBake($event)"
          />
        </div>
      }
      <div class="flex items-center justify-between gap-2" data-testid="setting-tilingX">
        <span class="text-foreground-muted text-xs">Tile across</span>
        <xui-switch
          ariaLabel="Tile across"
          [checked]="tiles('tilingX')"
          [disabled]="!store.editable()"
          (checkedChange)="store.setSetting('tilingX', String($event))"
        />
      </div>
      <div class="flex items-center justify-between gap-2" data-testid="setting-tilingY">
        <span class="text-foreground-muted text-xs">Tile down</span>
        <xui-switch
          ariaLabel="Tile down"
          [checked]="tiles('tilingY')"
          [disabled]="!store.editable()"
          (checkedChange)="store.setSetting('tilingY', String($event))"
        />
      </div>
      <div class="flex flex-col gap-1" data-testid="setting-seed">
        <span class="text-foreground-muted text-xs">Seed</span>
        <xui-numeric-input
          size="sm"
          ariaLabel="Seed"
          [disabled]="!store.editable()"
          [min]="0"
          [stepSize]="1"
          [value]="seed()"
          (valueChange)="setSeed($event)"
        />
      </div>
    </div>

    <p class="section-label mt-4 mb-1">Authored at</p>
    <div class="flex flex-col gap-2.5">
      <div class="flex flex-col gap-1" data-testid="setting-baseWidth">
        <span class="text-foreground-muted text-xs">Width</span>
        <xui-select
          ariaLabel="Width"
          [disabled]="!store.editable()"
          [items]="sidesHolding(settingOf('baseWidth'))"
          [value]="settingOf('baseWidth')"
          (valueChange)="setSide('baseWidth', $event)"
        />
      </div>
      <div class="flex flex-col gap-1" data-testid="setting-baseHeight">
        <span class="text-foreground-muted text-xs">Height</span>
        <xui-select
          ariaLabel="Height"
          [disabled]="!store.editable()"
          [items]="sidesHolding(settingOf('baseHeight'))"
          [value]="settingOf('baseHeight')"
          (valueChange)="setSide('baseHeight', $event)"
        />
      </div>
      <p class="text-foreground-muted text-xs">
        Radii are counted in these texels and the shape of the image comes from them, so changing them changes the
        picture. Size does not.
      </p>
    </div>

    <p class="section-label mt-4 mb-1">Parameters</p>
    <div class="flex flex-col gap-3">
      @for (section of sections(); track section.group) {
        @if (section.group) {
          <p class="text-foreground-muted -mb-1 text-xs font-semibold" data-testid="parameter-group">
            {{ section.group }}
          </p>
        }
        @for (parameter of section.members; track parameter.name) {
          <div
            class="well flex flex-col gap-1.5 p-2"
            [attr.data-testid]="'parameter-' + parameter.name"
            [class.opacity-60]="!visible(parameter)"
            [title]="visible(parameter) ? parameter.summary : 'Hidden while ' + hiddenBecause(parameter)"
          >
            <div class="flex items-center gap-1">
              <input
                class="min-w-0 flex-1"
                xuiInput
                size="sm"
                aria-label="Parameter name"
                title="The name expressions write. Renaming it renames it in the file."
                [disabled]="!store.editable()"
                [value]="parameter.name"
                (change)="rename(parameter, $event)"
              />
              <xui-select
                class="w-28 min-w-0 shrink-0"
                ariaLabel="Parameter kind"
                [disabled]="!store.editable()"
                [itemText]="kindName"
                [items]="kinds"
                [value]="parameter.kind"
                (valueChange)="setKind(parameter, $event)"
              />
              <button
                class="shrink-0"
                type="button"
                xuiButton
                size="sm"
                variant="ghost"
                title="Describe it: label, group, range, widget and when it shows"
                [active]="describing().has(parameter.name)"
                [attr.aria-label]="'Describe ' + parameter.name"
                [attr.data-testid]="'parameter-describe-' + parameter.name"
                (click)="toggleDescribing(parameter.name)"
              >
                …
              </button>
            </div>
            @if (describing().has(parameter.name)) {
              <div class="flex flex-col gap-1.5" [attr.data-testid]="'parameter-details-' + parameter.name">
                <input
                  xuiInput
                  size="sm"
                  aria-label="Parameter label"
                  [attr.data-testid]="'parameter-label-' + parameter.name"
                  [disabled]="!store.editable()"
                  [placeholder]="'Label (' + parameter.name + ')'"
                  [value]="parameter.label ?? ''"
                  (change)="describe(parameter, { label: valueOf($event) })"
                />
                <div class="flex items-center gap-1">
                  <input
                    class="min-w-0 flex-1"
                    xuiInput
                    size="sm"
                    aria-label="Parameter group"
                    placeholder="Group"
                    [attr.data-testid]="'parameter-group-' + parameter.name"
                    [disabled]="!store.editable()"
                    [value]="parameter.group"
                    (change)="describe(parameter, { group: valueOf($event) })"
                  />
                  <xui-select
                    class="w-28 min-w-0 shrink-0"
                    ariaLabel="Parameter widget"
                    [attr.data-testid]="'parameter-widget-' + parameter.name"
                    [disabled]="!store.editable()"
                    [items]="widgetsFor(parameter)"
                    [value]="parameter.widget ?? 'Default'"
                    (valueChange)="setWidget(parameter, $event)"
                  />
                </div>
                <input
                  xuiInput
                  size="sm"
                  aria-label="Parameter summary"
                  placeholder="What it does, in one line"
                  [disabled]="!store.editable()"
                  [value]="parameter.summary"
                  (change)="describe(parameter, { summary: valueOf($event) })"
                />
                @if (parameter.kind === 'Float' || parameter.kind === 'Int') {
                  <div class="flex items-center gap-1" [attr.data-testid]="'parameter-range-' + parameter.name">
                    <span class="text-foreground-muted w-12 shrink-0 text-xs">Range</span>
                    <xui-numeric-input
                      class="min-w-0 flex-1"
                      size="sm"
                      buttonPosition="none"
                      [ariaLabel]="parameter.name + ' minimum'"
                      [disabled]="!store.editable()"
                      [value]="parameter.minimum ?? null"
                      (valueChange)="setRange(parameter, 'minimum', $event)"
                    />
                    <xui-numeric-input
                      class="min-w-0 flex-1"
                      size="sm"
                      buttonPosition="none"
                      [ariaLabel]="parameter.name + ' maximum'"
                      [disabled]="!store.editable()"
                      [value]="parameter.maximum ?? null"
                      (valueChange)="setRange(parameter, 'maximum', $event)"
                    />
                  </div>
                }
                @if (parameter.kind === 'Text') {
                  <input
                    xuiInput
                    size="sm"
                    aria-label="Parameter choices"
                    placeholder="Choices, separated by commas"
                    [attr.data-testid]="'parameter-choices-' + parameter.name"
                    [disabled]="!store.editable()"
                    [value]="parameter.accepted.join(', ')"
                    (change)="setChoices(parameter, $event)"
                  />
                }
                <div class="flex items-center gap-1" [attr.data-testid]="'parameter-visible-' + parameter.name">
                  <span class="text-foreground-muted w-12 shrink-0 text-xs">Shown</span>
                  <xui-select
                    class="min-w-0 flex-1"
                    ariaLabel="Shown while"
                    [disabled]="!store.editable()"
                    [itemText]="conditionName"
                    [items]="conditionsFor(parameter)"
                    [value]="parameter.visibleIf?.setting ?? ''"
                    (valueChange)="setCondition(parameter, { setting: $event ?? '' })"
                  />
                  @if (parameter.visibleIf; as condition) {
                    <xui-select
                      class="w-20 min-w-0 shrink-0"
                      ariaLabel="Comparison"
                      [disabled]="!store.editable()"
                      [items]="comparisons"
                      [value]="condition.negated ? 'is not' : 'is'"
                      (valueChange)="setCondition(parameter, { negated: $event === 'is not' })"
                    />
                    <input
                      class="w-20 min-w-0 shrink-0"
                      xuiInput
                      size="sm"
                      aria-label="Shown while value"
                      [disabled]="!store.editable()"
                      [value]="condition.value"
                      (change)="setCondition(parameter, { value: valueOf($event) })"
                    />
                  }
                </div>
              </div>
            }
            @switch (controlOf(parameter)) {
              @case ('slider') {
                <div class="flex items-center gap-2">
                  <xui-slider
                    class="min-w-0 flex-1"
                    labelStepSize="0"
                    [attr.aria-label]="titleOf(parameter) + ' default'"
                    [disabled]="!store.editable()"
                    [max]="parameter.maximum ?? 1"
                    [min]="parameter.minimum ?? 0"
                    [stepSize]="stepOf(parameter)"
                    [value]="numberOf(parameter)"
                    (valueChange)="setNumber(parameter, $event)"
                  />
                  <xui-numeric-input
                    class="w-28 min-w-0 shrink-0"
                    size="sm"
                    [ariaLabel]="parameter.name + ' default value'"
                    [disabled]="!store.editable()"
                    [stepSize]="parameter.kind === 'Int' ? 1 : 0.1"
                    [value]="numberOf(parameter)"
                    (valueChange)="setNumber(parameter, $event)"
                  />
                </div>
              }
              @case ('number') {
                <xui-numeric-input
                  size="sm"
                  [ariaLabel]="parameter.name + ' default value'"
                  [disabled]="!store.editable()"
                  [stepSize]="parameter.kind === 'Int' ? 1 : 0.1"
                  [value]="numberOf(parameter)"
                  (valueChange)="setNumber(parameter, $event)"
                />
              }
              @case ('angle') {
                <div class="flex items-center gap-2" [attr.data-testid]="'parameter-angle-' + parameter.name">
                  <ptn-angle-dial
                    [ariaLabel]="titleOf(parameter) + ' default'"
                    [disabled]="!store.editable()"
                    [value]="numberOf(parameter)"
                    (released)="store.endGesture()"
                    (valueChange)="setNumber(parameter, $event)"
                  />
                  <xui-numeric-input
                    class="min-w-0 flex-1"
                    size="sm"
                    [ariaLabel]="parameter.name + ' default value'"
                    [disabled]="!store.editable()"
                    [stepSize]="1"
                    [value]="numberOf(parameter)"
                    (valueChange)="setNumber(parameter, $event)"
                  />
                  <span class="text-foreground-muted text-xs">°</span>
                </div>
              }
              @case ('switch') {
                <xui-switch
                  [ariaLabel]="titleOf(parameter) + ' default'"
                  [checked]="store.parameterDefault(parameter) === 'true'"
                  [disabled]="!store.editable()"
                  (checkedChange)="setDefault(parameter, $event ? 'true' : 'false')"
                />
              }
              @case ('toggle') {
                <button
                  class="self-start"
                  type="button"
                  xuiButton
                  size="sm"
                  [active]="store.parameterDefault(parameter) === 'true'"
                  [disabled]="!store.editable()"
                  (click)="setDefault(parameter, store.parameterDefault(parameter) === 'true' ? 'false' : 'true')"
                >
                  {{ titleOf(parameter) }}
                </button>
              }
              @case ('choice') {
                <xui-select
                  [aria-label]="titleOf(parameter) + ' default'"
                  [disabled]="!store.editable()"
                  [itemText]="choiceOf(parameter)"
                  [items]="parameter.accepted"
                  [value]="store.parameterDefault(parameter)"
                  (valueChange)="setDefault(parameter, $event ?? parameter.default)"
                />
              }
              @default {
                <input
                  xuiInput
                  size="sm"
                  [attr.aria-label]="titleOf(parameter) + ' default'"
                  [disabled]="!store.editable()"
                  [value]="store.parameterDefault(parameter)"
                  (change)="setDefaultText(parameter, $event)"
                />
              }
            }
            <button
              class="self-start"
              type="button"
              xuiButton
              size="sm"
              variant="ghost"
              [disabled]="!store.editable()"
              (click)="store.removeParameter(parameter.name)"
            >
              Remove
            </button>
          </div>
        }
      } @empty {
        <p class="text-foreground-muted text-xs">This graph exposes nothing yet.</p>
      }
      <button
        class="self-start"
        type="button"
        xuiButton
        size="sm"
        data-testid="parameter-add"
        [disabled]="!store.editable()"
        (click)="addParameter()"
      >
        Add parameter
      </button>
    </div>

    <p class="section-label mt-4 mb-1">Presets</p>
    <div class="flex flex-col gap-1.5" data-testid="graph-presets">
      @for (preset of document?.presets; track preset.name) {
        <div class="flex items-center gap-1" [attr.data-testid]="'preset-' + preset.name">
          <span class="min-w-0 flex-1 truncate text-xs" [title]="presetSummary(preset)">{{ preset.name }}</span>
          <button
            type="button"
            xuiButton
            size="sm"
            variant="ghost"
            title="Set every parameter it names to its value"
            [attr.data-testid]="'preset-apply-' + preset.name"
            [disabled]="!store.editable()"
            (click)="store.applyPreset(preset.name)"
          >
            Apply
          </button>
          <button
            type="button"
            xuiButton
            size="sm"
            variant="ghost"
            title="Save the parameters as they are now under this name"
            [disabled]="!store.editable()"
            (click)="store.savePreset(preset.name)"
          >
            Update
          </button>
          <button
            type="button"
            xuiButton
            size="sm"
            variant="ghost"
            [attr.aria-label]="'Remove preset ' + preset.name"
            [disabled]="!store.editable()"
            (click)="store.removePreset(preset.name)"
          >
            ×
          </button>
        </div>
      } @empty {
        <p class="text-foreground-muted text-xs">No presets. Saving one keeps every parameter as it is now.</p>
      }
      <div class="flex items-center gap-1">
        <input
          #presetName
          class="min-w-0 flex-1"
          xuiInput
          size="sm"
          aria-label="New preset name"
          data-testid="preset-name"
          [disabled]="!store.editable() || !document?.parameters?.length"
          [placeholder]="freePreset()"
        />
        <button
          type="button"
          xuiButton
          size="sm"
          data-testid="preset-save"
          [disabled]="!store.editable() || !document?.parameters?.length"
          (click)="savePreset(presetName)"
        >
          Save preset
        </button>
      </div>
    </div>

    <p class="section-label mt-4 mb-1">About</p>
    @let info = document?.info;
    <div class="flex flex-col gap-2.5" data-testid="graph-about">
      <div class="flex flex-col gap-1">
        <span class="text-foreground-muted text-xs">Description</span>
        <textarea
          class="h-16 w-full"
          xuiInput
          aria-label="Description"
          data-testid="about-description"
          title="What the graph makes and how it is meant to be used. A package and the shelf show it."
          [disabled]="!store.editable()"
          [value]="info?.description ?? ''"
          (change)="store.describe({ description: valueOf($event) })"
        ></textarea>
      </div>
      <div class="flex flex-col gap-1">
        <span class="text-foreground-muted text-xs">Author</span>
        <input
          xuiInput
          size="sm"
          aria-label="Author"
          data-testid="about-author"
          [disabled]="!store.editable()"
          [value]="info?.author ?? ''"
          (change)="store.describe({ author: valueOf($event) })"
        />
      </div>
      <div class="flex flex-col gap-1">
        <span class="text-foreground-muted text-xs">Tags</span>
        <input
          xuiInput
          size="sm"
          aria-label="Tags"
          data-testid="about-tags"
          placeholder="metal, rust"
          title="Words the graph carries wherever it goes, into a package too. The shelf finds it by them, and by the project’s own tags for it."
          [disabled]="!store.editable()"
          [value]="(info?.tags ?? []).join(', ')"
          (change)="store.describe({ tags: tagsOf($event) })"
        />
      </div>
      <div class="flex flex-col gap-1" data-testid="about-physical-size">
        <span class="text-foreground-muted text-xs">Physical size, in centimetres</span>
        <div class="flex items-center gap-1">
          <xui-numeric-input
            class="min-w-0 flex-1"
            size="sm"
            ariaLabel="Physical width"
            [disabled]="!store.editable()"
            [min]="0"
            [value]="info?.physicalWidth ?? 0"
            (valueChange)="setPhysical($event, info?.physicalHeight ?? $event)"
          />
          <span class="text-foreground-muted text-xs">×</span>
          <xui-numeric-input
            class="min-w-0 flex-1"
            size="sm"
            ariaLabel="Physical height"
            [disabled]="!store.editable()"
            [min]="0"
            [value]="info?.physicalHeight ?? 0"
            (valueChange)="setPhysical(info?.physicalWidth ?? $event, $event)"
          />
        </div>
        <p class="text-foreground-muted text-xs">How much of the world one tile covers. Zero says nothing.</p>
      </div>
      <button
        type="button"
        xuiButton
        size="sm"
        data-testid="about-publish"
        [title]="commands.tooltip('publishPackage')"
        [disabled]="!commands.enabled('publishPackage')"
        (click)="commands.run('publishPackage')"
      >
        Publish as Package…
      </button>
    </div>

    @if (isCompound()) {
      <p class="section-label mt-4 mb-1">Interface</p>
      <div class="flex flex-col gap-1.5">
        @for (port of document?.interface; track port.direction + '/' + port.name) {
          <div class="well flex flex-col gap-1.5 p-2" [attr.data-testid]="'port-' + port.name">
            <div class="flex items-center gap-1">
              <span class="readout text-foreground-muted w-8 shrink-0">{{
                port.direction === 'Input' ? 'In' : 'Out'
              }}</span>
              <input
                class="min-w-0 flex-1"
                xuiInput
                size="sm"
                aria-label="Port name"
                [disabled]="!store.editable()"
                [value]="port.name"
                (change)="renamePort(port, $event)"
              />
            </div>
            <div class="flex items-center gap-1">
              <xui-select
                class="min-w-0 flex-1"
                ariaLabel="Port kind"
                [disabled]="!store.editable()"
                [itemText]="kindName"
                [items]="portKinds"
                [value]="port.kind"
                (valueChange)="setPortKind(port, $event)"
              />
              <button
                class="shrink-0"
                type="button"
                xuiButton
                size="sm"
                variant="ghost"
                [disabled]="!store.editable()"
                (click)="store.removePort(port.name, port.direction)"
              >
                Remove
              </button>
            </div>
          </div>
        } @empty {
          <p class="text-foreground-muted text-xs">This compound has no ports yet.</p>
        }
        <div class="flex gap-1">
          <button
            type="button"
            xuiButton
            size="sm"
            data-testid="port-add-input"
            [disabled]="!store.editable()"
            (click)="addPort('Input')"
          >
            Add input
          </button>
          <button
            type="button"
            xuiButton
            size="sm"
            data-testid="port-add-output"
            [disabled]="!store.editable()"
            (click)="addPort('Output')"
          >
            Add output
          </button>
        </div>
      </div>
    }

    @if (document?.groups?.length) {
      <p class="section-label mt-4 mb-1">Groups</p>
      <div class="flex flex-col gap-1.5">
        @for (group of document?.groups; track group.id) {
          <div class="flex flex-col gap-1" data-testid="group-row">
            <div class="flex items-center gap-1">
              <xui-select
                class="w-28 min-w-0 shrink-0"
                ariaLabel="Frame color"
                data-testid="group-colour"
                [disabled]="!store.editable()"
                [itemText]="colourName"
                [items]="frameColours"
                [value]="group.color"
                (valueChange)="store.describeGroup(group.id, { color: $event ?? '' })"
              />
              <input
                class="min-w-0 flex-1"
                xuiInput
                size="sm"
                aria-label="Group title"
                [disabled]="!store.editable()"
                [value]="group.title"
                (change)="retitle(group.id, $event)"
              />
              <span class="readout text-foreground-muted shrink-0">{{ group.nodes.length }}</span>
              <button
                type="button"
                xuiButton
                size="sm"
                variant="ghost"
                [disabled]="!store.editable()"
                (click)="store.ungroup(group.id)"
              >
                Ungroup
              </button>
            </div>
            <input
              xuiInput
              size="sm"
              aria-label="Group description"
              placeholder="What these nodes do"
              data-testid="group-description"
              [disabled]="!store.editable()"
              [value]="group.description"
              (change)="store.describeGroup(group.id, { description: valueOf($event) })"
            />
          </div>
        }
      </div>
    }
  `
})
export class GraphSettings {
  protected readonly store = inject(GraphStore);
  protected readonly commands = inject(Commands);
  private readonly project = inject(ProjectStore);
  private readonly status = inject(SystemStatus);
  protected readonly kinds = Kinds;
  protected readonly String = String;
  protected readonly bitDepths = BitDepths;
  protected readonly portKinds = PortKinds;
  protected readonly kindName = kindName;
  /** The labels of a parameter's choices, as a select asks for them. */
  protected choiceOf(parameter: GraphSettingDefinition): (value: string) => string {
    return value => choiceName(parameter, value);
  }

  protected readonly seed = computed(() => Number(this.store.document()?.settings['seed'] ?? 0));

  protected readonly bitDepth = computed(() => this.store.document()?.settings['bitDepth'] ?? DefaultBitDepth);

  /** The Server offers boundary nodes to a graph that can have an interface, and to no other. */
  protected readonly isCompound = computed(() => this.store.types().has('Sub-graph/Input'));

  /** The project's bakes, by path below the library, and no bake at all. */
  protected readonly bakes = computed(() => {
    const held = this.project.current()?.bakes ?? [];
    return held.length > 0 ? [NoBake, ...held.map(bake => bake.path)] : [];
  });

  protected readonly bake = linkedSignal(() => {
    const path = this.store.document()?.path;
    return this.project.current()?.documents.find(document => document.path === path)?.bake ?? NoBake;
  });

  protected readonly bakeName = (path: string): string =>
    path === NoBake ? 'None' : (this.project.current()?.bakes.find(bake => bake.path === path)?.name ?? path);

  protected readonly roles = Roles.map(role => role.value);
  protected readonly roleName = (role: string): string => Roles.find(held => held.value === role)?.label ?? role;

  /** The graph as its project lists it, which is what has a role. A graph no project lists has none to set. */
  protected readonly listed = computed(() => {
    const path = this.store.document()?.path;
    const document = this.project.current()?.documents.find(held => held.path === path);
    return document?.id && document.kind === 'graph' ? document : null;
  });

  /** Says what the graph is for. A graph of the wrong shape for it is refused, and the project says why. */
  protected setRole(role: string | null): void {
    const document = this.listed();
    const project = this.project.current()?.path;
    if (role !== null && document && project && role !== (document.role ?? 'material')) {
      void this.project.setRole(project, document, role);
    }
  }

  /** What a graph's Mesh Map nodes read. A project's stacks find their bake; this says it for a graph. */
  protected chooseBake(path: string | null): void {
    const status = this.status.state();
    if (path === null || path === this.bake() || status.kind !== 'online') {
      return;
    }

    this.bake.set(path);
    void this.store.useMeshMaps(path === NoBake ? null : `${status.system.library ?? ''}/${path}`);
  }

  protected settingOf(key: string): string {
    return this.store.document()?.settings[key] ?? '';
  }

  /** A graph that says nothing tiles, unless it reads a mesh map, because an atlas has no far side. */
  protected tiles(key: string): boolean {
    const declared = this.settingOf(key);
    if (declared !== '') {
      return declared === 'true';
    }

    return ![...(this.store.document()?.nodes.values() ?? [])].some(node => node.type === MeshMap);
  }

  /** What the graph evaluates at: its size, or its longer base side when it names none. */
  protected size(): string {
    const longest = Math.max(Number(this.settingOf('baseWidth')), Number(this.settingOf('baseHeight')));
    return this.settingOf('size') || (longest > 0 ? String(longest) : '');
  }

  /** The usual sizes, and the one this graph has if it is something else. */
  protected sidesHolding(held: string): readonly string[] {
    const sides = this.status.textureSides().map(String);
    return held === '' || sides.includes(held) ? sides : [...sides, held];
  }

  protected setSide(key: string, value: string | null): void {
    if (value !== null && value !== this.settingOf(key)) {
      this.store.setSetting(key, value);
    }
  }

  protected setBitDepth(value: string | null): void {
    if (value !== null && value !== this.bitDepth()) {
      this.store.setSetting('bitDepth', value);
    }
  }

  /** Tags as a person types them, separated by commas. The Server drops blanks and repeats. */
  protected tagsOf(event: Event): string[] {
    return this.valueOf(event)
      .split(',')
      .map(tag => tag.trim())
      .filter(tag => tag !== '');
  }

  /** A physical size is a width and a height together, both above zero, or both zero for none. */
  protected setPhysical(width: number | null, height: number | null): void {
    const [w, h] = [width ?? 0, height ?? 0];
    const info = this.store.document()?.info;
    if (w === (info?.physicalWidth ?? 0) && h === (info?.physicalHeight ?? 0)) {
      return;
    }

    this.store.describe(
      w > 0 && h > 0 ? { physicalWidth: w, physicalHeight: h } : { physicalWidth: 0, physicalHeight: 0 }
    );
  }

  protected addPort(direction: string): void {
    this.store.addPort({
      name: freePortName(this.store.document()?.interface ?? [], direction),
      direction,
      kind: 'Image',
      default: [],
      summary: ''
    });
  }

  protected renamePort(port: GraphInterfacePort, event: Event): void {
    const name = (event.target as HTMLInputElement).value.trim();
    if (name !== port.name) {
      this.store.editPort(port.name, port.direction, { ...port, name });
    }
  }

  protected setPortKind(port: GraphInterfacePort, kind: string | null): void {
    if (kind !== null && kind !== port.kind) {
      this.store.editPort(port.name, port.direction, { ...port, kind, default: [] });
    }
  }

  protected setSeed(value: number | null): void {
    if (value !== null && Number.isInteger(value) && value >= 0 && value !== this.seed()) {
      this.store.setSetting('seed', String(value));
    }
  }

  protected readonly controlOf = controlOf;
  protected readonly titleOf = titleOf;
  protected readonly widgetsFor = widgetsFor;
  protected readonly comparisons = ['is', 'is not'];
  /** Auto, which picks a colour from the frame's number, and the ramp's colours as the file holds them. */
  protected readonly frameColours = ['', ...FrameColours];
  protected readonly colourName = frameColourName;

  /** The parameters under their group headings. */
  protected readonly sections = computed(() => grouped(this.store.document()?.parameters ?? []));

  /** Parameters whose description is open. */
  protected readonly describing = signal<ReadonlySet<string>>(new Set());

  protected readonly freePreset = computed(() => freePresetName(this.store.document()?.presets ?? []));

  protected toggleDescribing(name: string): void {
    this.describing.update(open => {
      const next = new Set(open);
      if (!next.delete(name)) {
        next.add(name);
      }

      return next;
    });
  }

  protected valueOf(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  /** Whether a parameter shows to whoever uses the graph, given the defaults as they are now. */
  protected visible(parameter: GraphSettingDefinition): boolean {
    const condition = parameter.visibleIf;
    const other = this.store.document()?.parameters.find(held => held.name === condition?.setting);
    return holds(condition, other ? this.store.parameterDefault(other) : undefined);
  }

  protected hiddenBecause(parameter: GraphSettingDefinition): string {
    const condition = parameter.visibleIf;
    return condition ? `${condition.setting} ${condition.negated ? 'is' : 'is not'} ${condition.value}` : '';
  }

  /** Every parameter but this one, as something to show it by, and nothing for always. */
  protected conditionsFor(parameter: GraphSettingDefinition): readonly string[] {
    return [
      '',
      ...(this.store.document()?.parameters ?? []).map(held => held.name).filter(name => name !== parameter.name)
    ];
  }

  protected readonly conditionName = (name: string): string => (name === '' ? 'Always' : name);

  /** Changes how a parameter is described, keeping everything else about it. */
  protected describe(
    parameter: GraphSettingDefinition,
    change: Partial<Pick<GraphSettingDefinition, 'label' | 'group' | 'summary'>>
  ): void {
    parameter = this.latest(parameter);
    const next = { ...parameter, ...change };
    if (next.label !== parameter.label || next.group !== parameter.group || next.summary !== parameter.summary) {
      this.store.editParameter(parameter.name, next);
    }
  }

  protected setWidget(parameter: GraphSettingDefinition, widget: string | null): void {
    parameter = this.latest(parameter);
    const next = widget === null || widget === 'Default' ? null : widget;
    if (next !== (parameter.widget ?? null)) {
      this.store.editParameter(parameter.name, { ...parameter, widget: next });
    }
  }

  protected setRange(parameter: GraphSettingDefinition, end: 'minimum' | 'maximum', value: number | null): void {
    parameter = this.latest(parameter);
    const next = value !== null && Number.isFinite(value) ? value : null;
    if (next !== (parameter[end] ?? null)) {
      const widened = { ...parameter, [end]: next };
      // A slider needs both ends, so a range that loses one loses the slider too.
      const slider = widened.widget === 'Slider' && (widened.minimum === null || widened.maximum === null);
      this.store.editParameter(parameter.name, slider ? { ...widened, widget: null } : widened);
    }
  }

  protected setChoices(parameter: GraphSettingDefinition, event: Event): void {
    parameter = this.latest(parameter);
    const accepted = this.valueOf(event)
      .split(',')
      .map(choice => choice.trim())
      .filter(choice => choice !== '');
    if (accepted.join('\0') !== parameter.accepted.join('\0')) {
      const current = this.store.parameterDefault(parameter);
      this.store.editParameter(parameter.name, {
        ...parameter,
        accepted,
        default: accepted.length === 0 || accepted.includes(current) ? current : (accepted[0] ?? '')
      });
    }
  }

  protected setCondition(parameter: GraphSettingDefinition, change: Partial<GraphSettingCondition>): void {
    parameter = this.latest(parameter);
    const held = parameter.visibleIf ?? { setting: '', value: 'true', negated: false };
    const next = { ...held, ...change };
    this.store.editParameter(parameter.name, { ...parameter, visibleIf: next.setting === '' ? null : next });
  }

  /** The parameter as this client last asked for it, so that two quick edits do not undo each other. */
  private latest(parameter: GraphSettingDefinition): GraphSettingDefinition {
    return this.store.parameter(parameter.name) ?? parameter;
  }

  protected presetSummary(preset: GraphPresetState): string {
    return Object.entries(preset.values)
      .map(([name, value]) => `${name} = ${value}`)
      .join('\n');
  }

  protected savePreset(box: HTMLInputElement): void {
    const name = box.value.trim() || this.freePreset();
    this.store.savePreset(name);
    box.value = '';
  }

  protected stepOf(parameter: GraphSettingDefinition): number {
    return parameter.kind === 'Int' ? 1 : ((parameter.maximum ?? 1) - (parameter.minimum ?? 0)) / SliderSteps;
  }

  protected numberOf(parameter: GraphSettingDefinition): number {
    const value = Number(this.store.parameterDefault(parameter));
    return Number.isFinite(value) ? value : 0;
  }

  protected setNumber(parameter: GraphSettingDefinition, value: number | null): void {
    if (value === null || !Number.isFinite(value)) {
      return;
    }

    const next = parameter.kind === 'Int' ? Math.round(value) : value;
    if (next !== this.numberOf(parameter)) {
      this.store.setParameterDefault(parameter, String(next));
    }
  }

  protected setDefault(parameter: GraphSettingDefinition, value: string): void {
    this.store.setParameterDefault(parameter, value);
    this.store.endGesture();
  }

  protected setDefaultText(parameter: GraphSettingDefinition, event: Event): void {
    this.setDefault(parameter, (event.target as HTMLInputElement).value);
  }

  protected rename(parameter: GraphSettingDefinition, event: Event): void {
    parameter = this.latest(parameter);
    const name = (event.target as HTMLInputElement).value.trim();
    if (name !== parameter.name) {
      this.store.editParameter(parameter.name, { ...parameter, name });
    }
  }

  protected setKind(parameter: GraphSettingDefinition, kind: string | null): void {
    parameter = this.latest(parameter);
    if (kind !== null && kind !== parameter.kind) {
      const { minimum, maximum, ...rest } = parameter;
      this.store.editParameter(parameter.name, {
        ...rest,
        ...(kind === 'Float' ? { minimum, maximum } : {}),
        kind,
        default: startOf(kind),
        accepted: [],
        // A widget suits one kind, and the Server refuses one that does not.
        widget: null
      });
    }
  }

  protected addParameter(): void {
    this.store.addParameter({
      name: freeParameterName(this.store.document()?.parameters ?? []),
      default: '0.5',
      summary: '',
      kind: 'Float',
      minimum: 0,
      maximum: 1,
      group: '',
      accepted: []
    });
  }

  protected retitle(id: number, event: Event): void {
    this.store.renameGroup(id, (event.target as HTMLInputElement).value);
  }
}

/** A note in the Inspector. */
@Component({
  selector: 'ptn-graph-note',
  imports: [XuiButtonImports, XuiInputImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block h-full overflow-auto p-3' },
  template: `
    <h2 class="truncate text-sm font-semibold" data-testid="inspector-title">Note</h2>
    <p class="section-label mb-3">Graph</p>
    <textarea
      class="h-32 w-full"
      xuiInput
      aria-label="Note text"
      data-testid="note-text"
      [disabled]="!store.editable()"
      [value]="text()"
      (change)="retype($event)"
    ></textarea>
    <button
      class="mt-2"
      type="button"
      xuiButton
      size="sm"
      variant="ghost"
      [disabled]="!store.editable()"
      (click)="store.removeComment(note())"
    >
      Delete note
    </button>
  `
})
export class GraphNoteInspector {
  protected readonly store = inject(GraphStore);

  readonly note = input.required<number>();

  protected readonly text = computed(
    () => this.store.document()?.comments.find(comment => comment.id === this.note())?.text ?? ''
  );

  protected retype(event: Event): void {
    this.store.setCommentText(this.note(), (event.target as HTMLTextAreaElement).value);
  }
}

/** Several nodes in the Inspector: a single node's fields would not say which they apply to. */
@Component({
  selector: 'ptn-graph-selection',
  imports: [XuiButtonImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block h-full overflow-auto p-3' },
  template: `
    <h2 class="truncate text-sm font-semibold" data-testid="inspector-title">{{ titles().length }} nodes</h2>
    <p class="section-label mb-2">Selection</p>
    <ul class="mb-3 flex flex-col gap-0.5" data-testid="selection-list">
      @for (title of titles(); track $index) {
        <li class="truncate text-xs">{{ title }}</li>
      }
    </ul>
    <div class="flex gap-1">
      <button type="button" xuiButton size="sm" [disabled]="!store.editable()" (click)="store.groupSelection()">
        Group
      </button>
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        data-testid="selection-delete"
        [disabled]="!store.editable()"
        (click)="store.remove(nodes(), [])"
      >
        Delete
      </button>
    </div>
  `
})
export class GraphSelection {
  protected readonly store = inject(GraphStore);

  protected readonly nodes = computed(() => [...this.store.selection()]);
  protected readonly titles = computed(() => {
    const document = this.store.document();
    const types = this.store.types();
    return this.nodes().map(id => {
      const type = document?.nodes.get(id)?.type ?? '';
      return types.get(type)?.title ?? type;
    });
  });
}
