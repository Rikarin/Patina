import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import type { BakeOcclusionOptions, BakeOptions, BakeRayOptions, JobState } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiInputImports } from '@xui/input';
import { XuiNumericInputImports } from '@xui/numeric-input';
import { XuiSegmentedControlImports } from '@xui/segmented-control';
import { XuiSelectImports } from '@xui/select';
import { XuiSliderImports } from '@xui/slider';
import { XuiSwitchImports } from '@xui/switch';
import { desktop } from '../core/desktop';
import { imageFormatName, jobStateName, termName } from '../core/labels';
import { messageOf } from '../core/messages';
import { percent } from '../core/numbers';
import { SystemStatus } from '../core/system-status';
import { GraphStore } from '../graph/graph-store';
import { ProjectStore } from '../library/project-store';
import { BakeSettings } from './bake-settings';
import { BakeStore } from './bake-store';

const Formats = ['png8', 'png16', 'exr16', 'exr32'];

/** Every map, in the order a bake reports them, with what each one is for. */
const Maps: readonly { name: string; about: string }[] = [
  { name: 'normal', about: 'The high mesh’s surface direction, so the low mesh lights as if it had the detail.' },
  { name: 'height', about: 'How far the high mesh stands proud of the low one, or sinks below it.' },
  { name: 'ao', about: 'How much of the sky each point sees. Dark in creases and under overhangs.' },
  { name: 'bent', about: 'The average open direction from each point, from the ambient occlusion rays.' },
  { name: 'curvature', about: 'Convex edges light and concave creases dark, for wear and dirt.' },
  { name: 'thickness', about: 'How deep the surface goes behind each point, for light through thin parts.' },
  { name: 'position', about: 'Where each point is within the high mesh’s bounding box, for gradients.' },
  { name: 'world', about: 'The high mesh’s normal in object space, for masks by direction.' },
  { name: 'id', about: 'A flat colour per material, part or shell, for masks.' }
];

const Spaces = [
  { value: 'tangent', label: 'Tangent' },
  { value: 'object', label: 'Object' }
];
const Units = [
  { value: 'relative', label: 'Relative' },
  { value: 'absolute', label: 'Absolute' }
];
const HeightRanges = [
  { value: 'measured', label: 'Measured' },
  { value: 'rays', label: 'Ray distance' },
  { value: 'custom', label: 'Custom' }
];
const Distributions = [
  { value: 'cosine', label: 'Cosine' },
  { value: 'uniform', label: 'Uniform' }
];
const Palettes = [
  { value: 'random', label: 'Random' },
  { value: 'hue', label: 'Hue shift' },
  { value: 'grey', label: 'Grey' }
];
const Falloffs = ['none', 'linear', 'smooth'];
const FalloffNames: Readonly<Record<string, string>> = { none: 'None', linear: 'Linear', smooth: 'Smooth' };
const IdSources = ['auto', 'material', 'material-colour', 'vertex-colour', 'mesh', 'shell'];
const IdSourceNames: Readonly<Record<string, string>> = {
  auto: 'Automatic',
  material: 'Material',
  'material-colour': 'Material colour',
  'vertex-colour': 'Vertex colour',
  mesh: 'Mesh part',
  shell: 'Connected shell'
};

/** What the right side shows: `common`, or a map by name. */
type Section = string;

/**
 * The Bake dialog: common settings, then each map with its own, and the texture sets to bake. Every change is kept in
 * the project's file a moment later, so the next Bake starts where this one left off. Everything sent is a path, as for
 * any client (plan §8); the Server keeps the jobs, so closing the dialog stops nothing.
 */
