import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, computed, inject, signal } from '@angular/core';
import { provideIcons } from '@ng-icons/core';
import {
  matAdsClick,
  matArrowDownward,
  matArrowUpward,
  matBrush,
  matChevronRight,
  matClose,
  matDeleteOutline,
  matDensitySmall,
  matExpandLess,
  matExpandMore,
  matFolder,
  matFolderZip,
  matFormatColorFill,
  matGradient,
  matHideSource,
  matTune,
  matViewInAr,
  matVisibility,
  matVisibilityOff
} from '@ng-icons/material-icons/baseline';
import type { StackFilterKind, StackLayer, StackMaskEffect, StackMaskLayer } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiContextMenuImports } from '@xui/context-menu';
import { XuiInputImports } from '@xui/input';
import { XuiMenuImports } from '@xui/menu';
import { XuiNonIdealStateImports } from '@xui/non-ideal-state';
import { XuiPopoverImports } from '@xui/popover';
import { XuiSelectImports } from '@xui/select';
import { XuiSliderImports } from '@xui/slider';
import { XuiSwitchImports } from '@xui/switch';
import { Activity } from '../core/activity';
import { Commands } from '../core/commands';
import { desktop } from '../core/desktop';
import { IconImports } from '../core/icons';
import { blendName as blendLabelOf, channelName, termName } from '../core/labels';
import { MenuKeys } from '../core/menu-keys';
import { diagnosticOf, messageOf } from '../core/messages';
import { percent } from '../core/numbers';
import { SystemStatus } from '../core/system-status';
import { ProjectStore } from '../library/project-store';
import { IsolationStore } from '../viewport/isolation-store';
import { type DropZone, dropManyCommands, nudgeCommands } from './layer-drag';
import { tagSwatch } from './layer-tags';
import { MaterialUse } from './material-use';
import { type LayerRow, PaintStore, hexOf, maskOn, toggledEffect } from './paint-store';

const RowHeight = 56;
/** A row in the compact list (#106): its thumbnails and its blending on one line. */
const CompactHeight = 36;
const EffectHeight = 32;
const CompactKey = 'patina.layers.compact';

/** Whether this machine last chose compact rows. A choice of the viewer's, so it stays in this browser. */
function savedCompact(): boolean {
  try {
    return globalThis.localStorage.getItem(CompactKey) === 'true';
  } catch {
    return false;
  }
}

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

const KindMarks: Readonly<Record<string, string>> = { paint: 'P', fill: 'F', filter: 'Fx', group: '', instance: 'In' };

/** What a mask's stack can have added to it, in the order its menu offers them. */
const MaskLayerSources: readonly { value: string; label: string }[] = [
  { value: 'Paint', label: 'Paint' },
  { value: 'Constant', label: 'Uniform' },
  { value: 'Generator', label: 'Generator' },
  { value: 'Bake', label: 'Mesh Map' },
  { value: 'Texture', label: 'Bitmap' },
  { value: 'Anchor', label: 'Anchor Point' }
];

const MaskMarks: Readonly<Record<string, string>> = {
  Constant: 'C',
  Texture: 'T',
  Anchor: 'A',
  Generator: 'G',
  Bake: 'B',
  Paint: 'P'
};

/**
 * The layer stack, top first. A row is what a layer is, its mask beside it, and how it blends. Dragging moves layers
 * and folders; clicking a layer or its mask chooses what the brush paints.
 */
