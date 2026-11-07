import { provideIcons } from '@ng-icons/core';
import {
  matBrush,
  matDeleteOutline,
  matFolder,
  matFormatColorFill,
  matRedo,
  matSave,
  matTune,
  matUndo
} from '@ng-icons/material-icons/baseline';
import { ChangeDetectionStrategy, Component, ElementRef, computed, inject, signal } from '@angular/core';
import type { StackLayer } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiSelectImports } from '@xui/select';
import { XuiSliderImports } from '@xui/slider';
import { IconImports } from '../core/icons';
import { blendName as blendLabelOf, channelName } from '../core/labels';
import { ProjectStore } from '../library/project-store';
import { type DropZone, dropManyCommands } from './layer-drag';
import { hexOf } from './layer-panel';
import { MaterialUse } from './material-use';
import { type LayerRow, PaintStore, toggledEffect } from './paint-store';

const RowHeight = 56;
const EffectHeight = 32;

/** One thing added to a layer's mask. No effect index means the generator that is the mask. */
interface LayerWork {
  key: string;
  name: string;
  enabled: boolean;
  effect: number | null;
}

/** The filters the menu offers in one click. The rest are in its list. */
const QuickSteps: readonly { node: string; name: string }[] = [
  { node: 'Colour/Levels', name: 'Levels' },
  { node: 'Filters/Blur', name: 'Blur' },
  { node: 'Colour/Invert', name: 'Invert' }
];

const KindMarks: Readonly<Record<string, string>> = { paint: 'P', fill: 'F', filter: 'Fx', group: '' };

const MaskMarks: Readonly<Record<string, string>> = {
  Constant: 'C',
  Texture: 'T',
  Anchor: 'A',
  Generator: 'G',
  Bake: 'B',
  Paint: 'P'
};

interface MenuAt {
  x: number;
  y: number;
  layer: StackLayer;
}

/**
 * The layer stack, top first. A row is what a layer is, its mask beside it, and how it blends. Dragging moves layers
 * and folders; clicking a layer or its mask chooses what the brush paints.
 */
