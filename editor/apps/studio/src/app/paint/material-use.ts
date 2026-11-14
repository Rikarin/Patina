import { Injectable, inject, signal } from '@angular/core';
import { type MaterialInfo, type StackLayer, unwrap } from '@patina/api-client';
import { messageOf } from '../core/messages';
import { LibraryStore } from '../library/library-store';
import { ProjectStore } from '../library/project-store';
import { type DropZone, dropManyCommands } from './layer-drag';
import { PaintStore } from './paint-store';

/** Where in the stack a material was dropped. */
export interface MaterialDrop {
  layer: string;
  zone: DropZone;
}

/** Puts a material to work wherever its role says, from a click on the shelf or a drop on the stack. */
@Injectable({ providedIn: 'root' })
export class MaterialUse {
  private readonly library = inject(LibraryStore);
  private readonly paint = inject(PaintStore);
  private readonly project = inject(ProjectStore);

  /** What is being dragged from the shelf, since a drag says nothing about itself until it is dropped. */
  readonly carried = signal<MaterialInfo | null>(null);
  readonly failure = signal<string | null>(null);

  async use(material: MaterialInfo, drop?: MaterialDrop): Promise<void> {
    const project = this.project.current();
    if (!project) {
      return;
    }

    this.failure.set(null);
    try {
      if (material.role === 'brush') {
        const alpha = await this.brush(project.path, material);
        this.paint.brush.update(brush => ({ ...brush, alpha }));
      } else if (material.kind === 'layered-material' && material.role === 'mask') {
        // A smart mask joins the mask the layer has, narrowing it, rather than throwing it away (#135).
        const layer = this.paint.layers().find(row => row.layer.id === drop?.layer)?.layer ?? this.paint.chosen();
        if (!layer) {
          this.failure.set('Choose a layer for this mask first.');
          return;
        }

        await this.paint.edit({ op: 'applyMask', layer: layer.id, source: material.source, merge: true });
      } else if (material.kind === 'graph' || material.kind === 'image-material') {
        await this.useGraph(material, await this.publish(project.path, material), drop);
      } else {
        await this.placed(drop, () =>
          this.paint.edit({ op: 'addLayer', kind: 'material', name: material.name, material: material.source })
        );
      }
    } catch (failure) {
      this.failure.set(messageOf(failure));
    }
  }

  /**
   * Makes a material the brush's, so a stroke lays every channel of it from its own maps (#125). Only a material with
   * maps can: a graph, a picture material or a layered material, not a brush, a mask or a filter.
   */
  async paintWith(material: MaterialInfo): Promise<void> {
    const project = this.project.current();
    if (!project) {
      return;
    }

    this.failure.set(null);
    const layered = material.kind === 'layered-material';
    if (material.role !== 'material' || (material.kind !== 'graph' && material.kind !== 'image-material' && !layered)) {
      this.failure.set(`${material.name} has no maps to paint with.`);
      return;
    }

    try {
      // A layered material's layers are read by the Server as the stroke begins, so nothing is published for it.
      this.paint.material.set(
        layered
          ? { name: material.name, layered: material.source, source: material.source }
          : { name: material.name, graph: await this.publish(project.path, material), source: material.source }
      );
      // The fill and the projection tool lay a material too; any other tool gives way to the brush.
      if (this.paint.tool() !== 'fill' && this.paint.tool() !== 'projection') {
        this.paint.tool.set('brush');
      }
    } catch (failure) {
      this.failure.set(messageOf(failure));
    }
  }

  /**
   * Whether a material can be dropped on a region of the mesh: one with maps, a graph or a picture material, onto a
   * stack that is open (#110).
   */
  takesRegion(material: MaterialInfo | null): boolean {
    return (
      material !== null &&
      material.role === 'material' &&
      (material.kind === 'graph' || material.kind === 'image-material') &&
      this.paint.state() !== null
    );
  }

  /**
   * A material dropped on the mesh fills the region of the id map it landed on: a graph fill on top, masked by that
   * region's color (#110). A stack with no id map says how to get one.
   */
  async useOnRegion(material: MaterialInfo, colour: readonly number[] | null): Promise<void> {
    const project = this.project.current();
    if (!project || !this.takesRegion(material)) {
      return;
    }

    this.failure.set(null);
    if (!colour) {
      this.failure.set('Bake with the ID map on to drop a material onto a part of the mesh.');
      return;
    }

    try {
      await this.paint.addGraphLayerOnRegion(material.name, await this.publish(project.path, material), colour);
    } catch (failure) {
      this.failure.set(messageOf(failure));
    }
  }

  /** Makes a graph fill read this graph, keeping the layer it is on. */
  async fillWith(layer: StackLayer, material: MaterialInfo): Promise<void> {
    const project = this.project.current();
    if (project) {
      const graph = await this.publish(project.path, material);
      await this.paint.edit({ op: 'setLayer', layer: layer.id, fill: 'Graph', graph });
    }
  }

  private async brush(project: string, material: MaterialInfo): Promise<string> {
    const client = await this.library.api();
    const body = { project, source: material.source };
    return unwrap(await client.POST('/api/v1/library/materials/brush', { body })).alpha;
  }

  private async publish(project: string, material: MaterialInfo): Promise<string> {
    const client = await this.library.api();
    const body = { project, source: material.source };
    return unwrap(await client.POST('/api/v1/library/materials/use', { body })).graph;
  }

  /** A graph lands where its role says: a fill, a filter in the stack, or a layer's mask. */
  private async useGraph(material: MaterialInfo, graph: string, drop?: MaterialDrop): Promise<void> {
    if (material.role === 'mask') {
      const layer = this.paint.layers().find(row => row.layer.id === drop?.layer)?.layer ?? this.paint.chosen();
      if (!layer) {
        this.failure.set('Choose a layer for this mask first.');
        return;
      }

      await this.paint.setMask(layer, { ...layer.mask, source: 'Generator', generator: graph });
    } else if (material.role === 'filter') {
      await this.placed(drop, () =>
        this.paint.edit({ op: 'addLayer', kind: 'filter', name: material.name, filterNode: graph })
      );
    } else {
      await this.placed(drop, () => this.paint.addGraphLayer(material.name, graph));
    }
  }

  /** New layers arrive on top, and then go where they were dropped. */
  private async placed(drop: MaterialDrop | undefined, add: () => Promise<unknown>): Promise<void> {
    const before = new Set(this.paint.layers().map(row => row.layer.id));
    await add();
    if (!drop) {
      return;
    }

    const rows = this.paint.layers();
    const added = rows.filter(row => !before.has(row.layer.id) && (row.parent === null || before.has(row.parent)));
    const commands = dropManyCommands(
      rows.map(row => ({ id: row.layer.id, parent: row.parent, folder: row.layer.kind === 'group' })),
      added.map(row => row.layer.id),
      drop.layer,
      drop.zone
    );
    if (commands.length > 0) {
      await this.paint.edit(...commands);
    }
  }
}
