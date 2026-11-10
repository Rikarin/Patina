import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { type MeshFacts, unwrap } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiSegmentedControlImports, type XuiSegmentedOption } from '@xui/segmented-control';
import { XuiSelectImports } from '@xui/select';
import { desktop } from '../core/desktop';
import { labelIn } from '../core/labels';
import { count } from '../core/numbers';
import { SystemStatus } from '../core/system-status';
import { LibraryStore } from '../library/library-store';
import { ProjectStore } from '../library/project-store';
import { PaintStore } from './paint-store';

/** How a set's height becomes its normal: as painted, or blurred a texel first so eight-bit steps do not show. */
const HeightNormals: readonly XuiSegmentedOption[] = [
  { value: 'Sharp', label: 'Sharp' },
  { value: 'Smooth', label: 'Smooth' }
];

/**
 * The document's own settings, at the top of Properties whatever tool or layer is chosen (#201): what it is and the
 * mesh it is painted on, its texture set's size, UV set and height to normal (ADR 0020), what the mesh is under that
 * set (#146), and its channels, with the one to paint and Add Channel. Nothing here depends on the brush or the
 * layer, so choosing a fill no longer hides Add Channel.
 *
 * **A setting of the document or its texture set goes here**, as a row of the `dl`: a label in `dt`, the control in
 * `dd`. Brush settings go in {@link BrushPanel}, and a layer's in {@link LayerPanel}. The Texture Sets pane lists the
 * sets and shows each alone; it sets nothing, so a set's settings have this one place.
 */