@Component({
  selector: 'ptn-layer-stack',
  imports: [IconImports, XuiButtonImports, XuiSelectImports, XuiSliderImports],
  viewProviders: [
    provideIcons({ matBrush, matDeleteOutline, matFolder, matFormatColorFill, matRedo, matSave, matTune, matUndo })
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'flex h-full min-h-0 flex-col',
    '(document:click)': 'menu.set(null); meshes.set(null)',
    '(document:pointerup)': 'letGo()',
    '(document:keydown)': 'press($event)'
  },
  template: `
    <div class="chrome-bar flex shrink-0 items-center gap-0.5 px-1 py-0.5" role="toolbar" aria-label="Layers">
      <xui-select
        class="w-24 min-w-0 shrink-0 [&_button]:text-xs"
        style="--control-height-md: 1.375rem; --control-padding-md: 0.375rem"
        ariaLabel="Channel"
        data-testid="layers-channel"
        [filterable]="false"
        [itemText]="channelLabel"
        [items]="channels()"
        [matchTargetWidth]="false"
        [value]="store.channel()"
        (valueChange)="store.showChannel($event ?? store.channel())"
      />
      <span class="flex-1"></span>
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        class="w-6 shrink-0 px-0"
        data-testid="paint-add"
        title="Add a paint layer"
        aria-label="Add a paint layer"
        (click)="add('paint', 'Paint')"
      >
        <ng-icon xui size="sm" name="matBrush" />
      </button>
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        class="w-6 shrink-0 px-0"
        data-testid="paint-add-fill"
        title="Add a fill layer"
        aria-label="Add a fill layer"
        (click)="add('fill', 'Fill')"
      >
        <ng-icon xui size="sm" name="matFormatColorFill" />
      </button>
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        class="w-6 shrink-0 px-0"
        data-testid="paint-add-filter"
        title="Add a filter layer"
        aria-label="Add a filter layer"
        (click)="add('filter', 'Filter', 'Levels')"
      >
        <ng-icon xui size="sm" name="matTune" />
      </button>
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        class="w-6 shrink-0 px-0"
        data-testid="paint-add-group"
        title="Add a folder"
        aria-label="Add a folder"
        (click)="add('group', 'Folder')"
      >
        <ng-icon xui size="sm" name="matFolder" />
      </button>
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        class="w-6 shrink-0 px-0"
        data-testid="paint-remove-selected"
        title="Delete the selected layers"
        aria-label="Delete the selected layers"
        [disabled]="store.selection().size === 0"
        (click)="removeSelected()"
      >
        <ng-icon xui size="sm" name="matDeleteOutline" />
      </button>
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        class="w-6 shrink-0 px-0"
        data-testid="paint-undo"
        title="Undo"
        aria-label="Undo"
        [disabled]="!store.state()?.history?.canUndo"
        (click)="store.act('undo')"
      >
        <ng-icon xui size="sm" name="matUndo" />
      </button>
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        class="w-6 shrink-0 px-0"
        data-testid="paint-redo"
        title="Redo"
        aria-label="Redo"
        [disabled]="!store.state()?.history?.canRedo"
        (click)="store.act('redo')"
      >
        <ng-icon xui size="sm" name="matRedo" />
      </button>
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        class="w-6 shrink-0 px-0"
        data-testid="paint-save"
        title="Save"
        aria-label="Save"
        [disabled]="!store.state()?.history?.dirty"
        (click)="store.act('save')"
      >
        <ng-icon xui size="sm" name="matSave" />
      </button>
      @if (store.state()?.history?.dirty) {
        <span class="text-foreground-muted text-xs" data-testid="paint-unsaved" title="Unsaved changes">•</span>
      }
    </div>

    @if (project.bakeOutOfDate()) {
      <div class="bg-warning-subtle flex shrink-0 items-center gap-2 px-2 py-1 text-xs" data-testid="layers-stale-bake">
        <span class="min-w-0 flex-1">The mesh changed after the last bake.</span>
        <button type="button" xuiButton size="sm" (click)="project.askToBake()">Bake…</button>
      </div>
    }

    @if (store.unbaked(); as maps) {
      <div class="bg-warning-subtle flex shrink-0 items-center gap-2 px-2 py-1 text-xs" data-testid="layers-need-bake">
        <span class="min-w-0 flex-1" [title]="maps">Some masks need baked maps, and show grey until then.</span>
        <button type="button" xuiButton size="sm" data-testid="layers-bake" (click)="project.askToBake()">Bake…</button>
      </div>
    }

    @if (store.evaluated(); as evaluated) {
      <div class="readout text-foreground-muted shrink-0 truncate px-2 text-xs" data-testid="layers-evaluation">
        {{ evaluated }}
      </div>
    }

    @if (store.problems().length > 0) {
      <ul class="well max-h-24 shrink-0 overflow-auto text-xs" aria-label="Problems" data-testid="layers-problems">
        @for (item of store.problems(); track $index) {
          <li>
            <button
              type="button"
              class="hover:bg-surface-hover flex w-full items-baseline gap-2 px-2 py-0.5 text-start"
              [disabled]="!item.layer"
              (click)="reveal(item.layer)"
            >
              <span class="readout shrink-0" [class.text-error]="item.severity === 'Error'">{{ item.severity }}</span>
              <span class="truncate" [title]="item.message">{{ item.message }}</span>
            </button>
          </li>
        }
      </ul>
    }

    <div
      class="min-h-0 flex-1 overflow-auto"
      role="tree"
      aria-label="Layers"
      aria-multiselectable="true"
      data-testid="paint-layers"
      (dragover)="overList($event)"
      (drop)="dropOnList($event)"
    >
      @for (row of rows(); track row.layer.id) {
        <div
          class="border-hairline relative flex items-center gap-1.5 border-b pe-2"
          role="treeitem"
          [attr.aria-level]="row.depth + 1"
          [attr.draggable]="sliding() !== row.layer.id"
          [attr.aria-selected]="store.selection().has(row.layer.id)"
          [attr.aria-expanded]="row.layer.kind === 'group' ? !closed().has(row.layer.id) : null"
          [attr.data-testid]="'paint-layer-' + row.layer.id"
          [class.bg-selection]="store.selection().has(row.layer.id)"
          [class.opacity-50]="!row.layer.enabled"
          [style.height.px]="row.effect ? effectHeight : rowHeight"
          [style.padding-inline-start.px]="row.depth * 14 + 4"
          (click)="click($event, row.layer)"
          (contextmenu)="openMenu($event, row.layer)"
          (dragstart)="start($event, row.layer.id)"
          (dragover)="over($event, row)"
          (dragleave)="target.set(null)"
          (dragend)="dragged = []"
          (drop)="drop($event, row)"
          (keydown.enter)="choose(row.layer, false)"
          tabindex="0"
        >
          @if (target()?.id === row.layer.id) {
            <span
              class="bg-accent pointer-events-none absolute inset-x-0 h-0.5"
              data-testid="layer-drop-line"
              [class.inset-y-0]="target()?.zone === 'into'"
              [class.h-full]="target()?.zone === 'into'"
              [class.opacity-30]="target()?.zone === 'into'"
              [class.top-0]="target()?.zone === 'above'"
              [class.bottom-0]="target()?.zone === 'below'"
            ></span>
          }
          <button
            type="button"
            class="grid size-5 shrink-0 place-items-center"
            data-testid="layer-eye"
            [attr.aria-label]="(row.layer.enabled ? 'Hide ' : 'Show ') + row.layer.name"
            [attr.aria-pressed]="row.layer.enabled"
            (click)="$event.stopPropagation(); store.setEnabled(row.layer, !row.layer.enabled)"
          >
            <svg viewBox="0 0 16 16" class="size-4" aria-hidden="true">
              <path d="M1 8s2.6-4.5 7-4.5S15 8 15 8s-2.6 4.5-7 4.5S1 8 1 8z" fill="none" stroke="currentColor" />
              @if (row.layer.enabled) {
                <circle cx="8" cy="8" r="2.2" fill="currentColor" />
              }
            </svg>
          </button>

          @if (row.effect) {
            <span class="text-foreground-muted grid size-5 shrink-0 place-items-center text-xs" data-testid="layer-effect-mark">
              {{ mark(row.layer) }}
            </span>
          } @else if (row.layer.kind === 'group') {
            <button
              type="button"
              class="text-foreground-muted grid size-4 shrink-0 place-items-center"
              data-testid="layer-toggle"
              [attr.aria-label]="(closed().has(row.layer.id) ? 'Open ' : 'Close ') + row.layer.name"
              (click)="$event.stopPropagation(); toggle(row.layer.id)"
            >
              <svg
                viewBox="0 0 16 16"
                class="size-3"
                [class.rotate-90]="!closed().has(row.layer.id)"
                aria-hidden="true"
              >
                <path d="M6 3l5 5-5 5z" fill="currentColor" />
              </svg>
            </button>
            <svg viewBox="0 0 16 16" class="text-foreground-muted size-8 shrink-0" aria-hidden="true">
              <path d="M1.5 3.5h5l1.5 2h6.5v7.5h-13z" fill="currentColor" fill-opacity="0.45" />
            </svg>
          } @else {
            <span class="flex shrink-0 flex-col gap-0.5">
              <span
                class="border-hairline text-foreground-muted relative grid size-8 place-items-center overflow-hidden rounded-full border"
                data-testid="layer-thumbnail"
                [class.outline]="row.layer.id === store.layer() && !store.masking()"
                [style.background]="swatch(row.layer)"
              >
                @if (store.thumbnails().get(row.layer.id); as picture) {
                  <img class="size-full object-cover" alt="" [src]="picture" />
                } @else {
                  {{ mark(row.layer) }}
                }
                <span
                  class="pointer-events-none absolute inset-0 rounded-full"
                  style="background: radial-gradient(circle at 35% 30%, rgb(255 255 255 / 0.5), transparent 45%, rgb(0 0 0 / 0.5))"
                  aria-hidden="true"
                ></span>
              </span>
              <span
                class="h-[3px] w-8 rounded-full"
                data-testid="layer-content-mark"
                [class.bg-surface-sunken]="!hasContent(row.layer)"
                [class.bg-warning]="hasContent(row.layer)"
              ></span>
            </span>
          }

          @if (row.layer.mask.source !== 'None') {
            <span class="flex shrink-0 flex-col gap-0.5">
              <button
                type="button"
                class="border-hairline bg-background text-foreground-muted grid size-8 place-items-center rounded-sm border text-xs"
                data-testid="layer-mask"
                [attr.aria-label]="'Mask of ' + row.layer.name"
                [class.outline]="row.layer.id === store.layer() && store.masking()"
                [style.background]="maskColour(row.layer)"
                [title]="row.layer.mask.source + ' mask'"
                (click)="$event.stopPropagation(); choose(row.layer, true)"
              >
                <svg viewBox="0 0 16 16" class="size-5" aria-hidden="true">
                  <path d="M1 1h14v14H1z" fill="currentColor" fill-opacity="0.35" />
                  <circle cx="8" cy="8" r="4.5" fill="currentColor" />
                </svg>
              </button>
              <span
                class="h-[3px] w-8 rounded-full"
                data-testid="layer-mask-mark"
                [class.bg-surface-sunken]="!hasMaskWork(row.layer)"
                [class.bg-warning]="hasMaskWork(row.layer)"
              ></span>
            </span>
          }

          @if (store.faults().get(row.layer.id); as fault) {
            <span
              class="shrink-0 text-xs font-semibold"
              data-testid="layer-fault"
              role="img"
              [attr.aria-label]="fault.message"
              [class.text-error]="fault.severity === 'Error'"
              [class.text-warning]="fault.severity !== 'Error'"
              [title]="fault.message"
              >!</span
            >
          }

          <span class="min-w-0 flex-1 leading-tight">
            @if (renaming() === row.layer.id) {
              <input
                class="w-full bg-transparent text-sm"
                aria-label="Layer name"
                data-testid="layer-rename"
                [value]="row.layer.name"
                (click)="$event.stopPropagation()"
                (blur)="rename(row.layer, $event)"
                (keydown.enter)="blur($event)"
                (keydown.escape)="renaming.set(null)"
              />
            } @else {
              <span class="block truncate text-sm" [class.font-semibold]="row.layer.id === store.layer()">
                {{ row.layer.name }}
              </span>
            }
          </span>

          @if (!row.effect) {
            <button
              type="button"
              class="border-hairline bg-background text-foreground-muted grid size-8 shrink-0 place-items-center rounded-sm border text-xs"
              data-testid="layer-mesh-mask"
              [attr.aria-label]="'Meshes of ' + row.layer.name"
              [attr.aria-pressed]="!!row.layer.mask.meshParts"
              [class.bg-selection]="!!row.layer.mask.meshParts"
              [title]="row.layer.mask.meshParts ? 'On some meshes' : 'On every mesh'"
              (click)="$event.stopPropagation(); openMeshes($event, row.layer)"
            >
              <svg viewBox="0 0 16 16" class="size-4" aria-hidden="true">
                <path d="M2 2h12v12H2z" fill="none" stroke="currentColor" stroke-width="1.5" />
                @if (row.layer.mask.meshParts) {
                  <path d="M2 2h6v6H2z" fill="currentColor" />
                }
              </svg>
            </button>
          }

          <span
            class="flex shrink-0"
            [class.flex-col]="!row.effect"
            [class.gap-1]="row.effect"
            [class.gap-2]="!row.effect"
            [class.items-center]="row.effect"
            [class.w-20]="!row.effect"
            [class.w-40]="row.effect"
            role="group"
            tabindex="-1"
            data-testid="layer-blending"
            [attr.aria-label]="'Blending of ' + row.layer.name"
            (click)="$event.stopPropagation()"
            (keydown)="$event.stopPropagation()"
            (pointerdown)="sliding.set(row.layer.id)"
          >
            <xui-select
              class="min-w-0 [&_button]:text-xs"
              style="--control-height-md: 1.375rem; --control-padding-md: 0.375rem"
              data-testid="paint-layer-blend"
              [ariaLabel]="'Blend mode of ' + row.layer.name"
              [filterable]="false"
              [class.w-20]="row.effect"
              [class.w-full]="!row.effect"
              [itemText]="blendLabel"
              [items]="blends()"
              [matchTargetWidth]="false"
              [value]="row.layer.blend"
              (valueChange)="setBlend(row.layer, $event)"
            />
            <span class="flex min-w-0 flex-1 items-center gap-1">
              <xui-slider
                class="h-4 min-w-0 flex-1 pb-0"
                labelStepSize="0"
                data-testid="paint-layer-opacity"
                [attr.aria-label]="'Opacity of ' + row.layer.name"
                [max]="1"
                [min]="0"
                [stepSize]="0.01"
                [value]="opacityOf(row.layer)"
                (valueChange)="setOpacity(row.layer, $event)"
              />
              <span class="readout w-7 shrink-0 text-end text-xs">{{ round(opacityOf(row.layer)) }}</span>
            </span>
          </span>

          @if (row.effect) {
            <button
              type="button"
              class="text-foreground-muted grid size-5 shrink-0 place-items-center"
              data-testid="layer-effect-remove"
              [attr.aria-label]="'Remove ' + row.layer.name"
              (click)="$event.stopPropagation(); remove(row.layer)"
            >
              ×
            </button>
          }

        </div>
        @for (item of workOf(row.layer); track item.key) {
          <div
            class="border-hairline flex h-7 items-center gap-1.5 border-b pe-2"
            data-testid="layer-work"
            [style.padding-inline-start.px]="row.depth * 14 + 34"
          >
            <button
              type="button"
              class="grid size-5 shrink-0 place-items-center"
              data-testid="layer-work-eye"
              [attr.aria-label]="(item.enabled ? 'Hide ' : 'Show ') + item.name"
              [attr.aria-pressed]="item.enabled"
              [disabled]="item.effect === null"
              (click)="setWork(row.layer, item.effect, !item.enabled)"
            >
              <svg viewBox="0 0 16 16" class="size-3.5" aria-hidden="true">
                <path d="M1 8s2.6-4.5 7-4.5S15 8 15 8s-2.6 4.5-7 4.5S1 8 1 8z" fill="none" stroke="currentColor" stroke-width="1.3" />
                @if (item.enabled) {
                  <circle cx="8" cy="8" r="2.2" fill="currentColor" />
                }
              </svg>
            </button>
            <button
              type="button"
              class="hover:bg-selection min-w-0 flex-1 truncate text-start text-xs"
              (click)="choose(row.layer, true)"
            >
              {{ item.name }}
            </button>
            <button
              type="button"
              class="text-foreground-muted grid size-5 shrink-0 place-items-center"
              data-testid="layer-work-remove"
              [attr.aria-label]="'Take off ' + item.name"
              (click)="removeWork(row.layer, item.effect)"
            >
              ×
            </button>
          </div>
        }
      } @empty {
        <p class="text-foreground-muted p-3 text-sm">Nothing open.</p>
      }
    </div>

    @if (meshes(); as at) {
      <div
        class="bg-surface-raised border-hairline fixed z-50 flex max-h-80 min-w-44 flex-col gap-1 overflow-auto border p-2 text-sm"
        role="group"
        aria-label="Meshes this layer is on"
        data-testid="layer-meshes"
        [style.left.px]="at.x"
        [style.top.px]="at.y"
        (click)="$event.stopPropagation()"
        (keydown.escape)="meshes.set(null)"
      >
        @for (part of parts(); track part) {
          <label class="flex items-center gap-2">
            <input
              type="checkbox"
              [attr.data-testid]="'layer-mesh-' + part"
              [checked]="onPart(at.layer, part)"
              [disabled]="isLastPart(at.layer, part)"
              (change)="setPart(at.layer, part, checked($event))"
            />
            <span class="truncate">{{ part }}</span>
          </label>
        } @empty {
          <span class="text-foreground-muted text-xs">This mesh has no named parts.</span>
        }
      </div>
    }

    @if (menu(); as at) {
      <div
        class="bg-surface-raised border-hairline fixed z-50 flex min-w-44 flex-col rounded border py-1 text-sm shadow-lg"
        role="menu"
        tabindex="-1"
        data-testid="layer-menu"
        [style.left.px]="at.x"
        [style.top.px]="at.y"
        (click)="$event.stopPropagation()"
        (keydown.escape)="menu.set(null)"
      >
        <button
          type="button"
          class="hover:bg-selection px-3 py-1 text-start"
          role="menuitem"
          data-testid="layer-menu-rename"
          (click)="startRename(at.layer)"
        >
          Rename
        </button>
        <div class="border-hairline my-1 border-t"></div>
        @if (at.layer.mask.source === 'None') {
          <button
            type="button"
            class="hover:bg-selection px-3 py-1 text-start"
            role="menuitem"
            data-testid="layer-menu-mask-white"
            (click)="addValueMask(at.layer, 1)"
          >
            Add White Mask
          </button>
          <button
            type="button"
            class="hover:bg-selection px-3 py-1 text-start"
            role="menuitem"
            data-testid="layer-menu-mask-black"
            (click)="addValueMask(at.layer, 0)"
          >
            Add Black Mask
          </button>
          <button
            type="button"
            class="hover:bg-selection px-3 py-1 text-start"
            role="menuitem"
            data-testid="layer-menu-mask-bitmap"
            (click)="addBitmapMask(at.layer)"
          >
            Add Bitmap Mask…
          </button>
          <button
            type="button"
            class="hover:bg-selection px-3 py-1 text-start"
            role="menuitem"
            data-testid="layer-menu-mask-paint"
            (click)="addMask(at.layer, 'Paint')"
          >
            Add Paint Mask
          </button>
          <button
            type="button"
            class="hover:bg-selection px-3 py-1 text-start"
            role="menuitem"
            data-testid="layer-menu-mask-colour"
            (click)="addColourMask(at.layer)"
          >
            Add Colour Selection Mask
          </button>
          <button
            type="button"
            class="hover:bg-selection px-3 py-1 text-start"
            role="menuitem"
            data-testid="layer-menu-mask-generator"
            (click)="addMask(at.layer, 'Generator')"
          >
            Add Generator
          </button>
        } @else {
          <button
            type="button"
            class="hover:bg-selection px-3 py-1 text-start"
            role="menuitem"
            data-testid="layer-menu-mask-invert"
            (click)="invertMask(at.layer)"
          >
            Invert Mask
          </button>
          <button
            type="button"
            class="hover:bg-selection px-3 py-1 text-start"
            role="menuitem"
            data-testid="layer-menu-mask-remove"
            (click)="addMask(at.layer, 'None')"
          >
            Remove Mask
          </button>
        }
        <div class="border-hairline my-1 border-t"></div>
        @if (!store.masking()) {
          <button
            type="button"
            class="hover:bg-selection px-3 py-1 text-start"
            role="menuitem"
            data-testid="layer-menu-effect-paint"
            (click)="addContent(at.layer, 'paint', 'Paint')"
          >
            Add Paint
          </button>
          <button
            type="button"
            class="hover:bg-selection px-3 py-1 text-start"
            role="menuitem"
            data-testid="layer-menu-effect-fill"
            (click)="addContent(at.layer, 'fill', 'Fill')"
          >
            Add Fill
          </button>
        }
        @for (kind of quickSteps; track kind.node) {
          <button
            type="button"
            class="hover:bg-selection px-3 py-1 text-start"
            role="menuitem"
            [attr.data-testid]="'layer-menu-effect-' + kind.name"
            (click)="addStep(at.layer, kind.node)"
          >
            Add {{ kind.name }}{{ store.masking() ? ' to Mask' : '' }}
          </button>
        }
        <div class="px-3 py-1">
          <xui-select
            class="w-full [&_button]:text-xs"
            data-testid="layer-menu-filter"
            searchPlaceholder="Find a filter"
            [ariaLabel]="store.masking() ? 'Add a filter to the mask' : 'Add a filter'"
            [itemText]="stepName"
            [items]="steps()"
            [matchTargetWidth]="false"
            [placeholder]="store.masking() ? 'Add Filter to Mask…' : 'Add Filter…'"
            [value]="null"
            (valueChange)="$event && addStep(at.layer, $event)"
          />
        </div>
        <div class="border-hairline my-1 border-t"></div>
        <button
          type="button"
          class="hover:bg-selection px-3 py-1 text-start"
          role="menuitem"
          data-testid="layer-menu-group"
          (click)="groupSelected()"
        >
          Group Layers
        </button>
        <button
          type="button"
          class="hover:bg-selection flex justify-between gap-4 px-3 py-1 text-start"
          role="menuitem"
          data-testid="layer-menu-duplicate"
          (click)="menu.set(null); store.duplicateLayers()"
        >
          Duplicate Layers <span class="text-foreground-muted">⌘D</span>
        </button>
        <button
          type="button"
          class="hover:bg-selection flex justify-between gap-4 px-3 py-1 text-start"
          role="menuitem"
          data-testid="layer-menu-copy"
          (click)="menu.set(null); store.copyLayers()"
        >
          Copy <span class="text-foreground-muted">⌘C</span>
        </button>
        <button
          type="button"
          class="hover:bg-selection flex justify-between gap-4 px-3 py-1 text-start"
          role="menuitem"
          data-testid="layer-menu-cut"
          (click)="menu.set(null); store.cutLayers()"
        >
          Cut <span class="text-foreground-muted">⌘X</span>
        </button>
        <button
          type="button"
          class="hover:bg-selection flex justify-between gap-4 px-3 py-1 text-start disabled:opacity-50"
          role="menuitem"
          data-testid="layer-menu-paste"
          [disabled]="!store.clip()"
          (click)="menu.set(null); store.pasteLayers()"
        >
          Paste <span class="text-foreground-muted">⌘V</span>
        </button>
        <div class="border-hairline my-1 border-t"></div>
        <button
          type="button"
          class="hover:bg-selection px-3 py-1 text-start"
          role="menuitem"
          data-testid="paint-remove"
          (click)="remove(at.layer)"
        >
          Delete Layers
        </button>
      </div>
    }
  `
})
export class LayerStack {
  protected readonly store = inject(PaintStore);
  protected readonly project = inject(ProjectStore);

