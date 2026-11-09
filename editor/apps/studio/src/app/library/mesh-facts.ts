import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { type MeshFacts, unwrap } from '@patina/api-client';
import { messageOf } from '../core/messages';
import { count, length } from '../core/numbers';
import { LibraryStore } from './library-store';

/** The three sides of the box round a mesh, in its units. */
function sides(facts: MeshFacts): number[] {
  return [0, 1, 2].map(axis => (facts.maximum[axis] ?? 0) - (facts.minimum[axis] ?? 0));
}

/** One side, as a length where the file says what its unit is, and in its own units where it does not. */
function side(value: number, metresPerUnit: number | null | undefined): string {
  return metresPerUnit ? length(value * metresPerUnit) : value.toFixed(value >= 10 ? 0 : 2);
}

/**
 * What a mesh is (#146): its format, triangles, parts, materials (each a texture set), UV sets, size and units, read by
 * the engine from the file. Shown for the project's mesh, or for one about to replace it.
 */
@Component({
  selector: 'ptn-mesh-facts',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (facts(); as facts) {
      <div class="well flex flex-col gap-1 px-2 py-1 text-xs" data-testid="mesh-facts">
        <div class="flex flex-wrap gap-x-3 gap-y-1">
          <span class="readout" title="Format">{{ facts.format }}</span>
          <span class="readout" data-testid="mesh-facts-triangles" title="Triangles">
            {{ amountOf(facts.triangles) }} triangles
          </span>
          <span class="readout" title="Named meshes">{{ facts.parts.length }} parts</span>
          <span class="readout" data-testid="mesh-facts-uv-sets" title="Texture coordinate sets">
            {{ facts.uvSets === 0 ? 'No UVs' : facts.uvSets === 1 ? '1 UV set' : facts.uvSets + ' UV sets' }}
          </span>
          @if (facts.vertexColours) {
            <span class="readout" title="The file carries vertex colours">Vertex colours</span>
          }
        </div>
        <div class="flex flex-wrap gap-x-3 gap-y-1">
          <span class="readout" data-testid="mesh-facts-size" title="The box round the model: width, height, depth">
            {{ size() }}
          </span>
          <span class="text-foreground-muted" data-testid="mesh-facts-units">{{ units() }}</span>
        </div>
        @for (material of facts.materials; track material.name) {
          <div class="flex items-center gap-2" data-testid="mesh-facts-material">
            <span class="min-w-0 flex-1 truncate" [title]="material.name">{{ material.name || 'No material' }}</span>
            <span class="readout" title="Parts">{{ material.parts }}</span>
            <span class="readout" title="Triangles">{{ amountOf(material.triangles) }}</span>
            <span class="readout" title="UV sets every part of this texture set has">UV {{ material.uvSets }}</span>
          </div>
        }
      </div>
    } @else if (failure(); as failure) {
      <span class="text-error text-xs" data-testid="mesh-facts-failure">{{ failure }}</span>
    } @else if (path()) {
      <span class="text-foreground-muted text-xs">Reading the mesh…</span>
    }
  `
})
export class MeshFactsView {
  /** The mesh: a path below the library, or a file on this machine. Empty shows nothing. */
  readonly path = input<string>('');

  private readonly library = inject(LibraryStore);
  protected readonly facts = signal<MeshFacts | null>(null);
  protected readonly failure = signal<string | null>(null);
  protected readonly amountOf = count;

  protected readonly size = computed(() => {
    const facts = this.facts();
    return facts ? sides(facts).map(value => side(value, facts.metresPerUnit)).join(' × ') : '';
  });

  protected readonly units = computed(() => {
    const per = this.facts()?.metresPerUnit;
    return per ? `1 unit is ${length(per)}` : 'The file does not say what its unit is';
  });

  constructor() {
    effect(onCleanup => {
      const path = this.path();
      let current = true;
      onCleanup(() => (current = false));
      this.facts.set(null);
      this.failure.set(null);
      if (path) {
        void this.read(path, () => current);
      }
    });
  }

  private async read(path: string, current: () => boolean): Promise<void> {
    try {
      const client = await this.library.api();
      const facts = unwrap(await client.GET('/api/v1/library/mesh', { params: { query: { path } } }));
      if (current()) {
        this.facts.set(facts);
      }
    } catch (failure) {
      if (current()) {
        this.failure.set(messageOf(failure));
      }
    }
  }
}