@Component({
  selector: 'ptn-layer-stack',
  imports: [
    IconImports,
    MenuKeys,
    XuiButtonImports,
    XuiCalloutImports,
    XuiContextMenuImports,
    XuiInputImports,
    XuiMenuImports,
    XuiNonIdealStateImports,
    XuiPopoverImports,
    XuiSelectImports,
    XuiSliderImports,
    XuiSwitchImports
  ],
  viewProviders: [
    provideIcons({
      matAdsClick,
      matArrowDownward,
      matArrowUpward,
      matBrush,
      matChevronRight,
      matClose,
      matDeleteOutline,
      matDensitySmall,
      matExpandLess,
      matExpandMore,
      matFolder,
      matFolderZip,
      matFormatColorFill,
      matGradient,
      matHideSource,
      matTune,
      matViewInAr,
      matVisibility,
      matVisibilityOff
    })
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'flex h-full min-h-0 flex-col',
    'data-commands': 'stack',
    '(document:pointerup)': 'letGo()'
  },
  template: `
    <div class="chrome-bar flex shrink-0 items-center gap-0.5 px-1 py-0.5" role="toolbar" aria-label="Layers">
      <xui-select
        class="w-24 min-w-0 shrink-0 [&_button]:text-xs"
        style="--control-height-md: 1.375rem; --control-padding-md: 0.375rem"
        aria-label="Channel"
        data-testid="layers-channel"
        [filterable]="false"
        [itemText]="channelLabel"
        [items]="channels()"
        [matchTargetWidth]="false"
        [value]="store.channel()"
        (valueChange)="store.showChannel($event ?? store.channel())"
      />
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        class="shrink-0 px-1.5 text-xs"
        data-testid="layers-add"
        title="Add a layer, or a mask or an effect to the chosen layer"
        [xuiMenuTriggerFor]="addMenu"
        (menuOpened)="menuId.set(store.layer())"
      >
        Add…
      </button>
      <span class="flex-1"></span>
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        class="w-6 shrink-0 px-0"
        data-testid="layers-find"
        [title]="commands.tooltip('findLayer')"
        [attr.aria-label]="commands.label('findLayer')"
        [attr.aria-pressed]="store.finding()"
        [class.bg-selection]="store.finding()"
        (click)="store.finding.set(!store.finding())"
      >
        <ng-icon xui size="sm" name="matAdsClick" />
      </button>
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        class="w-6 shrink-0 px-0"
        data-testid="layers-compact"
        [title]="compact() ? 'Tall rows, with each layer’s blend above its opacity' : 'Compact rows, a line each'"
        aria-label="Compact rows"
        [attr.aria-pressed]="compact()"
        [class.bg-selection]="compact()"
        (click)="setCompact(!compact())"
      >
        <ng-icon xui size="sm" name="matDensitySmall" />
      </button>
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
    </div>

    @if (store.link() === 'reconnecting') {
      <xui-callout class="shrink-0 text-xs" color="error" compact data-testid="layers-offline">
        Lost touch with Patina’s engine. The stack comes back when it reconnects; an edit made before then is refused.
      </xui-callout>
    } @else if (workerMissing()) {
      <xui-callout class="shrink-0 text-xs" color="warning" compact data-testid="layers-no-worker">
        The GPU engine is not running. Edits are kept, and the paint shows again when it starts.
      </xui-callout>
    }

    @if (staleBake(); as stale) {
      <xui-callout class="shrink-0 text-xs" color="warning" compact data-testid="layers-stale-bake">
        <span class="flex items-center gap-2">
          <span class="min-w-0 flex-1">{{ stale }}</span>
          <button
            type="button"
            xuiButton
            size="sm"
            data-testid="layers-rebake"
            title="Bakes again with the project’s settings. Texture sets the change did not touch come from the cache."
            (click)="commands.run('quickBake')"
          >
            Rebake
          </button>
          <button type="button" xuiButton size="sm" (click)="project.askToBake()">Bake…</button>
        </span>
      </xui-callout>
    }

    @if (store.unbaked(); as maps) {
      <xui-callout class="shrink-0 text-xs" color="warning" compact data-testid="layers-need-bake">
        <span class="flex items-center gap-2">
          <span class="min-w-0 flex-1" [title]="mapNames(maps)"
            >Some masks need mesh maps, and show gray until they are baked.</span
          >
          <button type="button" xuiButton size="sm" data-testid="layers-bake" (click)="project.askToBake()">
            Bake…
          </button>
        </span>
      </xui-callout>
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
              <span class="readout shrink-0" [class.text-error]="item.severity === 'Error'">{{
                item.severity === 'Error' ? 'Error' : 'Warning'
              }}</span>
              <span class="truncate" [title]="item.message">{{ diagnosticOf(item.message).summary }}</span>
            </button>
          </li>
        }
      </ul>
    }

    @if (maskStackLayer(); as owner) {
      <div class="chrome-bar flex shrink-0 items-center gap-1 px-1 py-0.5" data-testid="mask-stack-bar">
        <button
          type="button"
          xuiButton
          size="sm"
          variant="ghost"
          class="shrink-0 px-1.5 text-xs"
          data-testid="mask-stack-back"
          title="Back to the layers"
          (click)="closeMaskStack()"
        >
          ‹ Layers
        </button>
        <span class="min-w-0 flex-1 truncate text-xs" data-testid="mask-stack-title">{{ owner.name }} · Mask</span>
        @if (chosenEffects().size > 0) {
          <button
            type="button"
            xuiButton
            size="sm"
            variant="ghost"
            class="shrink-0 px-1.5 text-xs"
            data-testid="mask-stack-copy"
            [title]="'Copy the chosen effects ' + commands.keyText('copy')"
            (click)="copyEffects(owner)"
          >
            Copy
          </button>
        }
        @if (store.effectClip(); as clip) {
          <button
            type="button"
            xuiButton
            size="sm"
            variant="ghost"
            class="shrink-0 px-1.5 text-xs"
            data-testid="mask-stack-paste"
            [title]="
              'Paste ' + clip.length + (clip.length === 1 ? ' effect ' : ' effects ') + commands.keyText('paste')
            "
            (click)="pasteEffects(owner)"
          >
            Paste
          </button>
        }
        <button
          type="button"
          xuiButton
          size="sm"
          variant="ghost"
          class="shrink-0 px-1.5 text-xs"
          data-testid="mask-stack-add"
          title="Add a layer to this mask"
          [xuiMenuTriggerFor]="maskLayerMenu"
        >
          Add…
        </button>
      </div>
      <div
        class="min-h-0 flex-1 overflow-auto"
        role="list"
        data-testid="mask-stack"
        [attr.aria-label]="'Mask of ' + owner.name"
      >
        @for (item of workOf(owner); track item.key) {
          @if (item.effect !== null) {
            @let effect = owner.mask.effects?.[item.effect];
            <div
              class="border-hairline flex h-9 items-center gap-1.5 border-b px-1"
              role="listitem"
              tabindex="0"
              data-testid="mask-stack-effect"
              [attr.aria-label]="item.name"
              [attr.aria-selected]="chosenEffects().has(item.effect)"
              [class.bg-selection]="chosenEffects().has(item.effect)"
              [class.opacity-50]="!item.enabled"
              (click)="chooseEffect($event, item.effect)"
              (keydown.enter)="chooseEffect($event, item.effect)"
            >
              <button
                type="button"
                class="grid size-5 shrink-0 place-items-center"
                [attr.aria-label]="(item.enabled ? 'Hide ' : 'Show ') + item.name"
                [title]="(item.enabled ? 'Hide ' : 'Show ') + item.name"
                [attr.aria-pressed]="item.enabled"
                (click)="$event.stopPropagation(); setWork(owner, item.effect, !item.enabled)"
              >
                <ng-icon xui size="sm" [name]="item.enabled ? 'matVisibility' : 'matVisibilityOff'" />
              </button>
              <span class="text-foreground-muted w-5 shrink-0 text-center text-xs">Fx</span>
              <span class="min-w-0 flex-1 truncate text-xs">{{ item.name }}</span>
              @if (effect) {
                <span
                  class="flex w-24 shrink-0 flex-col gap-0.5"
                  role="group"
                  tabindex="-1"
                  [attr.aria-label]="'Blending of ' + item.name"
                  (click)="$event.stopPropagation()"
                  (keydown)="$event.stopPropagation()"
                >
                  <xui-select
                    class="w-full min-w-0 [&_button]:text-xs"
                    style="--control-height-md: 1.25rem; --control-padding-md: 0.375rem"
                    data-testid="mask-stack-effect-blend"
                    [aria-label]="'Blend mode of ' + item.name"
                    [filterable]="false"
                    [itemText]="blendLabel"
                    [items]="blends()"
                    [matchTargetWidth]="false"
                    [value]="effect.blend"
                    (valueChange)="$event && setEffectBlend(owner, item.effect, { blend: $event })"
                  />
                  <span class="flex items-center gap-1">
                    <xui-slider
                      class="h-3 min-w-0 flex-1 pb-0"
                      labelStepSize="0"
                      data-testid="mask-stack-effect-opacity"
                      [attr.aria-label]="'Opacity of ' + item.name"
                      [max]="1"
                      [min]="0"
                      [stepSize]="0.01"
                      [value]="effect.opacity"
                      (valueChange)="setEffectBlend(owner, item.effect, { opacity: $event })"
                    />
                    <span class="readout w-8 shrink-0 text-end text-xs">{{ percent(effect.opacity) }}</span>
                  </span>
                </span>
              }
              <button
                type="button"
                class="text-foreground-muted grid size-5 shrink-0 place-items-center disabled:opacity-30"
                data-testid="mask-stack-effect-up"
                [attr.aria-label]="'Move ' + item.name + ' up'"
                [title]="'Move ' + item.name + ' up'"
                [disabled]="item.effect === 0"
                (click)="$event.stopPropagation(); moveWork(owner, item.effect, -1)"
              >
                <ng-icon xui size="sm" name="matArrowUpward" />
              </button>
              <button
                type="button"
                class="text-foreground-muted grid size-5 shrink-0 place-items-center disabled:opacity-30"
                data-testid="mask-stack-effect-down"
                [attr.aria-label]="'Move ' + item.name + ' down'"
                [title]="'Move ' + item.name + ' down'"
                [disabled]="item.effect === (owner.mask.effects?.length ?? 0) - 1"
                (click)="$event.stopPropagation(); moveWork(owner, item.effect, 1)"
              >
                <ng-icon xui size="sm" name="matArrowDownward" />
              </button>
              <button
                type="button"
                class="text-foreground-muted grid size-5 shrink-0 place-items-center"
                [attr.aria-label]="'Remove ' + item.name + ' from the mask'"
                [title]="'Remove ' + item.name + ' from the mask'"
                (click)="$event.stopPropagation(); removeWork(owner, item.effect)"
              >
                <ng-icon xui size="sm" name="matClose" />
              </button>
            </div>
          }
        }
        @for (entry of maskEntries(owner); track entry.index) {
          <div
            class="border-hairline flex h-9 items-center gap-1.5 border-b px-1"
            role="listitem"
            tabindex="0"
            [attr.aria-label]="entry.name"
            [attr.aria-current]="isMaskEntry(owner, entry.index)"
            [attr.data-testid]="'mask-layer-' + entry.index"
            [class.bg-selection]="isMaskEntry(owner, entry.index)"
            [class.opacity-50]="!entry.layer.enabled"
            (click)="store.chooseMaskLayer(owner, entry.index)"
            (keydown.enter)="store.chooseMaskLayer(owner, entry.index)"
          >
            <button
              type="button"
              class="grid size-5 shrink-0 place-items-center"
              data-testid="mask-layer-eye"
              [attr.aria-label]="(entry.layer.enabled ? 'Hide ' : 'Show ') + entry.name"
              [title]="(entry.layer.enabled ? 'Hide ' : 'Show ') + entry.name"
              [attr.aria-pressed]="entry.layer.enabled"
              (click)="
                $event.stopPropagation(); store.setMaskLayer(owner, entry.index, { enabled: !entry.layer.enabled })
              "
            >
              <ng-icon xui size="sm" [name]="entry.layer.enabled ? 'matVisibility' : 'matVisibilityOff'" />
            </button>
            <span
              class="border-hairline text-foreground-muted grid size-6 shrink-0 place-items-center rounded-sm border text-xs"
              [style.background]="entryColour(entry.layer)"
              >{{ sourceMark(entry.layer.source) }}</span
            >
            <span class="min-w-0 flex-1 truncate text-xs" [title]="entry.name">{{ entry.name }}</span>
            <span
              class="flex w-24 shrink-0 flex-col gap-0.5"
              role="group"
              tabindex="-1"
              [attr.aria-label]="'Blending of ' + entry.name"
              (click)="$event.stopPropagation()"
              (keydown)="$event.stopPropagation()"
            >
              <xui-select
                class="w-full min-w-0 [&_button]:text-xs"
                style="--control-height-md: 1.25rem; --control-padding-md: 0.375rem"
                data-testid="mask-layer-blend"
                [aria-label]="'Blend mode of ' + entry.name"
                [filterable]="false"
                [itemText]="blendLabel"
                [items]="blends()"
                [matchTargetWidth]="false"
                [value]="entry.layer.blend"
                (valueChange)="$event && store.setMaskLayer(owner, entry.index, { blend: $event })"
              />
              <span class="flex items-center gap-1">
                <xui-slider
                  class="h-3 min-w-0 flex-1 pb-0"
                  labelStepSize="0"
                  data-testid="mask-layer-opacity"
                  [attr.aria-label]="'Opacity of ' + entry.name"
                  [max]="1"
                  [min]="0"
                  [stepSize]="0.01"
                  [value]="entry.layer.opacity"
                  (valueChange)="setEntryOpacity(owner, entry.index, $event)"
                />
                <span class="readout w-8 shrink-0 text-end text-xs">{{ percent(entry.layer.opacity) }}</span>
              </span>
            </span>
            <span
              class="flex shrink-0 flex-col"
              (click)="$event.stopPropagation()"
              (keydown)="$event.stopPropagation()"
              role="group"
              tabindex="-1"
              [attr.aria-label]="'Order of ' + entry.name"
            >
              <button
                type="button"
                class="text-foreground-muted grid h-4 w-5 place-items-center text-xs"
                data-testid="mask-layer-up"
                [attr.aria-label]="'Move ' + entry.name + ' up'"
                [title]="'Move ' + entry.name + ' up'"
                [disabled]="entry.index === (owner.mask.layers?.length ?? 0) - 1"
                (click)="store.moveMaskLayer(owner, entry.index, 1)"
              >
                <ng-icon xui size="sm" name="matExpandLess" />
              </button>
              <button
                type="button"
                class="text-foreground-muted grid h-4 w-5 place-items-center text-xs"
                data-testid="mask-layer-down"
                [attr.aria-label]="'Move ' + entry.name + ' down'"
                [title]="'Move ' + entry.name + ' down'"
                [disabled]="entry.index === 0"
                (click)="store.moveMaskLayer(owner, entry.index, -1)"
              >
                <ng-icon xui size="sm" name="matExpandMore" />
              </button>
            </span>
            <button
              type="button"
              class="text-foreground-muted grid size-5 shrink-0 place-items-center"
              data-testid="mask-layer-remove"
              [attr.aria-label]="'Remove ' + entry.name"
              [title]="'Remove ' + entry.name"
              (click)="$event.stopPropagation(); store.removeMaskLayer(owner, entry.index)"
            >
              <ng-icon xui size="sm" name="matClose" />
            </button>
          </div>
        }
        <div
          class="border-hairline flex h-9 items-center gap-1.5 border-b px-1"
          role="listitem"
          tabindex="0"
          data-testid="mask-stack-base"
          [attr.aria-current]="store.masking() && store.layer() === owner.id && store.maskLayer() === null"
          [class.bg-selection]="store.masking() && store.layer() === owner.id && store.maskLayer() === null"
          (click)="store.chooseMask(owner)"
          (keydown.enter)="store.chooseMask(owner)"
        >
          <span class="size-5 shrink-0"></span>
          <span
            class="border-hairline text-foreground-muted grid size-6 shrink-0 place-items-center rounded-sm border text-xs"
            [style.background]="maskColour(owner)"
            >{{ sourceMark(owner.mask.source) }}</span
          >
          <span class="min-w-0 flex-1 truncate text-xs">Base · {{ termName(owner.mask.source) }}</span>
        </div>
      </div>
    } @else {
      <div
        class="min-h-0 flex-1 overflow-auto"
        role="tree"
        aria-label="Layers"
        aria-multiselectable="true"
        data-testid="paint-layers"
        ptnMenuKeys
        (aiming)="aimMenu($event)"
        [xuiContextMenuTriggerFor]="layerMenu"
        (pointerdown)="pressed($event)"
        (contextmenu)="aimMenu($event)"
        (contextMenuClosed)="restoreFocus()"
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
            [attr.aria-expanded]="
              row.layer.kind === 'group' && !row.layer.flattened ? !closed().has(row.layer.id) : null
            "
            [attr.data-testid]="'paint-layer-' + row.layer.id"
            [attr.data-layer]="row.layer.id"
            [attr.tabindex]="row.layer.id === (store.layer() ?? rows()[0]?.layer?.id) ? 0 : -1"
            [class.bg-selection]="store.selection().has(row.layer.id)"
            [class.opacity-50]="!row.layer.enabled"
            [style.height.px]="row.effect ? effectHeight : compact() ? compactHeight : rowHeight"
            [style.padding-inline-start.px]="row.depth * 14 + 4"
            (click)="click($event, row.layer)"
            (dragstart)="start($event, row.layer.id)"
            (dragover)="over($event, row)"
            (dragleave)="target.set(null)"
            (dragend)="dragged = []"
            (drop)="drop($event, row)"
            (keydown)="key($event, row)"
            (keydown.enter)="choose(row.layer, false)"
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
            @if (tagSwatch(row.layer.tag); as colour) {
              <span
                class="pointer-events-none absolute inset-y-0 start-0 w-[3px]"
                data-testid="layer-tag"
                [attr.data-tag]="row.layer.tag"
                [style.background]="colour"
              ></span>
            }
            <button
              type="button"
              class="grid size-5 shrink-0 place-items-center"
              data-testid="layer-eye"
              [attr.aria-label]="(row.layer.enabled ? 'Hide ' : 'Show ') + row.layer.name"
              [title]="(row.layer.enabled ? 'Hide ' : 'Show ') + row.layer.name"
              [attr.aria-pressed]="row.layer.enabled"
              (click)="$event.stopPropagation(); store.setEnabled(row.layer, !row.layer.enabled)"
            >
              <ng-icon xui size="sm" [name]="row.layer.enabled ? 'matVisibility' : 'matVisibilityOff'" />
            </button>

            @if (row.effect) {
              <span
                class="text-foreground-muted grid size-5 shrink-0 place-items-center text-xs"
                data-testid="layer-effect-mark"
              >
                {{ mark(row.layer) }}
              </span>
            } @else if (row.layer.flattened) {
              <span class="size-4 shrink-0"></span>
              <span
                class="text-foreground-muted grid size-8 shrink-0 place-items-center"
                role="img"
                aria-label="Flattened folder"
                data-testid="layer-flattened"
                title="Flattened folder: its layers are kept, and Unflatten brings them back"
              >
                <ng-icon xui size="lg" name="matFolderZip" />
              </span>
            } @else if (row.layer.kind === 'group') {
              <button
                type="button"
                class="text-foreground-muted grid size-4 shrink-0 place-items-center"
                data-testid="layer-toggle"
                [attr.aria-label]="(closed().has(row.layer.id) ? 'Open ' : 'Close ') + row.layer.name"
                [title]="(closed().has(row.layer.id) ? 'Open ' : 'Close ') + row.layer.name"
                (click)="$event.stopPropagation(); toggle(row.layer.id)"
              >
                <ng-icon
                  xui
                  size="sm"
                  name="matChevronRight"
                  class="transition-transform"
                  [class.rotate-90]="!closed().has(row.layer.id)"
                />
              </button>
              <span class="text-foreground-muted grid size-8 shrink-0 place-items-center" aria-hidden="true">
                <ng-icon xui size="lg" name="matFolder" />
              </span>
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
                  [class.hidden]="compact()"
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
                  [class.opacity-40]="!maskOn(row.layer)"
                  [attr.data-off]="maskOn(row.layer) ? null : ''"
                  [style.background]="maskColour(row.layer)"
                  [title]="maskTitle(row.layer)"
                  (click)="$event.stopPropagation(); maskClicked($event, row.layer)"
                  (dblclick)="$event.stopPropagation(); openMaskStack(row.layer)"
                >
                  <ng-icon
                    xui
                    [class.text-error]="!maskOn(row.layer)"
                    [name]="maskOn(row.layer) ? 'matGradient' : 'matHideSource'"
                  />
                </button>
                <span
                  class="h-[3px] w-8 rounded-full"
                  data-testid="layer-mask-mark"
                  [class.hidden]="compact()"
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
                  class="w-full"
                  xuiInput
                  size="sm"
                  autocomplete="off"
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
                placement="bottom-end"
                [ariaLabel]="'Meshes ' + row.layer.name + ' is on'"
                [role]="'dialog'"
                [xuiPopover]="meshList"
                (click)="$event.stopPropagation()"
                (opened)="openMeshes(row.layer)"
              >
                <ng-icon xui size="sm" name="matViewInAr" />
              </button>
            }

            <span
              class="flex shrink-0"
              [class.flex-col]="!row.effect && !compact()"
              [class.gap-1]="row.effect || compact()"
              [class.gap-2]="!row.effect && !compact()"
              [class.items-center]="row.effect || compact()"
              [class.w-20]="!row.effect && !compact()"
              [class.w-28]="compact() && !row.effect"
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
                [aria-label]="'Blend mode of ' + row.layer.name"
                [filterable]="false"
                [class.w-20]="row.effect"
                [class.w-16]="compact() && !row.effect"
                [class.w-full]="!row.effect && !compact()"
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
                <span
                  class="readout w-9 shrink-0 ps-1 text-end text-xs"
                  data-testid="paint-layer-opacity-readout"
                  [class.hidden]="compact() && !row.effect"
                  >{{ percent(opacityOf(row.layer)) }}</span
                >
              </span>
            </span>

            @if (row.effect) {
              <button
                type="button"
                class="text-foreground-muted grid size-5 shrink-0 place-items-center"
                data-testid="layer-effect-remove"
                [attr.aria-label]="'Remove ' + row.layer.name"
                [title]="'Remove ' + row.layer.name"
                (click)="$event.stopPropagation(); remove(row.layer)"
              >
                <ng-icon xui size="sm" name="matClose" />
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
                [title]="(item.enabled ? 'Hide ' : 'Show ') + item.name"
                [attr.aria-pressed]="item.enabled"
                [disabled]="item.effect === null"
                (click)="setWork(row.layer, item.effect, !item.enabled)"
              >
                <ng-icon xui size="sm" [name]="item.enabled ? 'matVisibility' : 'matVisibilityOff'" />
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
                [attr.aria-label]="'Remove ' + item.name + ' from the mask'"
                [title]="'Remove ' + item.name + ' from the mask'"
                (click)="removeWork(row.layer, item.effect)"
              >
                <ng-icon xui size="sm" name="matClose" />
              </button>
            </div>
          }
        } @empty {
          @if (store.state()) {
            <xui-non-ideal-state
              title="No layers yet"
              description="Layers lay paint, fills and filters one over another into the maps this document makes. Add one, or drag a material from the shelf."
              data-testid="layers-empty"
            >
              <button
                action
                type="button"
                xuiButton
                size="sm"
                [disabled]="!commands.enabled('addFillLayer')"
                (click)="commands.run('addFillLayer')"
              >
                {{ commands.label('addFillLayer') }}
              </button>
            </xui-non-ideal-state>
          } @else {
            <xui-non-ideal-state
              title="No document open"
              description="Open a texturing document or a layered material to see its layers here."
              data-testid="layers-empty"
            />
          }
        }
      </div>
    }

    <ng-template #maskLayerMenu>
      <xui-menu data-testid="mask-stack-add-menu" aria-label="Add to the mask">
        @if (maskStackLayer(); as owner) {
          @for (source of maskLayerSources; track source.value) {
            <button
              type="button"
              xuiMenuItem
              [attr.data-testid]="'mask-stack-add-' + source.value"
              (triggered)="store.addMaskLayer(owner, source.value)"
            >
              {{ source.label }}
            </button>
          }
        }
      </xui-menu>
    </ng-template>

    <ng-template #meshList>
      <div class="flex max-h-80 min-w-44 flex-col gap-1 overflow-auto p-1 text-sm" data-testid="layer-meshes">
        @if (meshLayer(); as layer) {
          @if (parts().length > 0) {
            <div class="flex flex-wrap gap-1 pb-1">
              <button
                type="button"
                xuiButton
                size="sm"
                data-testid="layer-meshes-pick"
                title="Each click on the model takes the mesh under it into this layer, or out of it"
                [attr.aria-pressed]="store.pickingPart() === layer.id"
                [variant]="store.pickingPart() === layer.id ? 'default' : 'ghost'"
                (click)="store.pickingPart.set(store.pickingPart() === layer.id ? null : layer.id)"
              >
                {{ store.pickingPart() === layer.id ? 'Click the model…' : 'Pick on Mesh' }}
              </button>
              <button
                type="button"
                xuiButton
                size="sm"
                variant="ghost"
                data-testid="layer-meshes-isolate"
                title="Shows only the meshes this layer is on in the viewport, or the whole model again"
                [attr.aria-pressed]="hidesOthers(layer)"
                (click)="hideOthers(layer)"
              >
                {{ hidesOthers(layer) ? 'Show All' : 'Hide Others' }}
              </button>
            </div>
          }
          @for (part of parts(); track part) {
            <div class="flex h-7 items-center gap-2">
              <span class="min-w-0 flex-1 truncate" [title]="part">{{ part }}</span>
              <xui-switch
                [aria-label]="part"
                [attr.data-testid]="'layer-mesh-' + part"
                [checked]="onPart(layer, part)"
                [disabled]="isLastPart(layer, part)"
                (checkedChange)="setPart(layer, part, $event)"
              />
            </div>
          } @empty {
            <span class="text-foreground-muted text-xs">This mesh has no named parts.</span>
          }
        }
      </div>
    </ng-template>

    <ng-template #addMenu>
      <xui-menu data-testid="layers-add-menu" aria-label="Add">
        <button type="button" xuiMenuItem data-testid="add-paint" (triggered)="add('paint', 'Paint')">
          Paint Layer
        </button>
        <button type="button" xuiMenuItem data-testid="add-fill" (triggered)="add('fill', 'Fill')">Fill Layer</button>
        <button type="button" xuiMenuItem data-testid="add-filter" (triggered)="add('filter', 'Filter', 'Levels')">
          Filter Layer
        </button>
        <button type="button" xuiMenuItem data-testid="add-group" (triggered)="add('group', 'Folder')">Folder</button>
        @if (menuLayer(); as layer) {
          <xui-menu-divider />
          @if (layer.mask.source === 'None') {
            <button type="button" xuiMenuItem data-testid="add-mask" [xuiMenuTriggerFor]="maskMenu">
              Mask on {{ layer.name }}
            </button>
          }
          <button type="button" xuiMenuItem data-testid="add-effect" [xuiMenuTriggerFor]="effectMenu">
            {{ store.masking() ? 'Effect on the Mask of ' : 'Effect in ' }}{{ layer.name }}
          </button>
        }
      </xui-menu>
    </ng-template>

    <ng-template #maskMenu>
      <xui-menu aria-label="Mask">
        @if (menuLayer(); as layer) {
          <button type="button" xuiMenuItem (triggered)="addValueMask(layer, 1)">White Mask</button>
          <button type="button" xuiMenuItem (triggered)="addValueMask(layer, 0)">Black Mask</button>
          <button type="button" xuiMenuItem (triggered)="addBitmapMask(layer)">Bitmap Mask…</button>
          <button type="button" xuiMenuItem (triggered)="addMask(layer, 'Paint')">Paint Mask</button>
          <button type="button" xuiMenuItem (triggered)="addColourMask(layer)">Color Selection Mask</button>
          <button type="button" xuiMenuItem (triggered)="addMask(layer, 'Generator')">Generator</button>
        }
      </xui-menu>
    </ng-template>

    <ng-template #effectMenu>
      <xui-menu aria-label="Effect">
        @if (menuLayer(); as layer) {
          @if (!store.masking()) {
            <button
              type="button"
              xuiMenuItem
              data-testid="layer-menu-effect-paint"
              (triggered)="addContent(layer, 'paint', 'Paint')"
            >
              Paint
            </button>
            <button
              type="button"
              xuiMenuItem
              data-testid="layer-menu-effect-fill"
              (triggered)="addContent(layer, 'fill', 'Fill')"
            >
              Fill
            </button>
          }
          @for (kind of quickSteps; track kind.node) {
            <button
              type="button"
              xuiMenuItem
              [attr.data-testid]="'layer-menu-effect-' + kind.name"
              (triggered)="addStep(layer, kind.node)"
            >
              {{ kind.name }}
            </button>
          }
          <button type="button" xuiMenuItem data-testid="layer-menu-filter" [xuiMenuTriggerFor]="filterMenu">
            Filter
          </button>
        }
      </xui-menu>
    </ng-template>

    <ng-template #exportMenu>
      @if (menuLayer(); as layer) {
        <xui-menu data-testid="layer-menu-exports" aria-label="Export alone">
          <button type="button" xuiMenuItem data-testid="layer-menu-export-alone" (triggered)="exportAlone(layer)">
            {{ commands.label('exportLayer') }}
          </button>
          @if (layer.mask.source !== 'None') {
            <button
              type="button"
              xuiMenuItem
              data-testid="layer-menu-export-mask"
              (triggered)="exportAlone(layer, true)"
            >
              {{ commands.label('exportMask') }}
            </button>
          }
        </xui-menu>
      }
    </ng-template>
    <ng-template #partsMenu>
      @if (menuLayer(); as layer) {
        <xui-menu data-testid="layer-menu-part-list" aria-label="Content and mask">
          @if (layer.kind !== 'group') {
            <button
              type="button"
              xuiMenuItem
              data-testid="layer-menu-copy-content"
              (triggered)="store.copyPart(layer, 'Content')"
            >
              Copy Content
            </button>
          }
          <button
            type="button"
            xuiMenuItem
            data-testid="layer-menu-paste-mask"
            [disabled]="!store.clip()"
            (triggered)="store.pasteInto('Mask')"
          >
            Paste into Mask
          </button>
          @if (layer.kind !== 'group') {
            <button
              type="button"
              xuiMenuItem
              data-testid="layer-menu-paste-content"
              [disabled]="!store.clip()"
              (triggered)="store.pasteInto('Content')"
            >
              Paste as Content
            </button>
          }
        </xui-menu>
      }
    </ng-template>
    <ng-template #tagMenu>
      <xui-menu data-testid="layer-menu-tags" aria-label="Color tag">
        @for (tag of store.vocabulary()?.tags ?? []; track tag.value) {
          <button
            type="button"
            xuiMenuItem
            [attr.data-testid]="'layer-menu-tag-' + tag.value"
            (triggered)="tagChosen(tag.value)"
          >
            <span
              class="border-hairline inline-block size-3 rounded-sm border"
              aria-hidden="true"
              [style.background]="tagSwatch(tag.value) ?? 'transparent'"
            ></span>
            {{ tag.label }}
          </button>
        }
      </xui-menu>
    </ng-template>

    <ng-template #filterMenu>
      <xui-menu
        data-testid="layer-menu-filters"
        [attr.aria-label]="store.masking() ? 'Filters for the mask' : 'Filters'"
      >
        @for (group of stepGroups(); track group.category) {
          <button
            type="button"
            xuiMenuItem
            [attr.data-testid]="'layer-menu-category-' + group.category"
            [xuiMenuTriggerFor]="categoryMenu"
            [xuiMenuTriggerData]="{ $implicit: group }"
          >
            {{ group.category }}
          </button>
        }
      </xui-menu>
    </ng-template>

    <ng-template #categoryMenu let-group>
      <xui-menu [attr.aria-label]="group.category">
        @for (kind of group.kinds; track kind.node) {
          <button
            type="button"
            xuiMenuItem
            [attr.data-testid]="'layer-menu-step-' + kind.name"
            [title]="kind.summary"
            (triggered)="addChosenStep(kind.node)"
          >
            {{ kind.name }}
          </button>
        }
      </xui-menu>
    </ng-template>

    <ng-template #layerMenu>
      <xui-menu data-testid="layer-menu" [attr.aria-label]="menuLabel()">
        @if (menuPart() === 'mask' && menuLayer(); as layer) {
          <button type="button" xuiMenuItem data-testid="mask-menu-switch" (triggered)="store.switchMask(layer)">
            {{ maskOn(layer) ? 'Switch Mask Off' : 'Switch Mask On' }}
          </button>
          <button type="button" xuiMenuItem data-testid="mask-menu-open" (triggered)="openMaskStack(layer)">
            {{ commands.label('openMaskStack') }}
          </button>
          <button type="button" xuiMenuItem data-testid="mask-menu-invert" (triggered)="invertMask(layer)">
            Invert Mask
          </button>
          <xui-menu-divider />
          <button type="button" xuiMenuItem data-testid="mask-menu-copy" (triggered)="store.copyPart(layer, 'Mask')">
            Copy Mask
          </button>
          <button
            type="button"
            xuiMenuItem
            data-testid="mask-menu-save"
            [disabled]="!project.current()"
            (triggered)="saveToShelf(layer, true)"
          >
            Save as Smart Mask
          </button>
          <button
            type="button"
            xuiMenuItem
            data-testid="mask-menu-paste"
            [disabled]="!store.clip()"
            (triggered)="store.pasteInto('Mask')"
          >
            Paste into Mask
          </button>
          <xui-menu-divider />
          <button
            type="button"
            xuiMenuItem
            color="error"
            data-testid="mask-menu-remove"
            (triggered)="addMask(layer, 'None')"
          >
            Remove Mask
          </button>
        } @else if (menuPart() === 'blend' && menuLayer(); as layer) {
          <button
            type="button"
            xuiMenuItem
            data-testid="blend-menu-apply"
            [disabled]="store.selection().size < 2"
            (triggered)="store.blendEverywhere(layer)"
          >
            Apply {{ blendLabel(layer.blend) }} to the Chosen Layers
          </button>
        } @else {
          @if (menuLayer(); as layer) {
            <button type="button" xuiMenuItem data-testid="layer-menu-rename" (triggered)="startRename(layer)">
              Rename
            </button>
            <xui-menu-divider />
            @if (layer.mask.source === 'None') {
              <button
                type="button"
                xuiMenuItem
                data-testid="layer-menu-mask-white"
                (triggered)="addValueMask(layer, 1)"
              >
                Add White Mask
              </button>
              <button
                type="button"
                xuiMenuItem
                data-testid="layer-menu-mask-black"
                (triggered)="addValueMask(layer, 0)"
              >
                Add Black Mask
              </button>
              <button type="button" xuiMenuItem data-testid="layer-menu-mask-bitmap" (triggered)="addBitmapMask(layer)">
                Add Bitmap Mask…
              </button>
              <button
                type="button"
                xuiMenuItem
                data-testid="layer-menu-mask-paint"
                (triggered)="addMask(layer, 'Paint')"
              >
                Add Paint Mask
              </button>
              <button type="button" xuiMenuItem data-testid="layer-menu-mask-colour" (triggered)="addColourMask(layer)">
                Add Color Selection Mask
              </button>
              <button
                type="button"
                xuiMenuItem
                data-testid="layer-menu-mask-generator"
                (triggered)="addMask(layer, 'Generator')"
              >
                Add Generator
              </button>
            } @else {
              <button type="button" xuiMenuItem data-testid="layer-menu-mask-open" (triggered)="openMaskStack(layer)">
                {{ commands.label('openMaskStack') }}
              </button>
              <button
                type="button"
                xuiMenuItem
                data-testid="layer-menu-mask-switch"
                (triggered)="store.switchMask(layer)"
              >
                {{ maskOn(layer) ? 'Switch Mask Off' : 'Switch Mask On' }}
              </button>
              <button type="button" xuiMenuItem data-testid="layer-menu-mask-invert" (triggered)="invertMask(layer)">
                Invert Mask
              </button>
              <button
                type="button"
                xuiMenuItem
                data-testid="layer-menu-mask-copy"
                (triggered)="store.copyPart(layer, 'Mask')"
              >
                Copy Mask
              </button>
              <button
                type="button"
                xuiMenuItem
                data-testid="layer-menu-mask-remove"
                (triggered)="addMask(layer, 'None')"
              >
                Remove Mask
              </button>
            }
            <xui-menu-divider />
            <button type="button" xuiMenuItem data-testid="layer-menu-effect" [xuiMenuTriggerFor]="effectMenu">
              {{ store.masking() ? 'Add Effect to Mask' : 'Add Effect' }}
            </button>
            <xui-menu-divider />
            @if (layer.flattened) {
              <button
                type="button"
                xuiMenuItem
                data-testid="layer-menu-unflatten"
                (triggered)="store.unflatten(layer.id)"
              >
                Unflatten Folder
              </button>
            } @else if (layer.kind === 'group') {
              <button type="button" xuiMenuItem data-testid="layer-menu-flatten" (triggered)="store.flatten(layer.id)">
                Flatten Folder
              </button>
            }
            <button type="button" xuiMenuItem data-testid="layer-menu-export" [xuiMenuTriggerFor]="exportMenu">
              Export Alone
            </button>
            <button type="button" xuiMenuItem data-testid="layer-menu-tag" [xuiMenuTriggerFor]="tagMenu">
              Color Tag
            </button>
            <button type="button" xuiMenuItem data-testid="layer-menu-group" (triggered)="groupSelected()">
              Group Layers <span menuItemLabel>{{ commands.keyText('groupLayers') }}</span>
            </button>
            <button
              type="button"
              xuiMenuItem
              data-testid="layer-menu-instance"
              (triggered)="store.addInstance(layer.id)"
            >
              {{ commands.label('createInstance') }}
            </button>
            <button type="button" xuiMenuItem data-testid="layer-menu-duplicate" (triggered)="store.duplicateLayers()">
              Duplicate Layers <span menuItemLabel>{{ commands.keyText('duplicate') }}</span>
            </button>
            <button
              type="button"
              xuiMenuItem
              data-testid="layer-menu-save-material"
              [disabled]="!project.current()"
              (triggered)="saveToShelf(layer, false)"
            >
              Save as Smart Material
            </button>
            <button type="button" xuiMenuItem data-testid="layer-menu-copy" (triggered)="store.copyLayers()">
              Copy <span menuItemLabel>{{ commands.keyText('copy') }}</span>
            </button>
            <button type="button" xuiMenuItem data-testid="layer-menu-cut" (triggered)="store.cutLayers()">
              Cut <span menuItemLabel>{{ commands.keyText('cut') }}</span>
            </button>
            <button type="button" xuiMenuItem data-testid="layer-menu-parts" [xuiMenuTriggerFor]="partsMenu">
              Content and Mask
            </button>
          } @else {
            <button type="button" xuiMenuItem data-testid="layer-menu-new-paint" (triggered)="add('paint', 'Paint')">
              New Paint Layer
            </button>
            <button type="button" xuiMenuItem data-testid="layer-menu-new-fill" (triggered)="add('fill', 'Fill')">
              New Fill Layer
            </button>
            <button type="button" xuiMenuItem data-testid="layer-menu-new-group" (triggered)="add('group', 'Folder')">
              New Folder
            </button>
          }
          <button
            type="button"
            xuiMenuItem
            data-testid="layer-menu-paste"
            [disabled]="!store.clip()"
            (triggered)="store.pasteLayers()"
          >
            Paste <span menuItemLabel>{{ commands.keyText('paste') }}</span>
          </button>
          <button
            type="button"
            xuiMenuItem
            data-testid="layer-menu-paste-instance"
            [disabled]="!store.clip()"
            (triggered)="store.pasteAsInstances()"
          >
            {{ commands.label('pasteAsInstance') }}
          </button>
          <button
            type="button"
            xuiMenuItem
            data-testid="layer-menu-flatten-stack"
            [disabled]="(store.state()?.layers?.length ?? 0) === 0"
            (triggered)="store.flatten()"
          >
            Flatten All Layers
          </button>
          @if (menuLayer(); as layer) {
            <xui-menu-divider />
            <button type="button" xuiMenuItem color="error" data-testid="paint-remove" (triggered)="remove(layer)">
              Delete Layers <span menuItemLabel>{{ commands.keyText('delete') }}</span>
            </button>
          }
        }
      </xui-menu>
    </ng-template>
  `
})
export class LayerStack {
  protected readonly store = inject(PaintStore);
  private readonly status = inject(SystemStatus);
  /** The Server answers but its GPU engine does not, so nothing new can be drawn. */
  protected readonly workerMissing = computed(() => {
    const state = this.status.state();
    return state.kind === 'online' && !state.system.worker.connected;
  });
  protected readonly project = inject(ProjectStore);
  private readonly isolation = inject(IsolationStore);