  protected round(share: number): number {
    return Math.round(share * 100);
  }

  protected opacityOf(layer: StackLayer): number {
    const draft = this.draft();
    return draft?.layer === layer.id ? draft.opacity : layer.opacity;
  }

  protected setBlend(layer: StackLayer, blend: string | null): void {
    if (blend) {
      void this.store.edit({ op: 'setLayer', layer: layer.id, blend });
    }
  }

  /** One request out at a time, the newest value next, so that a fast slider costs what the Server can take. */
  protected setOpacity(layer: StackLayer, opacity: number): void {
    this.draft.set({ layer: layer.id, opacity });
    this.latest = { layer: layer.id, opacity };
    void this.sendOpacity();
  }

  private async sendOpacity(): Promise<void> {
    if (this.sendingOpacity) {
      return;
    }

    this.sendingOpacity = true;
    while (this.latest) {
      const { layer, opacity } = this.latest;
      this.latest = null;
      await this.store.edit({ op: 'setLayer', layer, opacity });
    }

    this.sendingOpacity = false;
    if (this.sliding() === null) {
      this.draft.set(null);
    }
  }

  protected letGo(): void {
    this.sliding.set(null);
    if (!this.sendingOpacity) {
      this.draft.set(null);
    }
  }

  protected async openMeshes(event: MouseEvent, layer: StackLayer): Promise<void> {
    this.meshes.set({ x: event.clientX, y: event.clientY, layer });
    this.parts.set(await this.store.meshParts());
  }

