import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import type { GraphNodeType } from '@patina/api-client';
import { XuiInputImports } from '@xui/input';
import { GraphStore } from './graph-store';

/** How a dragged node names itself to the canvas. */
export const NodeDragType = 'application/x-patina-node';

const SearchQuiet = 120;

/**
 * Every node a graph can hold, by category. Typing ranks them, best first and in the order the palette has, since both
 * ask the Server. Double-click to add, or drag onto the canvas.
 */
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
        @if (group.name) {
          <p class="section-label mt-2 mb-1">{{ group.name }}</p>
        }
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
  private readonly found = signal<{ query: string; types: readonly GraphNodeType[] }>({ query: '', types: [] });
  protected readonly query = signal('');

  protected readonly groups = computed(() => {
    const { query, types } = this.found();
    if (query !== '') {
      return types.length === 0 ? [] : [{ name: '', types }];
    }

    const groups = new Map<string, GraphNodeType[]>();
    for (const type of types) {
      const name = type.category || 'Compounds';
      groups.set(name, [...(groups.get(name) ?? []), type]);
    }

    return [...groups].map(([name, held]) => ({ name, types: held }));
  });

  constructor() {
    effect(onCleanup => {
      const query = this.query().trim();
      this.store.types();
      let stale = false;
      const timer = setTimeout(
        () =>
          void this.store.findNodes(query).then(types => {
            if (!stale) {
              this.found.set({ query, types });
            }
          }),
        query ? SearchQuiet : 0
      );
      onCleanup(() => {
        stale = true;
        clearTimeout(timer);
      });
    });
  }

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
