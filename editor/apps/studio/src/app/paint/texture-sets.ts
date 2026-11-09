import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { XuiSegmentedControlImports, type XuiSegmentedOption } from '@xui/segmented-control';
import { ProjectStore } from '../library/project-store';
import { IsolationStore } from '../viewport/isolation-store';
import { PaintStore } from './paint-store';

/** How a set's height becomes its normal: as painted, or blurred a texel first so eight-bit steps do not show. */
const HeightNormals: readonly XuiSegmentedOption[] = [
  { value: 'Sharp', label: 'Sharp' },
  { value: 'Smooth', label: 'Smooth' }
];

/** The model's texture sets: a document per material, of which one is being painted. */
@Component({
  selector: 'ptn-texture-sets',
  imports: [XuiSegmentedControlImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col overflow-auto p-1' },
  template: `
    <div role="listbox" aria-label="Texture sets" data-testid="texture-sets">
      @for (set of sets(); track set.path) {
        <div class="border-hairline flex h-7 w-full items-center border-b">
          <button
            type="button"
            role="option"
            class="hover:bg-selection flex h-full min-w-0 flex-1 items-center gap-2 px-2 text-start text-sm"
            [attr.aria-selected]="set.path === front()"
            [attr.data-testid]="'texture-set-' + set.name"
            [class.outline]="set.path === front()"
            (click)="choose(set.path)"
          >
            <span class="min-w-0 flex-1 truncate">{{ set.name }}</span>
            @if (set.path === front() && size(); as side) {
              <span class="readout text-foreground-muted shrink-0 text-xs" data-testid="texture-set-size">{{ side }}</span>
            }
          </button>
          @if (set.material) {
            <button
              type="button"
              class="text-foreground-muted grid size-6 shrink-0 place-items-center text-xs"
              [attr.data-testid]="'texture-set-isolate-' + set.name"
              [attr.aria-label]="'Show ' + set.name + ' alone in the viewport'"
              [attr.aria-pressed]="isolatedMaterial() === set.material"
              [class.bg-selection]="isolatedMaterial() === set.material"
              [title]="isolatedMaterial() === set.material ? 'Show the whole model' : 'Show only this texture set'"
              (click)="isolation.toggleMaterial(set.material)"
            >
              ◎
            </button>
          }
        </div>
      } @empty {
        <p class="text-foreground-muted p-2 text-sm">Nothing open.</p>
      }
    </div>
    @if (parts().length > 1) {
      <div class="flex flex-col px-2 py-1" role="group" aria-label="Mesh parts to show alone" data-testid="texture-set-parts">
        <div class="flex items-center gap-2">
          <span class="text-foreground-muted min-w-0 flex-1 truncate text-xs">Isolate parts</span>
          @if (isolated()) {
            <button type="button" class="text-xs underline" data-testid="texture-set-show-all" (click)="isolation.showAll()">
              Show all
            </button>
          }
        </div>
        @for (part of parts(); track part) {
          <label class="flex items-center gap-2 text-xs">
            <input
              type="checkbox"
              [attr.data-testid]="'texture-set-part-' + part"
              [checked]="isolation.isolation().parts.includes(part)"
              (change)="isolation.togglePart(part)"
            />
            <span class="truncate">{{ part }}</span>
          </label>
        }
      </div>
    }
    @if (heightNormal(); as method) {
      <div
        class="flex items-center gap-2 px-2 py-1"
        title="A height channel lights the surface through the normal. Smooth blurs it a texel first."
        data-testid="texture-set-height-normal"
      >
        <span class="text-foreground-muted min-w-0 flex-1 truncate text-xs">Height to normal</span>
        <xui-segmented-control
          size="sm"
          ariaLabel="Height to normal"
          [options]="heightNormals"
          [value]="method"
          (valueChange)="setHeightNormal($event)"
        />
      </div>
    }
  `
})
export class TextureSets {
  private readonly project = inject(ProjectStore);
  private readonly store = inject(PaintStore);
  protected readonly isolation = inject(IsolationStore);
  /** The named meshes of the open set, which can be shown alone. */
  protected readonly parts = signal<readonly string[]>([]);
  protected readonly isolated = computed(() => {
    const held = this.isolation.isolation();
    return held.parts.length > 0 || held.materials.length > 0;
  });
  /** The one material shown alone, if that is what the viewport shows. */
  protected readonly isolatedMaterial = computed(() => {
    const held = this.isolation.isolation();
    return held.parts.length === 0 && held.materials.length === 1 ? held.materials[0] : null;
  });

  constructor() {
    effect(() => {
      if (this.store.state()?.id) {
        untracked(() => void this.store.meshParts().then(parts => this.parts.set(parts), () => this.parts.set([])));
      } else {
        this.parts.set([]);
      }
    });
  }
  protected readonly heightNormals = HeightNormals;
  protected readonly front = computed(() => this.project.document()?.path);
  /** A mesh with one material has one set, which is the document itself. */
  protected readonly sets = computed(() => {
    const named = this.project.textureSets();
    const document = this.project.document();
    return named.length > 0
      ? named.map(set => ({ path: set.path, name: set.material ?? set.name, material: set.material ?? null }))
      : document?.kind === 'texturing'
        ? [{ path: document.path, name: document.name, material: null }]
        : [];
  });
  protected readonly size = computed(() => {
    const state = this.store.state();
    return state ? `${state.width}×${state.height}` : null;
  });
  /** How the open set's height reaches its normal, for a set that has a height channel to say it of. */
  protected readonly heightNormal = computed(() => {
    const state = this.store.state();
    return state?.channels.some(channel => channel.usage === 'height') ? state.heightNormal : null;
  });

  protected choose(path: string): void {
    const project = this.project.current();
    if (project) {
      void this.project.openDocument(project.path, path);
    }
  }

  protected setHeightNormal(method: string | null): void {
    if (method && method !== this.heightNormal()) {
      void this.store.edit({ op: 'setHeightNormal', method });
    }
  }
}