  protected onPart(layer: StackLayer, part: string): boolean {
    return layer.mask.meshParts?.includes(part) ?? true;
  }

  /** ⚠ No meshes at all means the whole model to the Server, so the last one stays: the eye hides a layer. */
  protected isLastPart(layer: StackLayer, part: string): boolean {
    return this.onPart(layer, part) && this.parts().filter(name => this.onPart(layer, name)).length === 1;
  }

  protected checked(event: Event): boolean {
    return (event.target as HTMLInputElement).checked;
  }

  protected setPart(layer: StackLayer, part: string, on: boolean): void {
    const kept = this.parts().filter(name => (name === part ? on : this.onPart(layer, name)));
    void this.store.keepToMeshes(layer, kept.length === this.parts().length ? [] : kept);
    const held = this.meshes();
    if (held) {
      // The row's layer changes under the open list when the Server answers, so the list keeps up from the store.
      this.meshes.set({ ...held, layer: { ...layer, mask: { ...layer.mask, meshParts: kept } } });
    }
  }

  /** Goes to a texture set, which is a document of the project. */
  protected chooseSet(path: string): void {
    const project = this.project.current();
    if (project) {
      void this.project.openDocument(project.path, path);
    }
  }
  protected readonly rowHeight = RowHeight;
  protected readonly quickSteps = QuickSteps;

