import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import type { StackLayer } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { ProjectStore } from '../library/project-store';
import { hexOf } from './layer-panel';
import { type DropZone, dropCommands } from './layer-drag';
import { type LayerRow, PaintStore } from './paint-store';

const RowHeight = 44;

const KindMarks: Readonly<Record<string, string>> = { paint: 'P', fill: 'F', filter: 'Fx', group: '' };

const MaskMarks: Readonly<Record<string, string>> = {
  Constant: 'C',
  Texture: 'T',
  Anchor: 'A',
  Generator: 'G',
  Bake: 'B',
  Paint: 'P'
};

const BlendNames: Readonly<Record<string, string>> = { Copy: 'Normal', LinearDodge: 'Add', HardLight: 'Hard Light' };

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
  imports: [XuiButtonImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col', '(document:click)': 'menu.set(null)' },
  template: `
    <div class="chrome-bar flex shrink-0 flex-wrap items-center gap-0.5 px-1 py-0.5" role="toolbar" aria-label="Layers">
      <button type="button" xuiButton size="sm" variant="ghost" data-testid="paint-add" title="Add a paint layer" (click)="add('paint', 'Paint')">
        Paint
      </button>
      <button type="button" xuiButton size="sm" variant="ghost" data-testid="paint-add-fill" title="Add a fill layer" (click)="add('fill', 'Fill')">
        Fill
      </button>
      <button type="button" xuiButton size="sm" variant="ghost" data-testid="paint-add-filter" title="Add a filter" (click)="add('filter', 'Filter', 'Levels')">
        Filter
      </button>
      <button type="button" xuiButton size="sm" variant="ghost" data-testid="paint-add-group" title="Add a folder" (click)="add('group', 'Folder')">
        Folder
      </button>
      <span class="flex-1"></span>
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        data-testid="paint-undo"
        [disabled]="!store.state()?.history?.canUndo"
        (click)="store.act('undo')"
      >
        Undo
      </button>
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        data-testid="paint-redo"
        [disabled]="!store.state()?.history?.canRedo"
        (click)="store.act('redo')"
      >
        Redo
      </button>
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        data-testid="paint-save"
        [disabled]="!store.state()?.history?.dirty"
        (click)="store.act('save')"
      >
        Save
      </button>
      @if (store.state()?.history?.dirty) {
        <span class="text-foreground-muted text-xs" data-testid="paint-unsaved" title="Unsaved changes">•</span>
      }
    </div>

    @if (store.unbaked(); as maps) {
      <div class="bg-warning-subtle flex shrink-0 items-center gap-2 px-2 py-1 text-xs" data-testid="layers-need-bake">
        <span class="min-w-0 flex-1" [title]="maps">Some masks need baked maps, and show grey until then.</span>
        <button type="button" xuiButton size="sm" data-testid="layers-bake" (click)="project.askToBake()">Bake…</button>
      </div>
    }

    <div class="min-h-0 flex-1 overflow-auto" role="tree" aria-label="Layers" data-testid="paint-layers">
      @for (row of rows(); track row.layer.id) {
        <div
          class="border-hairline relative flex items-center gap-1.5 border-b pe-2"
          role="treeitem"
          draggable="true"
          [attr.aria-level]="row.depth + 1"
          [attr.aria-selected]="row.layer.id === store.layer()"
          [attr.aria-expanded]="row.layer.kind === 'group' ? !closed().has(row.layer.id) : null"
          [attr.data-testid]="'paint-layer-' + row.layer.id"
          [class.bg-selection]="row.layer.id === store.layer()"
          [class.opacity-50]="!row.layer.enabled"
          [style.height.px]="rowHeight"
          [style.padding-inline-start.px]="row.depth * 14 + 4"
          (click)="choose(row.layer, false)"
          (contextmenu)="openMenu($event, row.layer)"
          (dragstart)="start($event, row.layer.id)"
          (dragover)="over($event, row)"
          (dragleave)="target.set(null)"
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

          @if (row.layer.kind === 'group') {
            <button
              type="button"
              class="text-foreground-muted grid size-4 shrink-0 place-items-center"
              data-testid="layer-toggle"
              [attr.aria-label]="(closed().has(row.layer.id) ? 'Open ' : 'Close ') + row.layer.name"
              (click)="$event.stopPropagation(); toggle(row.layer.id)"
            >
              <svg viewBox="0 0 16 16" class="size-3" [class.rotate-90]="!closed().has(row.layer.id)" aria-hidden="true">
                <path d="M6 3l5 5-5 5z" fill="currentColor" />
              </svg>
            </button>
            <svg viewBox="0 0 16 16" class="text-foreground-muted size-8 shrink-0" aria-hidden="true">
              <path d="M1.5 3.5h5l1.5 2h6.5v7.5h-13z" fill="currentColor" fill-opacity="0.45" />
            </svg>
          } @else {
            <span
              class="border-hairline text-foreground-muted grid size-8 shrink-0 place-items-center overflow-hidden rounded-sm border text-xs"
              data-testid="layer-thumbnail"
              [class.outline]="row.layer.id === store.layer() && !store.masking()"
              [style.background]="swatch(row.layer)"
            >
              {{ mark(row.layer) }}
            </span>
          }

          @if (row.layer.mask.source !== 'None') {
            <button
              type="button"
              class="border-hairline bg-background text-foreground-muted grid size-8 shrink-0 place-items-center rounded-sm border text-xs"
              data-testid="layer-mask"
              [attr.aria-label]="'Mask of ' + row.layer.name"
              [class.outline]="row.layer.id === store.layer() && store.masking()"
              [style.background]="maskColour(row.layer)"
              [title]="row.layer.mask.source + ' mask'"
              (click)="$event.stopPropagation(); choose(row.layer, true)"
            >
              {{ maskMark(row.layer) }}
            </button>
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
            <span class="text-foreground-muted block truncate text-xs">
              {{ blendName(row.layer) }} {{ percent(row.layer) }}%
            </span>
          </span>
        </div>
      } @empty {
        <p class="text-foreground-muted p-3 text-sm">Nothing open.</p>
      }
    </div>

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
        <button type="button" class="hover:bg-selection px-3 py-1 text-start" role="menuitem" data-testid="layer-menu-rename" (click)="startRename(at.layer)">
          Rename
        </button>
        @if (at.layer.kind !== 'group') {
          @if (at.layer.mask.source === 'None') {
            <button type="button" class="hover:bg-selection px-3 py-1 text-start" role="menuitem" data-testid="layer-menu-mask-paint" (click)="addMask(at.layer, 'Paint')">
              Add Paint Mask
            </button>
            <button type="button" class="hover:bg-selection px-3 py-1 text-start" role="menuitem" data-testid="layer-menu-mask-generator" (click)="addMask(at.layer, 'Generator')">
              Add Generator Mask
            </button>
            <button type="button" class="hover:bg-selection px-3 py-1 text-start" role="menuitem" data-testid="layer-menu-mask-colour" (click)="addColourMask(at.layer)">
              Add Colour Selection Mask
            </button>
          } @else {
            <button type="button" class="hover:bg-selection px-3 py-1 text-start" role="menuitem" data-testid="layer-menu-mask-remove" (click)="addMask(at.layer, 'None')">
              Remove Mask
            </button>
          }
        }
        <div class="border-hairline my-1 border-t"></div>
        <button type="button" class="hover:bg-selection px-3 py-1 text-start" role="menuitem" data-testid="paint-remove" (click)="remove(at.layer)">
          Delete
        </button>
      </div>
    }
  `
})
export class LayerStack {
  protected readonly store = inject(PaintStore);
  protected readonly project = inject(ProjectStore);
  protected readonly rowHeight = RowHeight;
  protected readonly closed = signal<ReadonlySet<string>>(new Set());
  protected readonly menu = signal<MenuAt | null>(null);
  protected readonly renaming = signal<string | null>(null);
  protected readonly target = signal<{ id: string; zone: DropZone } | null>(null);
  private dragged: string | null = null;

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
    this.store.layer.set(layer.id);
    this.store.masking.set(mask);
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