  /** Why this texture set's mesh maps need baking again, or null: its own bake's word first, then the project's. */
  protected readonly staleBake = computed(() => {
    const own = this.store.state()?.bakeStale;
    if (own === 'uvSet') {
      return 'This texture set’s UV set changed after its last bake.';
    }

    return own === 'mesh' || this.project.bakeOutOfDate() ? 'The mesh changed after the last bake.' : null;
  });

  protected readonly percent = percent;

  /**
   * The layers chosen with this one, or this layer's mask, onto the project's shelf as a smart material or a smart
   * mask, named after the layer (#135). A folder brings its layers.
   */
  protected saveToShelf(layer: StackLayer, mask: boolean): void {
    const project = this.project.current();
    if (!project) {
      return;
    }

    const chosen = this.store.selection();
    const layers = mask || !chosen.has(layer.id) ? [layer.id] : [...chosen];
    void this.store.saveToShelf(layers, mask, mask ? `${layer.name} Mask` : layer.name, project.path);
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

  protected async openMeshes(layer: StackLayer): Promise<void> {
    this.meshLayer.set(layer);
    this.parts.set(await this.store.meshParts());
    // The list opened before its meshes arrived, with nothing to focus, so the first goes to the hand now.
    setTimeout(() => {
      globalThis.document.querySelector<HTMLElement>('[data-testid=layer-meshes] input:not(:disabled)')?.focus();
    });
  }

  /** Whether the viewport shows only the meshes this layer is on, which is how geometry left out is hidden. */
  protected hidesOthers(layer: StackLayer): boolean {
    const shown = this.isolation.isolation().parts;
    const kept = this.parts().filter(name => this.onPart(layer, name));
    return shown.length > 0 && shown.length === kept.length && kept.every(name => shown.includes(name));
  }

  protected hideOthers(layer: StackLayer): void {
    void (this.hidesOthers(layer)
      ? this.isolation.showAll()
      : this.isolation.showParts(this.parts().filter(name => this.onPart(layer, name))));
  }

  protected onPart(layer: StackLayer, part: string): boolean {
    return layer.mask.meshParts?.includes(part) ?? true;
  }

  /** ⚠ No meshes at all means the whole model to the Server, so the last one stays: the eye hides a layer. */
  protected isLastPart(layer: StackLayer, part: string): boolean {
    return this.onPart(layer, part) && this.parts().filter(name => this.onPart(layer, name)).length === 1;
  }

  protected setPart(layer: StackLayer, part: string, on: boolean): void {
    const kept = this.parts().filter(name => (name === part ? on : this.onPart(layer, name)));
    void this.store.keepToMeshes(layer, kept.length === this.parts().length ? [] : kept);
    // The list shows what was asked for at once, ahead of the Server's answer, so a quick second click builds on it.
    this.meshLayer.set({ ...layer, mask: { ...layer.mask, meshParts: kept } });
  }

  protected readonly rowHeight = RowHeight;
  protected readonly compactHeight = CompactHeight;
  /** Rows a line each, so that more of a long stack shows at once (#106). */
  protected readonly compact = signal(savedCompact());

  protected setCompact(compact: boolean): void {
    this.compact.set(compact);
    try {
      globalThis.localStorage.setItem(CompactKey, String(compact));
    } catch {
      // Only this machine's choice is lost.
    }
  }
  protected readonly maskLayerSources = MaskLayerSources;
  /** The layer whose mask the list shows as a stack of its own, in place of the layers (#109), or null. */
  protected readonly maskStack = signal<string | null>(null);
  protected readonly maskStackLayer = computed(() => {
    const id = this.maskStack();
    return this.store.layers().find(row => row.layer.id === id)?.layer ?? null;
  });
  private entryOpacity: { layer: StackLayer; index: number; opacity: number } | null = null;
  private sendingEntryOpacity = false;

  /** Shows a layer's mask as a stack: its effects, its entries top first, and its base. */
  /** A click on a mask puts it in front of the brush; with Shift it switches the mask off or on (#109). */
  protected maskClicked(event: MouseEvent, layer: StackLayer): void {
    if (event.shiftKey) {
      void this.store.switchMask(layer);
    } else {
      this.choose(layer, true);
    }
  }

  protected maskTitle(layer: StackLayer): string {
    const kind = `${termName(layer.mask.source)} mask`;
    return maskOn(layer)
      ? `${kind}. Double-click to open it as a stack, Shift-click to switch it off`
      : `${kind}, switched off. Shift-click to switch it on`;
  }

  protected openMaskStack(layer: StackLayer): void {
    this.maskStack.set(layer.id);
    this.chosenEffects.set(new Set());
    this.store.chooseMask(layer);
  }

  /** Back to the layers, with the layer whose mask it was still chosen. */
  protected closeMaskStack(): void {
    this.maskStack.set(null);
    this.chosenEffects.set(new Set());
  }

  /** The effects of the open mask stack that Copy, Cut and Delete take, by their place in its list (#109). */
  protected readonly chosenEffects = signal<ReadonlySet<number>>(new Set());

  /** One effect alone, or with Shift or the platform key one more or one less, as layers are chosen. */
  protected chooseEffect(event: Event, effect: number): void {
    const adding = event instanceof MouseEvent && (event.shiftKey || event.metaKey || event.ctrlKey);
    this.chosenEffects.update(held => {
      if (!adding) {
        return new Set([effect]);
      }

      const next = new Set(held);
      if (!next.delete(effect)) {
        next.add(effect);
      }

      return next;
    });
  }

  /** The chosen effects of a mask, in the order they run. */
  private chosenOf(owner: StackLayer): readonly StackMaskEffect[] {
    const chosen = this.chosenEffects();
    return (owner.mask.effects ?? []).filter((_, index) => chosen.has(index));
  }

  /** Keeps the chosen effects, settings, inputs, blend and all, to paste into this mask or another (#109). */
  protected copyEffects(owner: StackLayer): void {
    const effects = this.chosenOf(owner);
    if (effects.length > 0) {
      this.store.effectClip.set(effects.map(effect => structuredClone(effect)));
    }
  }

  protected cutEffects(owner: StackLayer): Promise<void> {
    this.copyEffects(owner);
    return this.removeEffects(owner);
  }

  /** Takes the chosen effects out of the mask, as one step. */
  protected removeEffects(owner: StackLayer): Promise<void> {
    const chosen = this.chosenEffects();
    this.chosenEffects.set(new Set());
    return this.store.setMask(owner, {
      ...owner.mask,
      effects: (owner.mask.effects ?? []).filter((_, index) => !chosen.has(index))
    });
  }

  /** Puts the copied effects after the last chosen one, or last, as one step, and chooses them. */
  protected async pasteEffects(owner: StackLayer): Promise<void> {
    const clip = this.store.effectClip();
    if (!clip) {
      return;
    }

    const effects = [...(owner.mask.effects ?? [])];
    const at = this.chosenEffects().size > 0 ? Math.max(...this.chosenEffects()) + 1 : effects.length;
    effects.splice(at, 0, ...clip.map(effect => structuredClone(effect)));
    this.chosenEffects.set(new Set(clip.map((_, offset) => at + offset)));
    // A mask with nothing to run on gets a white one, as adding an effect gives it.
    await this.store.setMask(owner, {
      ...owner.mask,
      source: owner.mask.source === 'None' ? 'Constant' : owner.mask.source,
      value: owner.mask.source === 'None' ? 1 : owner.mask.value,
      effects
    });
  }

  /** An effect's blend or opacity. */
  protected setEffectBlend(owner: StackLayer, effect: number, change: Partial<StackMaskEffect>): void {
    void this.store.setMask(owner, {
      ...owner.mask,
      effects: (owner.mask.effects ?? []).map((held, index) => (index === effect ? { ...held, ...change } : held))
    });
  }

  /** A mask's entries top first, as a list of layers reads, each with its place counted from the bottom. */
  protected maskEntries(layer: StackLayer): readonly { index: number; name: string; layer: StackMaskLayer }[] {
    return (layer.mask.layers ?? [])
      .map((entry, index) => ({ index, name: this.entryName(entry), layer: entry }))
      .reverse();
  }

  private entryName(entry: StackMaskLayer): string {
    const what =
      entry.source === 'Generator' && entry.generator
        ? termName(entry.generator)
        : entry.source === 'Bake' && entry.map
          ? termName(entry.map)
          : entry.source === 'Constant'
            ? percent(entry.value)
            : '';
    return what ? `${termName(entry.source)} · ${what}` : termName(entry.source);
  }

  protected isMaskEntry(layer: StackLayer, index: number): boolean {
    return this.store.masking() && this.store.layer() === layer.id && this.store.maskLayer() === index;
  }

  protected sourceMark(source: string): string {
    return MaskMarks[source] ?? '';
  }

  protected entryColour(entry: StackMaskLayer): string | null {
    return entry.source === 'Constant' ? hexOf([entry.value, entry.value, entry.value]) : null;
  }

  /** One request out at a time, the newest value next, as a layer's own opacity is sent. */
  protected setEntryOpacity(layer: StackLayer, index: number, opacity: number): void {
    this.entryOpacity = { layer, index, opacity };
    void this.sendEntryOpacity();
  }

  private async sendEntryOpacity(): Promise<void> {
    if (this.sendingEntryOpacity) {
      return;
    }

    this.sendingEntryOpacity = true;
    while (this.entryOpacity) {
      const { layer, index, opacity } = this.entryOpacity;
      this.entryOpacity = null;
      await this.store.setMaskLayer(layer, index, { opacity });
    }

    this.sendingEntryOpacity = false;
  }
  protected readonly quickSteps = QuickSteps;

  /** The filters a menu offers, under the headings the node library uses. */
  protected readonly stepGroups = computed(() => {
    const kinds =
      (this.store.masking() ? this.store.vocabulary()?.maskEffects : this.store.vocabulary()?.filters) ?? [];
    const groups = new Map<string, StackFilterKind[]>();
    for (const kind of kinds) {
      groups.set(kind.category, [...(groups.get(kind.category) ?? []), kind]);
    }

    return [...groups].map(([category, held]) => ({ category, kinds: held }));
  });
  protected readonly effectHeight = EffectHeight;
  protected readonly closed = signal<ReadonlySet<string>>(new Set());
  /** The layer a menu is about, or null for the stack itself. */
  protected readonly menuId = signal<string | null>(null);
  /** Which part of the row a menu is about: the layer, its mask, or how it blends (#106). */
  protected readonly menuPart = signal<'layer' | 'mask' | 'blend'>('layer');
  protected readonly menuLabel = computed(() =>
    this.menuLayer() === null ? 'Layers' : { layer: 'Layer', mask: 'Mask', blend: 'Blending' }[this.menuPart()]
  );
  protected readonly maskOn = maskOn;
  protected readonly menuLayer = computed(() => {
    const id = this.menuId();
    return this.store.layers().find(row => row.layer.id === id)?.layer ?? null;
  });
  protected readonly commands = inject(Commands);
  private readonly activity = inject(Activity);
  protected readonly tagSwatch = tagSwatch;

  /** Marks every chosen layer with a colour, or takes the mark away, as one step (#106). */
  protected tagChosen(tag: string): Promise<void> {
    const chosen = [...this.store.selection()];
    const layers = chosen.length > 0 ? chosen : [this.store.layer()].filter(id => id !== null);
    return this.store.edit(...layers.map(layer => ({ op: 'setLayer' as const, layer, tag })));
  }

  /**
   * Writes a layer's maps on their own, or its mask alone as one grey map, into a folder chosen now (#107, #153). A
   * flattened folder writes the picture it was flattened to.
   */
  protected async exportAlone(layer: StackLayer, mask = false): Promise<void> {
    const folder = await desktop()?.openFolder();
    if (!folder) {
      return;
    }

    try {
      const maps = await this.store.exportLayer(layer.id, folder, mask);
      if (maps) {
        const count = maps.length;
        const what = mask ? `the mask of ${layer.name}` : layer.name;
        this.activity.report({
          tone: 'done',
          source: 'Layers',
          text: `Exported ${what} on its own: ${count} ${count === 1 ? 'map' : 'maps'} in ${folder}.`
        });
      }
    } catch (failure) {
      this.activity.report({ tone: 'error', source: 'Layers', text: messageOf(failure) });
    }
  }

  /** The maps a mask waits for, as the app calls them, from the values the Worker lists them by (#198). */
  protected mapNames(maps: string): string {
    return maps
      .split(', ')
      .map(map => termName(map))
      .join(', ');
  }
  protected readonly undoLabel = computed(() => {
    const label = this.store.state()?.history.undoLabel;
    return label ? `Undo ${label}` : 'Undo';
  });
  protected readonly redoLabel = computed(() => {
    const label = this.store.state()?.history.redoLabel;
    return label ? `Redo ${label}` : 'Redo';
  });
  protected readonly channels = computed(() => (this.store.state()?.channels ?? []).map(channel => channel.usage));
  protected readonly channelLabel = channelName;
  /** The layer whose list of meshes is open. */
  protected readonly meshLayer = signal<StackLayer | null>(null);
  protected readonly parts = signal<readonly string[]>([]);
  protected readonly blends = computed(() => this.store.vocabulary()?.blends.map(term => term.value) ?? []);
  protected readonly termName = termName;
  protected readonly diagnosticOf = diagnosticOf;
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
    for (let at = rows.find(row => row.layer.id === id); at?.parent;) {
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

  /**
   * What the stack answers: the Edit commands while focus is in it, or nowhere, and the Layer menu's. Delete takes a
   * mask that is in front, or else the layers; Backspace, which is what a Mac's delete key sends, does the same.
   */
  constructor() {
    const destroyRef = inject(DestroyRef);
    const handle = (id: string, enabled: () => boolean, run: () => unknown) =>
      this.commands.handle(id, { enabled, run }, destroyRef);
    const open = () => this.store.state() !== null;
    const here = () => open() && (this.commands.focus().area === 'stack' || this.commands.focus().area === null);
    const selected = () => here() && this.store.selection().size > 0;
    // The mask stack's own effects come first while it is open and some are chosen (#109).
    const stacked = () => (here() ? this.maskStackLayer() : null);
    const effectsChosen = () => stacked() !== null && this.chosenEffects().size > 0;
    handle('copy', effectsChosen, () => {
      const owner = stacked();
      if (owner) {
        this.copyEffects(owner);
      }
    });
    handle('cut', effectsChosen, () => {
      const owner = stacked();
      return owner ? this.cutEffects(owner) : undefined;
    });
    handle(
      'paste',
      () => stacked() !== null && this.store.effectClip() !== null,
      () => {
        const owner = stacked();
        return owner ? this.pasteEffects(owner) : undefined;
      }
    );
    handle('delete', effectsChosen, () => {
      const owner = stacked();
      return owner ? this.removeEffects(owner) : undefined;
    });
    handle(
      'delete',
      () => here() && (this.store.selection().size > 0 || (this.store.masking() && this.store.chosen() !== null)),
      () => this.removeInFront()
    );
    handle(
      'selectAll',
      () => here() && this.rows().length > 0,
      () => this.store.marked.set(new Set(this.rows().map(row => row.layer.id)))
    );
    handle('copy', selected, () => this.store.copyLayers());
    handle('cut', selected, () => this.store.cutLayers());
    handle(
      'paste',
      () => here() && this.store.clip() !== null,
      () => this.store.pasteLayers()
    );
    handle('duplicate', selected, () => this.store.duplicateLayers());
    handle(
      'groupLayers',
      () => open() && this.store.selection().size > 0,
      () => this.groupSelected()
    );
    handle(
      'moveLayerUp',
      () => open() && this.nudges(1).length > 0,
      () => this.nudge(1)
    );
    handle(
      'moveLayerDown',
      () => open() && this.nudges(-1).length > 0,
      () => this.nudge(-1)
    );
    handle(
      'renameLayer',
      () => open() && this.store.chosen() !== null,
      () => {
        const layer = this.store.chosen();
        if (layer) {
          this.startRename(layer);
        }
      }
    );
    handle('addPaintLayer', open, () => this.add('paint', 'Paint'));
    handle('addFillLayer', open, () => this.add('fill', 'Fill'));
    handle('addFilterLayer', open, () => this.add('filter', 'Filter', 'Levels'));
    handle('addLayerFolder', open, () => this.add('group', 'Folder'));
    const chosen = () => (open() ? this.store.chosen() : null);
    handle(
      'flattenFolder',
      () => chosen()?.kind === 'group' && !chosen()?.flattened,
      () => {
        const layer = chosen();
        return layer ? this.store.flatten(layer.id) : undefined;
      }
    );
    handle(
      'unflattenFolder',
      () => chosen()?.flattened === true,
      () => {
        const layer = chosen();
        return layer ? this.store.unflatten(layer.id) : undefined;
      }
    );
    handle(
      'exportLayer',
      () => chosen() !== null,
      () => {
        const layer = chosen();
        return layer ? this.exportAlone(layer) : undefined;
      }
    );
    handle(
      'exportMask',
      () => (chosen()?.mask.source ?? 'None') !== 'None',
      () => {
        const layer = chosen();
        return layer ? this.exportAlone(layer, true) : undefined;
      }
    );
    handle(
      'flattenStack',
      () => open() && (this.store.state()?.layers.length ?? 0) > 0,
      () => this.store.flatten()
    );
    handle(
      'createInstance',
      () => chosen() !== null,
      () => {
        const layer = chosen();
        return layer ? this.store.addInstance(layer.id) : undefined;
      }
    );
    handle(
      'pasteAsInstance',
      () => here() && this.store.clip() !== null,
      () => this.store.pasteAsInstances()
    );
    handle(
      'openMaskStack',
      () => (chosen()?.mask.source ?? 'None') !== 'None',
      () => {
        const layer = chosen();
        if (layer) {
          this.openMaskStack(layer);
        }
      }
    );
  }

  /** The Delete key takes what is in front: a mask that is, or else the layers. Anything that says layers takes those. */
  private async removeInFront(): Promise<void> {
    const layer = this.store.chosen();
    const entry = this.store.maskLayer();
    if (this.store.masking() && layer && entry !== null) {
      await this.store.removeMaskLayer(layer, entry);
    } else if (this.store.masking() && layer) {
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

  /** A closed menu gives focus back to the chosen row, unless an item sent it somewhere on purpose. */
  protected restoreFocus(): void {
    // The menu says it closed while focus is still on its item, so this looks once the menu has gone.
    this.focusChosen();
  }

  /** Focus goes back to the chosen row: after a delete, whose row has gone, and after a menu closes. */
  protected focusChosen(): void {
    setTimeout(() => {
      const active = globalThis.document.activeElement;
      if (this.renaming() !== null) {
        return;
      }

      if (active && active !== globalThis.document.body && !this.host.nativeElement.contains(active)) {
        return; // a menu item moved focus somewhere on purpose, such as a rename field or a dialog
      }

      const row = this.host.nativeElement.querySelector<HTMLElement>(`[data-layer="${this.store.layer()}"]`);
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

  protected maskColour(layer: StackLayer): string | null {
    const [first] = layer.mask.colours ?? [];
    return first ? hexOf(first) : null;
  }

  /**
   * A menu is about the row it opens on, which it chooses unless it is already in what is selected. The press before
   * a right-click says which, as does the keyboard's own event from the row: the menu draws as it opens, and an item
   * that changed under focus would take the focus with it.
   */
  /**
   * A right press aims the menu before it opens. ⚠ A method, not `button === 2 && …` in the template: a listener that
   * returns false makes Angular prevent the default, and a pointerdown with its default prevented never starts a drag.
   */
  protected pressed(event: PointerEvent): void {
    if (event.button === 2) {
      this.aimMenu(event);
    }
  }

  protected aimMenu(event: { target: EventTarget | null }): void {
    const target = event.target as HTMLElement;
    this.menuPart.set(
      target.closest('[data-testid="layer-mask"]')
        ? 'mask'
        : target.closest('[data-testid="layer-blending"]')
          ? 'blend'
          : 'layer'
    );
    const id = target.closest<HTMLElement>('[data-layer]')?.dataset['layer'] ?? null;
    const layer = this.store.layers().find(row => row.layer.id === id)?.layer ?? null;
    if (layer && !this.store.selection().has(layer.id)) {
      this.choose(layer, false);
    }

    this.menuId.set(layer?.id ?? null);
  }

  /** The filter chosen in a menu, on the layer the menu is about. */
  protected addChosenStep(node: string): void {
    const layer = this.menuLayer();
    if (layer) {
      void this.addStep(layer, node);
    }
  }

  /** Arrow keys walk the rows as a tree does: up and down, into a folder and out to the one it is in. */
  protected key(event: KeyboardEvent, row: LayerRow): void {
    if (event.target !== event.currentTarget || event.altKey || event.metaKey || event.ctrlKey) {
      return;
    }

    const rows = this.rows();
    const at = rows.findIndex(held => held.layer.id === row.layer.id);
    const folder = row.layer.kind === 'group';
    let next: LayerRow | undefined;
    switch (event.key) {
      case 'ArrowDown':
        next = rows[at + 1];
        break;
      case 'ArrowUp':
        next = rows[at - 1];
        break;
      case 'Home':
        next = rows[0];
        break;
      case 'End':
        next = rows.at(-1);
        break;
      case 'ArrowRight':
        if (folder && this.closed().has(row.layer.id)) {
          this.toggle(row.layer.id);
        } else if (folder) {
          next = rows[at + 1]?.parent === row.layer.id ? rows[at + 1] : undefined;
        }

        break;
      case 'ArrowLeft':
        if (folder && !this.closed().has(row.layer.id)) {
          this.toggle(row.layer.id);
        } else {
          next = rows.find(held => held.layer.id === row.parent);
        }

        break;
      default:
        return;
    }

    event.preventDefault();
    if (next) {
      this.choose(next.layer, false);
      this.focusChosen();
    }
  }

  protected startRename(layer: StackLayer): void {
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
      ? [{ key: 'generator', name: termName(layer.mask.generator), enabled: true, effect: null }, ...effects]
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

  /** Moves a step of a mask a place earlier or later in the order the steps run (#109). */
  protected moveWork(layer: StackLayer, effect: number, delta: -1 | 1): void {
    const effects = [...(layer.mask.effects ?? [])];
    const to = effect + delta;
    const moving = effects[effect];
    if (moving === undefined || to < 0 || to >= effects.length) {
      return;
    }

    effects.splice(effect, 1);
    effects.splice(to, 0, moving);
    void this.store.setMask(layer, { ...layer.mask, effects });
  }

  /** Adds a step to the mask, which needs a mask to run on, so a layer with none gets a white one. */
  protected async addWork(layer: StackLayer, node: string): Promise<void> {
    await this.store.setMask(layer, {
      ...layer.mask,
      source: layer.mask.source === 'None' ? 'Constant' : layer.mask.source,
      value: layer.mask.source === 'None' ? 1 : layer.mask.value,
      effects: [...(layer.mask.effects ?? []), { node, enabled: true, blend: 'Copy', opacity: 1 }]
    });
  }

  /** The layer an effect goes in: an effect has none of its own, so the next goes beside it. */
  private ownerOf(layer: StackLayer): StackLayer {
    const row = this.store.layers().find(held => held.layer.id === layer.id);
    return row?.effect ? (this.store.layers().find(held => held.layer.id === row.parent)?.layer ?? layer) : layer;
  }

  protected async addContent(layer: StackLayer, kind: string, name: string, filterNode?: string): Promise<void> {
    await this.store.addEffect(this.ownerOf(layer), kind, name, filterNode);
  }

  /** With the mask in front a step runs on the mask, and otherwise on the layer's content. */
  protected addStep(layer: StackLayer, node: string): Promise<void> {
    const name =
      this.store.vocabulary()?.filters.find(kind => kind.node === node)?.name ?? node.split('/').pop() ?? node;
    return this.store.masking() ? this.addWork(layer, node) : this.addContent(layer, 'filter', name, node);
  }

  protected async addValueMask(layer: StackLayer, value: number): Promise<void> {
    await this.store.setMask(layer, { ...layer.mask, source: 'Constant', value });
    this.store.masking.set(false);
  }

  protected async addBitmapMask(layer: StackLayer): Promise<void> {
    const asset = await this.project.pickPicture();
    if (asset) {
      await this.store.setMask(layer, { ...layer.mask, source: 'Texture', asset });
    }
  }

  /** Invert is on or off, so a second time takes it away rather than stacking another. */
  protected async invertMask(layer: StackLayer): Promise<void> {
    const node = this.store.vocabulary()?.maskEffects.find(effect => effect.name === 'Invert')?.node;
    if (node) {
      await this.store.setMask(layer, { ...layer.mask, effects: toggledEffect(layer.mask.effects ?? [], node) });
    }
  }

  /** Puts the selected layers in a new folder, where the top one of them was. */
  protected async groupSelected(): Promise<void> {
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
    await this.store.setMask(layer, { ...layer.mask, source });
    this.store.masking.set(source === 'Paint');
    this.store.maskGrey.set(0);
  }

  /** A mask from one colour of the id map, which the Properties pane then offers to choose. */
  protected async addColourMask(layer: StackLayer): Promise<void> {
    await this.store.setMask(layer, { ...layer.mask, source: 'Bake', map: 'id' });
    this.store.masking.set(false);
  }

  protected async remove(layer: StackLayer): Promise<void> {
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
    if (this.using.carried() === null && this.dropCommands(row, zone).length === 0) {
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
    const commands = this.dropCommands(row, zone);
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

  /** The steps that move the chosen layers a place up or down, as one undo step (#106). */
  private nudges(delta: 1 | -1) {
    return nudgeCommands(this.dragRows(), this.store.selection(), delta);
  }

  private async nudge(delta: 1 | -1): Promise<void> {
    const commands = this.nudges(delta);
    if (commands.length > 0) {
      await this.store.edit(...commands);
      this.focusChosen();
    }
  }

  private dropCommands(row: LayerRow, zone: DropZone) {
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