  /** What the menu's list offers: every filter on a layer, and on a mask all but its own colour selection. */
  protected readonly steps = computed(() =>
    (this.store.masking() ? this.store.vocabulary()?.maskEffects : this.store.vocabulary()?.filters)?.map(
      kind => kind.node
    ) ?? []
  );

  protected readonly stepName = (node: string): string => {
    const kind = this.store.vocabulary()?.filters.find(held => held.node === node);
    return kind ? `${kind.name} · ${kind.category}` : node;
  };
  protected readonly effectHeight = EffectHeight;
  protected readonly closed = signal<ReadonlySet<string>>(new Set());
  protected readonly menu = signal<MenuAt | null>(null);
  protected readonly channels = computed(() => (this.store.state()?.channels ?? []).map(channel => channel.usage));
  protected readonly channelLabel = channelName;
  /** The list of meshes a layer can be kept to, where it was asked for. */
  protected readonly meshes = signal<MenuAt | null>(null);
  protected readonly parts = signal<readonly string[]>([]);
  protected readonly blends = computed(() => this.store.vocabulary()?.blends ?? []);
  protected readonly blendLabel = blendLabelOf;
  /** The row whose controls are under the hand, which must not start dragging the layer. */
  protected readonly sliding = signal<string | null>(null);
  /** The opacity under the hand, ahead of what the Server last said. */
  private readonly draft = signal<{ layer: string; opacity: number } | null>(null);
  private latest: { layer: string; opacity: number } | null = null;
  private sendingOpacity = false;
  protected readonly renaming = signal<string | null>(null);
  protected readonly target = signal<{ id: string; zone: DropZone } | null>(null);
  protected dragged: readonly string[] = [];
  private readonly using = inject(MaterialUse);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  /** The rows on screen: what is inside a closed folder is not. */
  protected readonly rows = computed(() => {
    const closed = this.closed();
    const hidden = new Set<string>();
    return this.store.layers().filter(row => {
      if (row.parent !== null && (hidden.has(row.parent) || closed.has(row.parent))) {
        hidden.add(row.layer.id);
        return false;
      }

      return true;
    });
  });

