import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { type MeshFacts, unwrap } from '@patina/api-client';
import { XuiNonIdealStateImports } from '@xui/non-ideal-state';
import { XuiSegmentedControlImports, type XuiSegmentedOption } from '@xui/segmented-control';
import { XuiSelectImports } from '@xui/select';
import { SystemStatus } from '../core/system-status';
import { LibraryStore } from '../library/library-store';
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
  imports: [XuiNonIdealStateImports, XuiSegmentedControlImports, XuiSelectImports],
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
              class="text-xs underline"
              data-testid="texture-set-show-all"
              (click)="isolation.showAll()"
            >
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
    @if (store.state(); as state) {
      <div
        class="flex flex-col gap-1 px-2 py-1"
        role="group"
        aria-label="Texture set settings"
        data-testid="texture-set-settings"
      >
        <div
          class="flex flex-col gap-1"
          title="What the set is painted, shown and exported at. Changing it rewrites no paint: a layer painted at another size is sampled at this one, and grows to it, exactly, the next time it is painted."
        >
          <span class="text-foreground-muted truncate text-xs">Resolution</span>
          <div class="grid grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)] items-center gap-1">
            <xui-select
              class="min-w-0"
              size="sm"
              ariaLabel="Texture set width"
              data-testid="texture-set-width"
              [items]="sides()"
              [itemText]="texels"
              [value]="state.width"
              (valueChange)="setSize($event, state.height)"
            />
            <span class="text-foreground-muted text-xs">×</span>
            <xui-select
              class="min-w-0"
              size="sm"
              ariaLabel="Texture set height"
              data-testid="texture-set-height"
              [items]="sides()"
              [itemText]="texels"
              [value]="state.height"
              (valueChange)="setSize(state.width, $event)"
            />
          </div>
        </div>
        @if (uvSets().length > 1) {
          <div
            class="flex items-center gap-2"
            title="Which of the mesh's UV sets this texture set is painted, baked and shown in."
          >
            <span class="text-foreground-muted min-w-0 flex-1 truncate text-xs">UV set</span>
            <xui-select
              class="min-w-0"
              size="sm"
              ariaLabel="UV set"
              data-testid="texture-set-uv-set"
              [items]="uvSets()"
              [itemText]="uvSetName"
              [value]="state.uvSet"
              (valueChange)="setUvSet($event)"
            />
          </div>
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
  protected readonly store = inject(PaintStore);
  private readonly status = inject(SystemStatus);
  private readonly library = inject(LibraryStore);
  /** What the project's mesh is, for the UV sets each of its texture sets can be laid out in. */
  private readonly facts = signal<MeshFacts | null>(null);
  /** The sides a set can have, from the Server, and the ones it has even if the list has not. */
  protected readonly sides = computed(() => {
    const state = this.store.state();
    const held = state ? [state.width, state.height] : [];
    return [...new Set([...this.status.textureSides(), ...held])].sort((a, b) => a - b);
  });
  /** The UV sets every part of the open set has, and the one it is laid out in even if the mesh has lost it. */
  protected readonly uvSets = computed(() => {
    const state = this.store.state();
    const facts = this.facts();
    const material = facts?.materials.find(entry => entry.name === state?.set) ?? facts?.materials[0];
    const count = Math.max(material?.uvSets ?? 1, (state?.uvSet ?? 0) + 1);
    return Array.from({ length: count }, (_, index) => index);
  });
  protected readonly texels = (side: number): string => `${side}`;
  protected readonly uvSetName = (index: number): string => `UV ${index + 1}`;
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
    effect(onCleanup => {
      const contents = this.project.current();
      const on = contents?.documents.find(document => !!document.mesh)?.mesh;
      const mesh = (contents?.meshes.find(held => !!on && held.id === on) ?? contents?.meshes[0])?.path;
      let current = true;
      onCleanup(() => (current = false));
      untracked(() => this.facts.set(null));
      if (mesh) {
        void this.library
          .api()
          .then(async client => unwrap(await client.GET('/api/v1/library/mesh', { params: { query: { path: mesh } } })))
          .then(
            facts => current && this.facts.set(facts),
            () => undefined
          );
      }
    });
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

  protected setSize(width: number | null, height: number | null): void {
    const state = this.store.state();
    if (state && width && height && (width !== state.width || height !== state.height)) {
      void this.store.edit({ op: 'setSettings', width, height });
    }
  }

  protected setUvSet(uvSet: number | null): void {
    if (uvSet !== null && uvSet !== this.store.state()?.uvSet) {
      void this.store.edit({ op: 'setSettings', uvSet });
    }
  }

  protected setHeightNormal(method: string | null): void {
    if (method && method !== this.heightNormal()) {
      void this.store.edit({ op: 'setHeightNormal', method });
    }
  }
}