@Component({
  selector: 'ptn-bake',
  imports: [
    XuiButtonImports,
    XuiCalloutImports,
    XuiInputImports,
    XuiNumericInputImports,
    XuiSegmentedControlImports,
    XuiSelectImports,
    XuiSliderImports,
    XuiSwitchImports
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  // A width of its own, so the dialog does not jump as a section with longer lines is chosen.
  host: { class: 'flex h-full min-h-0 w-[60rem] max-w-full flex-col' },
  template: `
    @if (options(); as options) {
      <div class="flex min-h-0 flex-1">
        <nav
          class="border-border flex w-60 shrink-0 flex-col gap-0.5 overflow-y-auto border-e p-2"
          aria-label="Bake sections"
        >
          <button
            type="button"
            xuiButton
            class="justify-start"
            size="sm"
            data-testid="bake-section-common"
            [variant]="section() === 'common' ? 'outline' : 'ghost'"
            (click)="section.set('common')"
          >
            Meshes and rays
          </button>
          <p class="section-label mt-2">Maps</p>
          @for (map of maps; track map.name) {
            <div class="flex items-center gap-1.5">
              <xui-switch
                [ariaLabel]="'Bake ' + label(map.name)"
                [attr.data-testid]="'bake-map-' + map.name"
                [checked]="options.maps.includes(map.name)"
                (checkedChange)="setMap(map.name, $event)"
              />
              <button
                type="button"
                xuiButton
                class="min-w-0 flex-1 justify-start"
                size="sm"
                [attr.data-testid]="'bake-section-' + map.name"
                [variant]="section() === map.name ? 'outline' : 'ghost'"
                (click)="section.set(map.name)"
              >
                {{ label(map.name) }}
              </button>
            </div>
          }
          @if (settings.sets().length > 0) {
            <div class="mt-2 flex items-center justify-between">
              <p class="section-label">Texture sets</p>
              <button type="button" xuiButton size="sm" variant="ghost" data-testid="bake-sets-all" (click)="allSets()">
                All
              </button>
            </div>
            @for (set of settings.sets(); track set.material) {
              <label class="flex items-center gap-1.5 px-1 text-sm">
                <xui-switch
                  [ariaLabel]="'Bake ' + set.name"
                  [attr.data-testid]="'bake-set-' + set.material"
                  [checked]="chosen().has(set.material)"
                  (checkedChange)="setSet(set.material, $event)"
                />
                <span class="truncate">{{ set.name }}</span>
              </label>
            }
          }
        </nav>

        <div class="flex min-w-0 flex-1 flex-col gap-2.5 overflow-x-hidden overflow-y-auto p-3">
          @if (section() === 'common') {
            <div class="flex flex-col gap-1">
              <span class="text-foreground-muted text-xs">Low poly mesh</span>
              <span class="readout truncate" data-testid="bake-low" [title]="settings.low()">
                {{ nameOf(settings.low()) || 'None' }}{{ setsSaid() }}
              </span>
            </div>

            <div class="flex flex-col gap-1">
              <span class="text-foreground-muted text-xs">High poly meshes, in order</span>
              <div class="flex gap-1">
                <input
                  class="min-w-0 flex-1"
                  xuiInput
                  autocomplete="off"
                  aria-label="High poly mesh"
                  data-testid="bake-high"
                  placeholder="None: bakes from the low poly mesh"
                  [readOnly]="canPick"
                  [title]="ownHigh()"
                  [value]="canPick ? nameOf(ownHigh()) : ownHigh()"
                  (change)="typedHigh(text($event))"
                />
                @if (canPick) {
                  <button
                    type="button"
                    xuiButton
                    size="sm"
                    variant="ghost"
                    aria-label="Choose High poly mesh"
                    data-testid="bake-choose-high"
                    (click)="pickHigh()"
                  >
                    Choose…
                  </button>
                }
              </div>
              @for (path of settings.settings()?.high ?? []; track $index) {
                <div class="flex items-center gap-1">
                  <span class="readout min-w-0 flex-1 truncate" [title]="path">{{ nameOf(path) }}</span>
                  <button
                    type="button"
                    xuiButton
                    size="sm"
                    variant="ghost"
                    [attr.aria-label]="'Remove ' + nameOf(path)"
                    [attr.data-testid]="'bake-remove-high-' + $index"
                    (click)="removeHigh($index)"
                  >
                    Remove
                  </button>
                </div>
              }
              @if (canPick) {
                <button
                  type="button"
                  xuiButton
                  class="self-start"
                  size="sm"
                  variant="ghost"
                  data-testid="bake-add-high"
                  (click)="addHigh()"
                >
                  Add high poly mesh…
                </button>
              }
            </div>

            @for (field of pathFields; track field.key) {
              <div class="flex flex-col gap-1">
                <span class="text-foreground-muted text-xs">{{ field.label }}</span>
                <div class="flex gap-1">
                  <input
                    class="min-w-0 flex-1"
                    xuiInput
                    autocomplete="off"
                    [attr.aria-label]="field.label"
                    [attr.data-testid]="'bake-' + field.key"
                    [placeholder]="field.placeholder"
                    [readOnly]="canPick"
                    [title]="path(field.key)"
                    [value]="canPick ? nameOf(path(field.key)) : path(field.key)"
                    (change)="setPath(field.key, text($event))"
                  />
                  @if (canPick) {
                    <button
                      type="button"
                      xuiButton
                      size="sm"
                      variant="ghost"
                      [attr.aria-label]="'Choose ' + field.label"
                      [attr.data-testid]="'bake-choose-' + field.key"
                      (click)="pick(field.key)"
                    >
                      Choose…
                    </button>
                  }
                  @if (path(field.key)) {
                    <button
                      type="button"
                      xuiButton
                      size="sm"
                      variant="ghost"
                      [attr.aria-label]="'Clear ' + field.label"
                      (click)="setPath(field.key, '')"
                    >
                      Clear
                    </button>
                  }
                </div>
              </div>
            }

            <div class="grid grid-cols-[minmax(0,1fr)_minmax(0,1fr)] gap-2">
              <div class="flex min-w-0 flex-col gap-1">
                <span class="text-foreground-muted text-xs">Size</span>
                <xui-select
                  class="w-full"
                  ariaLabel="Size"
                  data-testid="bake-resolution"
                  [itemText]="sideName"
                  [items]="sides()"
                  [value]="String(options.resolution)"
                  (valueChange)="set({ resolution: Number($event ?? options.resolution) })"
                />
              </div>
              <div class="flex min-w-0 flex-col gap-1">
                <span class="text-foreground-muted text-xs">Format</span>
                <xui-select
                  class="w-full"
                  ariaLabel="Format"
                  data-testid="bake-format"
                  [itemText]="formatName"
                  [items]="formats"
                  [value]="options.format"
                  (valueChange)="set({ format: $event ?? options.format })"
                />
              </div>
              <div class="flex min-w-0 flex-col gap-1">
                <span class="text-foreground-muted text-xs">Antialiasing, samples per side</span>
                <xui-numeric-input
                  size="sm"
                  ariaLabel="Samples per side"
                  [max]="8"
                  [min]="1"
                  [stepSize]="1"
                  [value]="options.supersampling"
                  (valueChange)="set({ supersampling: $event ?? 1 })"
                />
              </div>
              <div class="flex min-w-0 flex-col gap-1">
                <span class="text-foreground-muted text-xs">Dilation, in texels</span>
                <xui-numeric-input
                  size="sm"
                  ariaLabel="Dilation in texels"
                  [max]="64"
                  [min]="0"
                  [stepSize]="1"
                  [value]="options.gutter"
                  (valueChange)="set({ gutter: $event ?? 4 })"
                />
              </div>
            </div>

            <p class="section-label mt-1">Rays</p>
            <div class="flex items-center justify-between gap-2 text-sm">
              Distances are
              <xui-segmented-control
                size="sm"
                ariaLabel="Distance units"
                data-testid="bake-units"
                [options]="units"
                [value]="options.rays.units"
                (valueChange)="setRays({ units: $event ?? options.rays.units })"
              />
            </div>
            <p class="text-foreground-muted text-xs">{{ unitsSaid() }}</p>
            <div class="grid grid-cols-[minmax(0,1fr)_minmax(0,1fr)] gap-2">
              <div class="flex min-w-0 flex-col gap-1">
                <span class="text-foreground-muted text-xs">Front distance, {{ unit() }}</span>
                <xui-numeric-input
                  size="sm"
                  ariaLabel="Front distance"
                  data-testid="bake-front"
                  [min]="0"
                  [stepSize]="step()"
                  [value]="shown(options.rays.front)"
                  (valueChange)="setRays({ front: stored($event) })"
                />
              </div>
              <div class="flex min-w-0 flex-col gap-1">
                <span class="text-foreground-muted text-xs">Rear distance, {{ unit() }}</span>
                <xui-numeric-input
                  size="sm"
                  ariaLabel="Rear distance"
                  data-testid="bake-rear"
                  [min]="0"
                  [stepSize]="step()"
                  [value]="shown(options.rays.rear)"
                  (valueChange)="setRays({ rear: stored($event) })"
                />
              </div>
            </div>
            <label
              class="flex items-center justify-between gap-2 text-sm"
              title="A high face turned away from the low mesh is passed through, not baked as a dent."
            >
              Ignore backfaces
              <xui-switch
                ariaLabel="Ignore backfaces"
                data-testid="bake-ignore-backfaces"
                [checked]="options.rays.ignoreBackfaces"
                (checkedChange)="setRays({ ignoreBackfaces: $event })"
              />
            </label>
            <div
              class="flex items-center gap-2"
              title="0% follows the smoothed direction and hides seams at hard edges; 100% follows each face, which keeps detail beside them square."
            >
              <span class="text-foreground-muted w-16 shrink-0 text-xs">Skew</span>
              <xui-slider
                class="min-w-0 flex-1"
                labelStepSize="0"
                aria-label="Skew"
                data-testid="bake-skew"
                [max]="100"
                [min]="0"
                [stepSize]="1"
                [value]="Math.round(options.rays.skew * 100)"
                (valueChange)="setRays({ skew: $event / 100 })"
              />
              <span class="readout w-12 shrink-0 text-end text-xs">{{ share(options.rays.skew) }}</span>
            </div>
            <label class="flex items-center justify-between gap-2 text-sm">
              Match parts by name
              <xui-switch
                ariaLabel="Match parts by name"
                data-testid="bake-match"
                [checked]="options.rays.matchByName"
                (checkedChange)="setRays({ matchByName: $event })"
              />
            </label>
            @if (options.rays.matchByName) {
              <div class="grid grid-cols-[minmax(0,1fr)_minmax(0,1fr)] gap-2">
                <div class="flex min-w-0 flex-col gap-1">
                  <span class="text-foreground-muted text-xs">Low poly names end in</span>
                  <input
                    xuiInput
                    autocomplete="off"
                    aria-label="Low poly name ending"
                    data-testid="bake-low-suffix"
                    [value]="options.rays.lowSuffix"
                    (change)="setRays({ lowSuffix: text($event) })"
                  />
                </div>
                <div class="flex min-w-0 flex-col gap-1">
                  <span class="text-foreground-muted text-xs">High poly names end in</span>
                  <input
                    xuiInput
                    autocomplete="off"
                    aria-label="High poly name ending"
                    data-testid="bake-high-suffix"
                    [value]="options.rays.highSuffix"
                    (change)="setRays({ highSuffix: text($event) })"
                  />
                </div>
              </div>
            }
          } @else {
            <div class="flex flex-col gap-1">
              <p class="text-sm font-medium" data-testid="bake-section-title">{{ label(section()) }}</p>
              <p class="text-foreground-muted text-xs">{{ about(section()) }}</p>
              @if (!options.maps.includes(section())) {
                <p class="text-foreground-muted text-xs">Off: the next bake leaves this map out.</p>
              }
            </div>
            @switch (section()) {
              @case ('normal') {
                <div class="flex items-center justify-between gap-2 text-sm">
                  Space
                  <xui-segmented-control
                    size="sm"
                    ariaLabel="Normal map space"
                    data-testid="bake-space"
                    [options]="spaces"
                    [value]="options.space"
                    (valueChange)="set({ space: $event ?? options.space })"
                  />
                </div>
                <p class="text-foreground-muted text-xs">
                  Tangent space follows the surface and survives deformation; object space is fixed to the model. The
                  bent normal uses the same space.
                </p>
              }
              @case ('height') {
                <div class="flex items-center justify-between gap-2 text-sm">
                  Range
                  <xui-segmented-control
                    size="sm"
                    ariaLabel="Height range"
                    data-testid="bake-height-range"
                    [options]="heightRanges"
                    [value]="options.height.range"
                    (valueChange)="set({ height: { ...options.height, range: $event ?? options.height.range } })"
                  />
                </div>
                <p class="text-foreground-muted text-xs">
                  Measured uses the whole grey range for the tallest detail found. Ray distance and Custom give every
                  bake the same scale, so maps of different meshes agree.
                </p>
                @if (options.height.range === 'custom') {
                  <div class="flex min-w-0 flex-col gap-1">
                    <span class="text-foreground-muted text-xs">Height range, {{ unit() }}</span>
                    <xui-numeric-input
                      size="sm"
                      ariaLabel="Height range"
                      data-testid="bake-height-scale"
                      [min]="0"
                      [stepSize]="step()"
                      [value]="shown(options.height.scale)"
                      (valueChange)="set({ height: { ...options.height, scale: stored($event) } })"
                    />
                  </div>
                }
              }
              @case ('ao') {
                <div class="grid grid-cols-[minmax(0,1fr)_minmax(0,1fr)] gap-2">
                  <div class="flex min-w-0 flex-col gap-1">
                    <span class="text-foreground-muted text-xs">Rays per texel</span>
                    <xui-numeric-input
                      size="sm"
                      ariaLabel="Occlusion rays"
                      data-testid="bake-ao-rays"
                      [max]="8192"
                      [min]="1"
                      [stepSize]="32"
                      [value]="options.occlusion.rays"
                      (valueChange)="setOcclusion({ rays: $event ?? 64 })"
                    />
                  </div>
                  <div class="flex min-w-0 flex-col gap-1">
                    <span class="text-foreground-muted text-xs">Occluder distance, {{ unit() }}</span>
                    <xui-numeric-input
                      size="sm"
                      ariaLabel="Occluder distance"
                      data-testid="bake-ao-distance"
                      [min]="0"
                      [stepSize]="step() * 10"
                      [value]="shown(options.occlusion.distance)"
                      (valueChange)="setOcclusion({ distance: stored($event) })"
                    />
                  </div>
                  <div class="flex min-w-0 flex-col gap-1">
                    <span class="text-foreground-muted text-xs">Minimum distance, {{ unit() }}</span>
                    <xui-numeric-input
                      size="sm"
                      ariaLabel="Minimum occluder distance"
                      data-testid="bake-ao-minimum"
                      [min]="0"
                      [stepSize]="step()"
                      [value]="shown(options.occlusion.minimum)"
                      (valueChange)="setOcclusion({ minimum: stored($event) })"
                    />
                  </div>
                  <div class="flex min-w-0 flex-col gap-1">
                    <span class="text-foreground-muted text-xs">Falloff</span>
                    <xui-select
                      class="w-full"
                      ariaLabel="Occlusion falloff"
                      data-testid="bake-ao-falloff"
                      [itemText]="falloffName"
                      [items]="falloffs"
                      [value]="options.occlusion.falloff"
                      (valueChange)="setOcclusion({ falloff: $event ?? 'none' })"
                    />
                  </div>
                </div>
                <div
                  class="flex items-center gap-2"
                  title="How far from the normal the rays reach. 90° is the whole hemisphere."
                >
                  <span class="text-foreground-muted w-16 shrink-0 text-xs">Spread</span>
                  <xui-slider
                    class="min-w-0 flex-1"
                    labelStepSize="0"
                    aria-label="Occlusion spread"
                    data-testid="bake-ao-spread"
                    [max]="90"
                    [min]="1"
                    [stepSize]="1"
                    [value]="options.occlusion.spread"
                    (valueChange)="setOcclusion({ spread: $event })"
                  />
                  <span class="readout w-12 shrink-0 text-end text-xs">{{ options.occlusion.spread }}°</span>
                </div>
                <div class="flex items-center justify-between gap-2 text-sm">
                  Distribution
                  <xui-segmented-control
                    size="sm"
                    ariaLabel="Ray distribution"
                    data-testid="bake-ao-distribution"
                    [options]="distributions"
                    [value]="options.occlusion.distribution"
                    (valueChange)="setOcclusion({ distribution: $event ?? options.occlusion.distribution })"
                  />
                </div>
                <label class="flex items-center justify-between gap-2 text-sm">
                  Ignore backfaces
                  <xui-switch
                    ariaLabel="Occlusion ignores backfaces"
                    data-testid="bake-ao-backfaces"
                    [checked]="options.occlusion.ignoreBackfaces"
                    (checkedChange)="setOcclusion({ ignoreBackfaces: $event })"
                  />
                </label>
                <label
                  class="flex items-center justify-between gap-2 text-sm"
                  [title]="options.rays.matchByName ? '' : 'Needs Match parts by name, under Meshes and rays.'"
                >
                  Only a part's own surface occludes it
                  <xui-switch
                    ariaLabel="Only a part's own surface occludes it"
                    data-testid="bake-ao-within-part"
                    [disabled]="!options.rays.matchByName"
                    [checked]="options.occlusion.withinPart"
                    (checkedChange)="setOcclusion({ withinPart: $event })"
                  />
                </label>
                <label
                  class="flex items-center justify-between gap-2 text-sm"
                  title="A floor under the high mesh, Y up."
                >
                  Ground plane
                  <xui-switch
                    ariaLabel="Ground plane"
                    data-testid="bake-ao-ground"
                    [checked]="options.occlusion.groundPlane"
                    (checkedChange)="setOcclusion({ groundPlane: $event })"
                  />
                </label>
                @if (options.occlusion.groundPlane) {
                  <div class="flex min-w-0 flex-col gap-1">
                    <span class="text-foreground-muted text-xs">Ground offset, {{ unit() }}</span>
                    <xui-numeric-input
                      size="sm"
                      ariaLabel="Ground offset"
                      data-testid="bake-ao-ground-offset"
                      [stepSize]="step()"
                      [value]="shown(options.occlusion.groundOffset)"
                      (valueChange)="setOcclusion({ groundOffset: stored($event) })"
                    />
                  </div>
                }
              }
              @case ('bent') {
                <p class="text-foreground-muted text-xs">Uses the ambient occlusion rays and the normal map's space.</p>
              }
              @case ('thickness') {
                <label class="flex items-center justify-between gap-2 text-sm">
                  Normalise, so the thickest point is white
                  <xui-switch
                    ariaLabel="Normalise thickness"
                    data-testid="bake-thickness-normalised"
                    [checked]="options.thickness.normalised"
                    (checkedChange)="set({ thickness: { normalised: $event } })"
                  />
                </label>
                <p class="text-foreground-muted text-xs">
                  Uses the ambient occlusion rays, turned through the surface, out to the occluder distance.
                </p>
              }
              @case ('id') {
                <div class="flex min-w-0 flex-col gap-1">
                  <span class="text-foreground-muted text-xs">Colour by</span>
                  <xui-select
                    class="w-full"
                    ariaLabel="ID colour source"
                    data-testid="bake-id-source"
                    [itemText]="idSourceName"
                    [items]="idSources"
                    [value]="options.id.source"
                    (valueChange)="set({ id: { ...options.id, source: $event ?? 'auto' } })"
                  />
                </div>
                <p class="text-foreground-muted text-xs">
                  Automatic takes vertex colours, then materials when there are several, then each connected shell.
                </p>
                <div class="flex items-center justify-between gap-2 text-sm">
                  Colours
                  <xui-segmented-control
                    size="sm"
                    ariaLabel="ID colours"
                    data-testid="bake-id-palette"
                    [options]="palettes"
                    [value]="options.id.palette"
                    (valueChange)="set({ id: { ...options.id, palette: $event ?? options.id.palette } })"
                  />
                </div>
              }
              @default {
                <p class="text-foreground-muted text-xs">This map has no settings of its own.</p>
              }
            }
          }
        </div>
      </div>
    }

    @if (settings.failure(); as reason) {
      <xui-callout color="error" data-testid="bake-settings-failure" compact>
        The settings were not kept: {{ reason }}
      </xui-callout>
    }
    @if (refused() ?? settings.refused(); as reason) {
      <xui-callout color="error" data-testid="bake-refused" compact>{{ reason }}</xui-callout>
    }

    @if (store.current(); as job) {
      <div class="well flex max-h-[40%] shrink-0 flex-col gap-1.5 overflow-y-auto p-2" data-testid="bake-job">
        <div class="flex items-center gap-2">
          <span class="readout" data-testid="bake-state">{{ stateName(job.state) }}</span>
          @if (store.batch().length > 1) {
            <span class="text-foreground-muted text-xs"
              >{{ finished() }} of {{ store.batch().length }} texture sets</span
            >
          }
          <span class="readout ms-auto" data-testid="bake-percent">{{ percent() }}%</span>
        </div>
        <div
          class="bg-surface-sunken h-1.5 shrink-0 overflow-hidden rounded-sm"
          role="progressbar"
          aria-label="Bake progress"
          [attr.aria-valuenow]="percent()"
          aria-valuemin="0"
          aria-valuemax="100"
        >
          <div class="bg-primary h-full" [style.width.%]="percent()"></div>
        </div>
        @for (each of store.batch(); track each.id) {
          @if (store.batch().length > 1) {
            <p class="section-label mt-1">{{ each.bake?.material }}: {{ stateName(each.state) }}</p>
          }
          @if (each.error; as error) {
            <xui-callout color="error" compact>{{ error }}</xui-callout>
          }
          @if (each.result; as result) {
            @if (result.cached) {
              <span class="text-foreground-muted text-xs" data-testid="bake-cached"
                >Already baked: nothing has changed.</span
              >
            }
            @if (result.missed > 0) {
              <span class="text-foreground-muted text-xs">
                {{ result.missed }} of {{ result.covered }} texels found no surface and took the nearest point.
              </span>
            }
            @for (warning of result.warnings; track warning) {
              <xui-callout color="warning" compact>{{ warning }}</xui-callout>
            }
            <ul class="flex flex-wrap gap-0.5">
              @for (map of result.maps; track map.map) {
                <li>
                  <button
                    type="button"
                    xuiButton
                    size="sm"
                    variant="ghost"
                    [attr.data-testid]="'bake-result-' + map.map"
                    [title]="map.path"
                    (click)="store.show(each, map.map)"
                  >
                    {{ label(map.map) }}
                  </button>
                </li>
              }
            </ul>
            @if (result.log.length) {
              <details data-testid="bake-log">
                <summary class="text-foreground-muted cursor-pointer text-xs">Log</summary>
                <ul class="readout flex flex-col gap-0.5 py-1 text-xs">
                  @for (line of result.log; track $index) {
                    <li>{{ line }}</li>
                  }
                </ul>
              </details>
            }
          }
        }
        @if (job.result) {
          <div class="flex flex-wrap gap-1">
            @if (job.bake?.options?.space === 'tangent' && hasNormal(job)) {
              <button
                type="button"
                xuiButton
                size="sm"
                variant="ghost"
                data-testid="bake-show-on-mesh"
                title="The low mesh with these maps, in the viewport"
                (click)="showOnMesh(job)"
              >
                Show on low mesh
              </button>
            }
            @if (graph.document(); as document) {
              <button
                type="button"
                xuiButton
                size="sm"
                variant="ghost"
                data-testid="bake-use-in-graph"
                [title]="'Mesh Map nodes in ' + document.name + ' read this bake'"
                (click)="store.useInGraph(document.id, job)"
              >
                Use in {{ document.name }}
              </button>
            }
          </div>
        }
      </div>
    }

    <footer class="chrome-bar flex h-8 shrink-0 items-center gap-2 px-2">
      @if (settings.blocked(); as reason) {
        <span class="text-foreground-muted min-w-0 truncate text-xs" data-testid="bake-blocked" [title]="reason">
          {{ reason }}
        </span>
      }
      @if (store.busy()) {
        <button type="button" xuiButton size="sm" variant="ghost" data-testid="bake-cancel" (click)="store.cancelAll()">
          Stop
        </button>
      }
      <button
        type="button"
        xuiButton
        class="ms-auto"
        color="primary"
        size="sm"
        data-testid="bake-start"
        [disabled]="settings.blocked() !== null"
        (click)="start()"
      >
        {{ bakeLabel() }}
      </button>
    </footer>
  `
})
export class BakePane {
  protected readonly store = inject(BakeStore);
  protected readonly settings = inject(BakeSettings);
  private readonly project = inject(ProjectStore);
  private readonly status = inject(SystemStatus);
  protected readonly graph = inject(GraphStore);
  protected readonly Math = Math;
  protected readonly String = String;
  protected readonly Number = Number;
  protected readonly maps = Maps;
  protected readonly formats = Formats;
  protected readonly spaces = Spaces;
  protected readonly units = Units;
  protected readonly heightRanges = HeightRanges;
  protected readonly distributions = Distributions;
  protected readonly palettes = Palettes;
  protected readonly falloffs = Falloffs;
  protected readonly idSources = IdSources;
  protected readonly formatName = imageFormatName;
  protected readonly stateName = jobStateName;
  /** A map by the label the stack vocabulary gives it, as a bake mask names it. */
  protected readonly label = termName;
  protected readonly share = percent;
  protected readonly falloffName = (value: string) => FalloffNames[value] ?? value;
  protected readonly idSourceName = (value: string) => IdSourceNames[value] ?? value;
  protected readonly sideName = (value: string) => `${value} × ${value}`;
  protected readonly canPick = desktop() !== undefined;
  protected readonly pathFields: readonly { key: 'cage' | 'skewMap' | 'output'; label: string; placeholder: string }[] =
    [
      { key: 'cage', label: 'Cage', placeholder: 'None: rays start on the low poly mesh' },
      { key: 'skewMap', label: 'Skew map', placeholder: 'None: the skew applies everywhere' },
      { key: 'output', label: 'Output folder', placeholder: 'The project’s bakes folder' }
    ];

  protected readonly section = signal<Section>('common');
  protected readonly refused = signal<string | null>(null);
  protected readonly options = this.settings.options;
  protected readonly sides = computed(() => this.status.textureSides().map(String));

  protected readonly chosen = computed(() => new Set(this.settings.chosen().map(set => set.material)));

  protected readonly ownHigh = computed(() => {
    const high = this.settings.high()[0] ?? '';
    return high === this.settings.low() ? '' : high;
  });

  protected readonly setsSaid = computed(() => {
    const sets = this.settings.sets();
    const chosen = this.settings.chosen();
    if (sets.length === 0) {
      return '';
    }

    return chosen.length === sets.length
      ? `, all ${sets.length} texture sets`
      : `, ${chosen.length} of ${sets.length} texture sets`;
  });

  protected readonly absolute = computed(() => this.options()?.rays.units === 'absolute');
  protected readonly unit = computed(() => (this.absolute() ? 'model units' : '% of size'));
  protected readonly step = computed(() => (this.absolute() ? 0.01 : 1));
  protected readonly unitsSaid = computed(() =>
    this.absolute()
      ? 'In the model’s own units. The same numbers reach less far on a model exported larger.'
      : 'As a percentage of the high mesh’s size, its bounding box’s diagonal, which suits a model at any scale.'
  );

  protected readonly finished = computed(
    () => this.store.batch().filter(job => job.state !== 'queued' && job.state !== 'running').length
  );
  protected readonly percent = computed(() => {
    const jobs = this.store.batch();
    return jobs.length === 0 ? 0 : Math.round((jobs.reduce((sum, job) => sum + job.progress, 0) / jobs.length) * 100);
  });

  protected readonly bakeLabel = computed(() => {
    const count = this.settings.sets().length > 0 ? this.settings.chosen().length : 0;
    return count > 1 ? `Bake ${count} texture sets` : 'Bake';
  });

  /** A distance as the field shows it: a percentage of the size, or model units. */
  protected shown(value: number): number {
    return this.absolute() ? value : Math.round(value * 100 * 1000) / 1000;
  }

  /** A distance from the field, as the settings keep it. */
  protected stored(value: number | null): number {
    const number = value ?? 0;
    return this.absolute() ? number : number / 100;
  }

  protected about(name: string): string {
    return Maps.find(map => map.name === name)?.about ?? '';
  }

  protected hasNormal(job: JobState): boolean {
    return job.result?.maps.some(map => map.map === 'normal') === true;
  }

  protected text(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  protected nameOf(path: string): string {
    return path.split(/[\\/]/u).pop() ?? path;
  }

  protected set(change: Partial<BakeOptions>): void {
    this.settings.set(options => ({ ...options, ...change }));
  }

  protected setRays(change: Partial<BakeRayOptions>): void {
    this.settings.set(options => ({ ...options, rays: { ...options.rays, ...change } }));
  }

  protected setOcclusion(change: Partial<BakeOcclusionOptions>): void {
    this.settings.set(options => ({ ...options, occlusion: { ...options.occlusion, ...change } }));
  }

  protected setMap(name: string, on: boolean): void {
    this.settings.set(options => {
      const maps = new Set(options.maps);
      if (on) {
        maps.add(name);
      } else {
        maps.delete(name);
      }

      // In the order a bake reports them, so the file does not churn with the order they were ticked in.
      return { ...options, maps: Maps.map(map => map.name).filter(map => maps.has(map)) };
    });
  }

  protected setSet(material: string, on: boolean): void {
    const all = this.settings.sets().map(set => set.material);
    const ticked = new Set(this.chosen());
    if (on) {
      ticked.add(material);
    } else {
      ticked.delete(material);
    }

    // Every set ticked is kept as none, which is every set: one added to the model later is baked too.
    this.settings.update(settings => ({
      ...settings,
      sets: ticked.size === all.length ? [] : all.filter(set => ticked.has(set))
    }));
  }

  protected allSets(): void {
    this.settings.update(settings => ({ ...settings, sets: [] }));
  }

  protected path(field: 'cage' | 'skewMap' | 'output'): string {
    const settings = this.settings.settings();
    switch (field) {
      case 'cage':
        return settings?.cage ?? '';
      case 'skewMap':
        return settings?.options.rays.skewMap ?? '';
      default:
        return this.settings.output();
    }
  }

  protected setPath(field: 'cage' | 'skewMap' | 'output', value: string): void {
    const path = value.trim();
    if (field === 'output') {
      this.settings.chooseOutput(path);
    } else if (field === 'cage') {
      this.settings.update(settings => ({ ...settings, cage: path }));
    } else {
      this.setRays({ skewMap: path });
    }
  }

  protected async pick(field: 'cage' | 'skewMap' | 'output'): Promise<void> {
    const bridge = desktop();
    const path = await (field === 'output'
      ? bridge?.openFolder()
      : field === 'skewMap'
        ? bridge?.openImage(this.status.pictures())
        : bridge?.openMesh());
    if (path) {
      this.setPath(field, path);
    }
  }

  /** The mesh's own detailed mesh is brought into the project, as its mesh is. */
  protected async pickHigh(): Promise<void> {
    const path = await desktop()?.openMesh();
    if (path) {
      await this.project.applySettings({ high: path });
    }
  }

  protected async typedHigh(path: string): Promise<void> {
    if (path.trim()) {
      await this.project.applySettings({ high: path.trim() });
    }
  }

  protected async addHigh(): Promise<void> {
    const path = await desktop()?.openMesh();
    if (path) {
      this.settings.update(settings => ({ ...settings, high: [...settings.high, path] }));
    }
  }

  protected removeHigh(index: number): void {
    this.settings.update(settings => ({ ...settings, high: settings.high.filter((_, at) => at !== index) }));
  }

  protected async start(): Promise<void> {
    this.refused.set(null);
    await this.settings.bake();
  }

  protected async showOnMesh(job: JobState): Promise<void> {
    this.refused.set(null);
    try {
      await this.store.showOnMesh(job);
    } catch (error) {
      this.refused.set(messageOf(error));
    }
  }
}