  protected async add(kind: string, name: string, filter?: string): Promise<void> {
    await this.store.addLayer(kind, name, filter);
    this.store.masking.set(false);
  }

  protected choose(layer: StackLayer, mask: boolean): void {
    this.store.marked.set(new Set());
    if (mask) {
      this.store.chooseMask(layer);
    } else {
      this.store.layer.set(layer.id);
      this.store.masking.set(false);
    }
  }

  /** Goes to the layer a problem is about, opening the folders it is in. */
  protected reveal(id: string): void {
    const rows = this.store.layers();
    const closed = new Set(this.closed());
    for (let at = rows.find(row => row.layer.id === id); at?.parent; ) {
      closed.delete(at.parent);
      const parent = at.parent;
      at = rows.find(row => row.layer.id === parent);
    }

    this.closed.set(closed);
    const row = rows.find(held => held.layer.id === id);
    if (row) {
      this.choose(row.layer, false);
    }
  }

  /** A click chooses one layer, Cmd adds or takes away, and Shift takes everything from the current layer to here. */
  protected click(event: MouseEvent, layer: StackLayer): void {
    if (event.shiftKey && this.store.layer() !== null) {
      this.selectTo(layer.id);
    } else if (event.metaKey || event.ctrlKey) {
      this.toggleSelected(layer.id);
    } else {
      this.choose(layer, false);
    }
  }

  private selectTo(id: string): void {
    const ids = this.rows().map(row => row.layer.id);
    const from = ids.indexOf(this.store.layer() ?? id);
    const to = ids.indexOf(id);
    this.store.marked.set(new Set(ids.slice(Math.min(from, to), Math.max(from, to) + 1)));
  }

  private toggleSelected(id: string): void {
    const selected = new Set(this.store.selection());
    if (selected.delete(id)) {
      if (this.store.layer() === id) {
        this.store.layer.set([...selected].at(-1) ?? null);
      }
    } else {
      selected.add(id);
      this.store.layer.set(id);
    }

    this.store.marked.set(selected);
    this.store.masking.set(false);
  }

  /** Delete takes what is selected, as does Backspace, which is what a Mac's delete key sends, and Cmd+A selects all. */
  protected press(event: KeyboardEvent): void {
    if (!this.keysAreOurs(event)) {
      return;
    }

    if (event.key === 'Delete' || event.key === 'Backspace') {
      event.preventDefault();
      void this.removeInFront();
    } else if (event.key === 'g' && (event.metaKey || event.ctrlKey)) {
      event.preventDefault();
      void this.groupSelected();
    } else if (event.key === 'a' && (event.metaKey || event.ctrlKey)) {
      event.preventDefault();
      this.store.marked.set(new Set(this.rows().map(row => row.layer.id)));
    } else if (event.key === 'd' && (event.metaKey || event.ctrlKey)) {
      event.preventDefault();
      void this.store.duplicateLayers();
    }
  }

