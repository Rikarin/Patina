import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { ProjectStore } from '../library/project-store';
import { GraphStore } from './graph-store';

const Labels: Readonly<Record<string, string>> = {
  baseColor: 'Base Color',
  height: 'Height',
  normal: 'Normal',
  roughness: 'Roughness'
};

/** A material made from a picture: its maps, each with its settings in the Inspector, and no canvas (ADR 0012). */
@Component({
  selector: 'ptn-image-material',
  imports: [XuiButtonImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col gap-2 overflow-auto p-3' },
  template: `
    <p class="text-foreground-muted text-xs" data-testid="image-material-note">
      Height comes from the picture's brightness, so lighting in a photo stays in the material.
    </p>
    @for (map of maps(); track map.id) {
      <button
        type="button"
        xuiButton
        size="sm"
        data-testid="image-material-map"
        [variant]="graph.selection().has(map.id) ? 'default' : 'ghost'"
        (click)="graph.select([map.id])"
      >
        {{ map.label }}
      </button>
    }
    <span class="flex-1"></span>
    <button
      type="button"
      xuiButton
      size="sm"
      variant="ghost"
      data-testid="image-material-graph"
      (click)="project.openAsGraph()"
    >
      Open as Graph
    </button>
  `
})
export class ImageMaterialPane {
  protected readonly graph = inject(GraphStore);
  protected readonly project = inject(ProjectStore);

  protected readonly maps = computed(() =>
    [...(this.graph.document()?.nodes.values() ?? [])]
      .filter(node => node.type === 'Output/Output')
      .map(node => {
        const usage = node.texts['Usage'] ?? 'baseColor';
        return { id: node.id, label: Labels[usage] ?? usage };
      })
  );
}