  protected blendName(layer: StackLayer): string {
    return BlendNames[layer.blend] ?? layer.blend;
  }

  protected percent(layer: StackLayer): number {
    return Math.round(layer.opacity * 100);
  }

  protected openMenu(event: MouseEvent, layer: StackLayer): void {
    event.preventDefault();
    event.stopPropagation();
    this.choose(layer, false);
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

  protected async addMask(layer: StackLayer, source: string): Promise<void> {
    this.menu.set(null);
    await this.store.setMask(layer, { ...layer.mask, source });
    this.store.masking.set(source === 'Paint');
  }

  /** A mask from one colour of the id map, which the Properties pane then offers to choose. */
  protected async addColourMask(layer: StackLayer): Promise<void> {
    this.menu.set(null);
    await this.store.setMask(layer, { ...layer.mask, source: 'Bake', map: 'id' });
    this.store.masking.set(false);
  }

  protected async remove(layer: StackLayer): Promise<void> {
    this.menu.set(null);
    await this.store.edit({ op: 'removeLayer', layer: layer.id });
  }

  protected start(event: DragEvent, id: string): void {
    this.dragged = id;
    event.dataTransfer?.setData('text/plain', id);
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = 'move';
    }
  }

  protected over(event: DragEvent, row: LayerRow): void {
    const zone = this.zoneOf(event, row);
    if (this.commands(row, zone).length === 0) {
      this.target.set(null);
      return;
    }

    event.preventDefault();
    this.target.set({ id: row.layer.id, zone });
  }

  protected async drop(event: DragEvent, row: LayerRow): Promise<void> {
    event.preventDefault();
    const commands = this.commands(row, this.zoneOf(event, row));
    this.dragged = null;
    this.target.set(null);
    if (commands.length > 0) {
      await this.store.edit(...commands);
    }
  }

  private commands(row: LayerRow, zone: DropZone) {
    const rows = this.store.layers().map(held => ({
      id: held.layer.id,
      parent: held.parent,
      folder: held.layer.kind === 'group'
    }));
    return this.dragged === null ? [] : dropCommands(rows, this.dragged, row.layer.id, zone);
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