  /** Keys belong to the stack when focus is in it or nowhere, and never while something is being typed. */
  private keysAreOurs(event: KeyboardEvent): boolean {
    const target = event.target as HTMLElement;
    if (target.closest('input, textarea, select, [contenteditable=true], [role=dialog]')) {
      return false;
    }

    return target === globalThis.document.body || this.host.nativeElement.contains(target);
  }

  /** The Delete key takes what is in front: a mask that is, or else the layers. Anything that says layers takes those. */
  private async removeInFront(): Promise<void> {
    const layer = this.store.chosen();
    if (this.store.masking() && layer) {
      this.store.masking.set(false);
      await this.store.setMask(layer, { ...layer.mask, source: 'None' });
    } else {
      await this.removeSelected();
    }
  }

  protected async removeSelected(): Promise<void> {
    this.store.masking.set(false);
    const selected = this.store.selection();
    const rows = this.store.layers();
    const going = rows.filter(row => selected.has(row.layer.id));
    const first = this.rows().findIndex(row => selected.has(row.layer.id));
    const top = going.filter(row => row.parent === null || !selected.has(row.parent));
    if (top.length === 0) {
      return;
    }

    await this.store.edit(...top.map(row => ({ op: 'removeLayer' as const, layer: row.layer.id })));
    this.store.marked.set(new Set());
    const left = this.rows();
    const next = left[Math.min(first, left.length - 1)];
    this.store.layer.set(next?.layer.id ?? null);
    this.focusChosen();
  }

  /** The row that held focus has gone, and the next Delete should still reach the stack. */
  private focusChosen(): void {
    setTimeout(() => {
      const row = globalThis.document.querySelector<HTMLElement>(`[data-testid="paint-layer-${this.store.layer()}"]`);
      row?.focus();
    });
  }

  protected toggle(id: string): void {
    const next = new Set(this.closed());
    if (!next.delete(id)) {
      next.add(id);
    }

    this.closed.set(next);
  }

  protected swatch(layer: StackLayer): string | null {
    const colour = layer.values['baseColor'];
    return layer.kind === 'fill' && layer.fill === 'Constant' && colour ? hexOf(colour) : null;
  }

  protected mark(layer: StackLayer): string {
    return layer.kind === 'fill' && layer.fill === 'Graph' ? 'M' : (KindMarks[layer.kind] ?? '');
  }

  protected maskMark(layer: StackLayer): string {
    return MaskMarks[layer.mask.source] ?? '';
  }

  protected maskColour(layer: StackLayer): string | null {
    return layer.mask.colour ? hexOf(layer.mask.colour) : null;
  }

  protected openMenu(event: MouseEvent, layer: StackLayer): void {
    event.preventDefault();
    event.stopPropagation();
    if (!this.store.selection().has(layer.id)) {
      this.choose(layer, false);
    }

    this.menu.set({ x: event.clientX, y: event.clientY, layer });
  }

  protected startRename(layer: StackLayer): void {
    this.menu.set(null);
    this.renaming.set(layer.id);
    setTimeout(() => {
      const input = globalThis.document.querySelector<HTMLInputElement>('[data-testid=layer-rename]');
      input?.focus();
      input?.select();
    });
  }

  protected blur(event: Event): void {
    (event.target as HTMLInputElement).blur();
  }

  protected rename(layer: StackLayer, event: Event): void {
    const name = (event.target as HTMLInputElement).value.trim();
    if (this.renaming() === layer.id && name.length > 0 && name !== layer.name) {
      void this.store.edit({ op: 'setLayer', layer: layer.id, name });
    }

    this.renaming.set(null);
  }

  protected hasContent(layer: StackLayer): boolean {
    return layer.children.length > 0 && layer.kind !== 'group';
  }

  protected hasMaskWork(layer: StackLayer): boolean {
    return layer.mask.source === 'Generator' || (layer.mask.effects?.length ?? 0) > 0;
  }

  /** What has been added to a layer's mask, listed under it. A generator is the mask itself, and has no switch. */
  protected workOf(layer: StackLayer): readonly LayerWork[] {
    const effects = (layer.mask.effects ?? []).map((effect, index) => ({
      key: `effect-${index}`,
      name: this.store.vocabulary()?.maskEffects.find(kind => kind.node === effect.node)?.name ?? effect.node,
      enabled: effect.enabled,
      effect: index
    }));
    return layer.mask.source === 'Generator' && layer.mask.generator
      ? [{ key: 'generator', name: layer.mask.generator, enabled: true, effect: null }, ...effects]
      : effects;
  }

  protected setWork(layer: StackLayer, effect: number | null, enabled: boolean): void {
    void this.store.setMask(layer, {
      ...layer.mask,
      effects: (layer.mask.effects ?? []).map((held, index) => (index === effect ? { ...held, enabled } : held))
    });
  }

  protected removeWork(layer: StackLayer, effect: number | null): void {
    void this.store.setMask(
      layer,
      effect === null
        ? { ...layer.mask, source: 'None' }
        : { ...layer.mask, effects: (layer.mask.effects ?? []).filter((_, index) => index !== effect) }
    );
  }

