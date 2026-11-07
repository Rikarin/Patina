import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import type { GraphNodeType } from '@patina/api-client';
import { XuiInputImports } from '@xui/input';
import { GraphStore } from './graph-store';

/** How a dragged node names itself to the canvas. */
export const NodeDragType = 'application/x-patina-node';

/** Every node a graph can hold, by category: type to narrow it, double-click to add, or drag onto the canvas. */
@Component({
  selector: 'ptn-node-library',
  imports: [XuiInputImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col' },
  template: `
    <div class="shrink-0 p-2">
      <input
        xuiInput
        size="sm"
        type="search"
        aria-label="Search nodes"
        data-testid="node-library-search"
        placeholder="Search nodes"
        [value]="query()"
        (input)="query.set(text($event))"
      />
    </div>
    <div class="min-h-0 flex-1 overflow-auto px-2 pb-2" role="list" aria-label="Nodes" data-testid="node-library">
      @for (group of groups(); track group.name) {
        <p class="section-label mt-2 mb-1">{{ group.name }}</p>
        @for (type of group.types; track type.path) {
          <div
            class="hover:bg-selection cursor-grab rounded-sm px-1.5 py-0.5 text-sm"
            role="listitem"
            draggable="true"
            tabindex="0"
            data-testid="node-library-item"
            [attr.data-node]="type.path"
            [title]="type.summary"
            (dblclick)="add(type)"
            (dragstart)="drag($event, type)"
            (keydown.enter)="add(type)"
          >
            {{ type.title }}
          </div>
        }
      } @empty {
        <p class="text-foreground-muted p-2 text-xs">No node by that name.</p>
      }
    </div>
  `
})
export class NodeLibrary {
  private readonly store = inject(GraphStore);
  protected readonly query = signal('');

  protected readonly groups = computed(() => {
    const query = this.query().trim().toLowerCase();
    const groups = new Map<string, GraphNodeType[]>();
    for (const type of this.store.types().values()) {
      if (`${type.path} ${type.title} ${type.summary}`.toLowerCase().includes(query)) {
        const name = type.category || 'Compounds';
        groups.set(name, [...(groups.get(name) ?? []), type]);
      }
    }

    return [...groups]
      .map(([name, types]) => ({ name, types: [...types].sort((a, b) => a.title.localeCompare(b.title)) }))
      .sort((a, b) => a.name.localeCompare(b.name));
  });

  protected text(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  protected add(type: GraphNodeType): void {
    if (this.store.editable()) {
      this.store.adding.set(type);
    }
  }

  protected drag(event: DragEvent, type: GraphNodeType): void {
    event.dataTransfer?.setData(NodeDragType, type.path);
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = 'copy';
    }
  }
}