@Component({
  selector: 'ptn-document-section',
  imports: [XuiButtonImports, XuiSegmentedControlImports, XuiSelectImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex flex-col gap-1.5', role: 'group', 'aria-label': 'Document', 'data-testid': 'paint-document' },
  template: `
    @if (store.state(); as state) {
      @if (project.current(); as open) {
        <div class="flex items-center gap-2">
          <span class="min-w-0 flex-1 leading-tight">
            <span class="block truncate font-semibold" data-testid="paint-project">{{ open.name }}</span>
            <span class="text-foreground-muted block truncate text-xs" data-testid="paint-mesh-name">
              {{ project.meshName() ?? project.document()?.name }}
            </span>
          </span>
          @if (canPick && project.document()?.kind === 'texturing') {
            <button
              type="button"
              xuiButton
              size="sm"
              variant="ghost"
              data-testid="paint-mesh"
              (click)="project.settingsOpen.set(true)"
            >
              {{ project.meshName() ? 'Change Mesh…' : 'Choose Mesh…' }}
            </button>
          }
        </div>
      }

      <dl
        class="grid grid-cols-[auto_minmax(0,1fr)] items-center gap-x-3 gap-y-1 text-xs"
        aria-label="Texture set settings"
        data-testid="texture-set-settings"
      >
        @if (state.set) {
          <dt class="text-foreground-muted">Texture set</dt>
          <dd class="truncate" data-testid="paint-document-set">{{ state.set }}</dd>
        }
        <dt class="text-foreground-muted" [title]="sizeHint">Size</dt>
        <dd
          class="grid grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)] items-center gap-1"
          data-testid="paint-document-size"
          [title]="sizeHint"
        >
          <xui-select
            class="min-w-0"
            size="sm"
            ariaLabel="Texture set width"
            data-testid="texture-set-width"
            [filterable]="false"
            [items]="sides()"
            [itemText]="texels"
            [value]="state.width"
            (valueChange)="setSize($event, state.height)"
          />
          <span class="text-foreground-muted">×</span>
          <xui-select
            class="min-w-0"
            size="sm"
            ariaLabel="Texture set height"
            data-testid="texture-set-height"
            [filterable]="false"
            [items]="sides()"
            [itemText]="texels"
            [value]="state.height"
            (valueChange)="setSize(state.width, $event)"
          />
        </dd>
        @if (uvSets().length > 1) {
          <dt class="text-foreground-muted" [title]="uvSetHint">UV set</dt>
          <dd [title]="uvSetHint">
            <xui-select
              class="min-w-0"
              size="sm"
              ariaLabel="UV set"
              data-testid="texture-set-uv-set"
              [filterable]="false"
              [items]="uvSets()"
              [itemText]="uvSetName"
              [value]="state.uvSet"
              (valueChange)="setUvSet($event)"
            />
          </dd>
        }
        @if (heightNormal(); as method) {
          <dt class="text-foreground-muted" [title]="heightNormalHint">Height to normal</dt>
          <dd data-testid="texture-set-height-normal" [title]="heightNormalHint">
            <xui-segmented-control
              size="sm"
              ariaLabel="Height to normal"
              [options]="heightNormals"
              [value]="method"
              (valueChange)="setHeightNormal($event)"
            />
          </dd>
        }
        @if (meshFacts(); as facts) {
          <dt class="text-foreground-muted">Mesh</dt>
          <dd
            class="readout truncate"
            data-testid="paint-document-mesh-facts"
            title="What the engine read of the mesh under this texture set: its triangles, parts and the UV sets they all have"
          >
            {{ facts }}
          </dd>
        }
      </dl>

      <div class="flex flex-wrap items-center gap-1" role="group" aria-label="Channels">
        @for (channel of state.channels; track channel.usage) {
          <button
            type="button"
            xuiButton
            size="sm"
            [variant]="channel.usage === store.channel() ? 'default' : 'ghost'"
            [attr.aria-pressed]="channel.usage === store.channel()"
            [attr.data-testid]="'paint-channel-' + channel.usage"
            (click)="store.showChannel(channel.usage)"
          >
            {{ nameOf(channel.usage) }}
          </button>
        }
        @if (addable().length > 0) {
          <select
            class="well h-7 min-w-0 px-1 text-sm"
            aria-label="Add a channel to this document"
            data-testid="paint-add-channel"
            (change)="addChannel($event)"
          >
            <option value="">Add Channel…</option>
            @for (usage of addable(); track usage) {
              <option [value]="usage">{{ nameOf(usage) }}</option>
            }
          </select>
        }
      </div>
    }
  `
})
export class DocumentSection {
  protected readonly store = inject(PaintStore);
  protected readonly project = inject(ProjectStore);
  private readonly status = inject(SystemStatus);
  private readonly library = inject(LibraryStore);
  protected readonly canPick = desktop() !== undefined;
  protected readonly heightNormals = HeightNormals;
  protected readonly texels = (side: number): string => `${side}`;
  protected readonly uvSetName = (index: number): string => `UV ${index + 1}`;
  protected readonly sizeHint =
    'What the texture set is painted, baked and shown at, and exported at unless the export gives it a size of its own. ' +
    'Changing it rewrites no paint: a layer painted at another size is sampled at this one, and grows to it, exactly, ' +
    'the next time it is painted.';
  protected readonly uvSetHint = 'Which of the mesh’s UV sets this texture set is painted, baked and shown in.';
  protected readonly heightNormalHint =
    'A height channel lights the surface through the normal. Smooth blurs it a texel first.';

  /** What the engine read of the document's mesh, for its UV sets and the row that says what it is. */
  private readonly facts = signal<MeshFacts | null>(null);

  /** A channel by the label the vocabulary carries for it. */
  protected nameOf(usage: string): string {
    return labelIn(this.store.vocabulary()?.usages, usage);
  }

  /** The channels the vocabulary has that this document does not. */
  protected readonly addable = computed(() => {
    const held = this.store.state()?.channels.map(channel => channel.usage) ?? [];
    return (this.store.vocabulary()?.usages ?? []).map(term => term.value).filter(usage => !held.includes(usage));
  });

  /** The sides a set can have, from the engine, and the ones it has even if the list has not. */
  protected readonly sides = computed(() => {
    const state = this.store.state();
    const held = state ? [state.width, state.height] : [];
    return [...new Set([...this.status.textureSides(), ...held])].sort((a, b) => a - b);
  });

  /** The mesh's material this set paints: the one named for it, or the only one. */
  private readonly material = computed(() => {
    const facts = this.facts();
    const set = this.store.state()?.set;
    return facts?.materials.find(entry => entry.name === set) ?? (facts?.materials.length === 1 ? facts.materials[0] : null);
  });

  /** The UV sets every part of the open set has, and the one it is laid out in even if the mesh has lost it. */
  protected readonly uvSets = computed(() => {
    const state = this.store.state();
    const count = Math.max(this.material()?.uvSets ?? 1, (state?.uvSet ?? 0) + 1);
    return Array.from({ length: count }, (_, index) => index);
  });

  /** One line of what the mesh under this set is. */
  protected readonly meshFacts = computed(() => {
    const material = this.material();
    if (!material) {
      return null;
    }
    const parts = material.parts === 1 ? '1 part' : `${material.parts} parts`;
    const uvSets = material.uvSets === 1 ? '1 UV set' : `${material.uvSets} UV sets`;
    return `${count(material.triangles)} triangles, ${parts}, ${uvSets}`;
  });

  /** How the open set's height reaches its normal, for a set that has a height channel to say it of. */
  protected readonly heightNormal = computed(() => {
    const state = this.store.state();
    return state?.channels.some(channel => channel.usage === 'height') ? state.heightNormal : null;
  });

  constructor() {
    effect(onCleanup => {
      const mesh = this.project.mesh()?.path;
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
  }

  protected addChannel(event: Event): void {
    const select = event.target as HTMLSelectElement;
    const usage = select.value;
    select.value = '';
    if (usage) {
      void this.store.edit({ op: 'addChannel', usage });
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