  /** Adds a step to the mask, which needs a mask to run on, so a layer with none gets a white one. */
  protected async addWork(layer: StackLayer, node: string): Promise<void> {
    this.menu.set(null);
    await this.store.setMask(layer, {
      ...layer.mask,
      source: layer.mask.source === 'None' ? 'Constant' : layer.mask.source,
      value: layer.mask.source === 'None' ? 1 : layer.mask.value,
      effects: [...(layer.mask.effects ?? []), { node, enabled: true }]
    });
  }

  /** The layer an effect goes in: an effect has none of its own, so the next goes beside it. */
  private ownerOf(layer: StackLayer): StackLayer {
    const row = this.store.layers().find(held => held.layer.id === layer.id);
    return row?.effect ? (this.store.layers().find(held => held.layer.id === row.parent)?.layer ?? layer) : layer;
  }

  protected async addContent(layer: StackLayer, kind: string, name: string, filterNode?: string): Promise<void> {
    this.menu.set(null);
    await this.store.addEffect(this.ownerOf(layer), kind, name, filterNode);
  }

  /** With the mask in front a step runs on the mask, and otherwise on the layer's content. */
  protected addStep(layer: StackLayer, node: string): Promise<void> {
    const name = this.store.vocabulary()?.filters.find(kind => kind.node === node)?.name ?? node.split('/').pop() ?? node;
    return this.store.masking() ? this.addWork(layer, node) : this.addContent(layer, 'filter', name, node);
  }

  protected async addValueMask(layer: StackLayer, value: number): Promise<void> {
    this.menu.set(null);
    await this.store.setMask(layer, { ...layer.mask, source: 'Constant', value });
    this.store.masking.set(false);
  }

  protected async addBitmapMask(layer: StackLayer): Promise<void> {
    this.menu.set(null);
    const asset = await this.project.pickPicture();
    if (asset) {
      await this.store.setMask(layer, { ...layer.mask, source: 'Texture', asset });
    }
  }

  /** Invert is on or off, so a second time takes it away rather than stacking another. */
  protected async invertMask(layer: StackLayer): Promise<void> {
    this.menu.set(null);
    const node = this.store.vocabulary()?.maskEffects.find(effect => effect.name === 'Invert')?.node;
    if (node) {
      await this.store.setMask(layer, { ...layer.mask, effects: toggledEffect(layer.mask.effects ?? [], node) });
    }
  }

  /** Puts the selected layers in a new folder, where the top one of them was. */
  protected async groupSelected(): Promise<void> {
    this.menu.set(null);
    const chosen = [...this.store.selection()];
    if (chosen.length === 0) {
      return;
    }

    await this.add('group', 'Folder');
    const folder = this.store.layer();
    if (folder) {
      const moves = dropManyCommands(this.dragRows(), chosen, folder, 'into');
      await this.store.edit(...moves);
    }
  }

  protected async addMask(layer: StackLayer, source: string): Promise<void> {
    this.menu.set(null);
    await this.store.setMask(layer, { ...layer.mask, source });
    this.store.masking.set(source === 'Paint');
    this.store.maskGrey.set(0);
  }

  /** A mask from one colour of the id map, which the Properties pane then offers to choose. */
  protected async addColourMask(layer: StackLayer): Promise<void> {
    this.menu.set(null);
    await this.store.setMask(layer, { ...layer.mask, source: 'Bake', map: 'id' });
    this.store.masking.set(false);
  }

  protected async remove(layer: StackLayer): Promise<void> {
    this.menu.set(null);
    if (this.store.selection().has(layer.id)) {
      await this.removeSelected();
    } else {
      await this.store.edit({ op: 'removeLayer', layer: layer.id });
    }
  }

  protected start(event: DragEvent, id: string): void {
    const selected = this.store.selection();
    this.dragged = selected.has(id) ? [...selected] : [id];
    event.dataTransfer?.setData('text/plain', id);
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = 'move';
    }
  }

  protected over(event: DragEvent, row: LayerRow): void {
    const zone = this.zoneOf(event, row);
    if (this.using.carried() === null && this.commands(row, zone).length === 0) {
      this.target.set(null);
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    this.target.set({ id: row.layer.id, zone });
  }

  /** A material can land anywhere in the list, and past the last row it lands on top, like a click. */
  protected overList(event: DragEvent): void {
    if (this.using.carried()) {
      event.preventDefault();
    }
  }

  protected async dropOnList(event: DragEvent): Promise<void> {
    const material = this.using.carried();
    if (material) {
      event.preventDefault();
      this.using.carried.set(null);
      await this.using.use(material);
    }
  }

  protected async drop(event: DragEvent, row: LayerRow): Promise<void> {
    event.preventDefault();
    event.stopPropagation();
    const zone = this.zoneOf(event, row);
    const material = this.using.carried();
    const commands = this.commands(row, zone);
    this.dragged = [];
    this.target.set(null);
    if (material) {
      this.using.carried.set(null);
      await this.using.use(material, { layer: row.layer.id, zone });
    } else if (commands.length > 0) {
      await this.store.edit(...commands);
    }
  }

  private dragRows() {
    return this.store.layers().map(held => ({
      id: held.layer.id,
      parent: held.parent,
      folder: held.layer.kind === 'group'
    }));
  }

  private commands(row: LayerRow, zone: DropZone) {
    return dropManyCommands(this.dragRows(), this.dragged, row.layer.id, zone);
  }

  /** The middle of a folder is into it, and everywhere else is above or below. */
  private zoneOf(event: DragEvent, row: LayerRow): DropZone {
    const box = (event.currentTarget as HTMLElement).getBoundingClientRect();
    const along = (event.clientY - box.top) / box.height;
    if (row.layer.kind === 'group' && along > 0.25 && along < 0.75) {
      return 'into';
    }

    return along < 0.5 ? 'above' : 'below';
  }
}
