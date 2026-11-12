import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { XuiNonIdealStateImports } from '@xui/non-ideal-state';
import { XuiSwitchImports } from '@xui/switch';
import { ProjectStore } from '../library/project-store';
import { IsolationStore } from '../viewport/isolation-store';
import { PaintStore } from './paint-store';

/**
 * The model's texture sets: a document per material, of which one is being painted, each shown alone on request. A
 * set's own settings, its size, UV set and height to normal, are in Properties' document section (#201).
 */
@Component({
  selector: 'ptn-texture-sets',
  imports: [XuiButtonImports, XuiNonIdealStateImports, XuiSwitchImports],
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
              <span class="readout text-foreground-muted shrink-0 text-xs" data-testid="texture-set-size">{{
                side
              }}</span>
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
        <xui-non-ideal-state
          title="No texture sets"
          description="A texturing document's mesh has a texture set for each of its materials, each painted on its own. Open a texturing document to see them."
          data-testid="texture-sets-empty"
        />
      }
    </div>
    @if (parts().length > 1) {
      <div
        class="flex flex-col px-2 py-1"
        role="group"
        aria-label="Mesh parts to show alone"
        data-testid="texture-set-parts"
      >
        <div class="flex items-center gap-2">
          <span class="text-foreground-muted min-w-0 flex-1 truncate text-xs">Isolate parts</span>
          @if (isolated()) {
            <button
              type="button"
              xuiButton
              size="sm"
              variant="ghost"
              data-testid="texture-set-show-all"
              (click)="isolation.showAll()"
            >
              Show All
            </button>
          }
        </div>
        @for (part of parts(); track part) {
          <div class="flex h-7 items-center gap-2 text-xs">
            <span class="min-w-0 flex-1 truncate" [title]="part">{{ part }}</span>
            <xui-switch
              [aria-label]="'Show ' + part + ' alone'"
              [attr.data-testid]="'texture-set-part-' + part"
              [checked]="isolation.isolation().parts.includes(part)"
              (checkedChange)="isolation.togglePart(part)"
            />
          </div>
        }
      </div>
    }
  `
})
export class TextureSets {
  private readonly project = inject(ProjectStore);
  protected readonly store = inject(PaintStore);
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
        untracked(
          () =>
            void this.store.meshParts().then(
              parts => this.parts.set(parts),
              () => this.parts.set([])
            )
        );
      } else {
        this.parts.set([]);
      }
    });
  }
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

  protected choose(path: string): void {
    const project = this.project.current();
    if (project) {
      void this.project.openDocument(project.path, path);
    }
  }
}
