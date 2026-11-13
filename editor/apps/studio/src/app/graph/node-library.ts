import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { provideIcons } from '@ng-icons/core';
import { matStar, matStarBorder } from '@ng-icons/material-icons/baseline';
import type { GraphNodeType } from '@patina/api-client';
import { XuiInputImports } from '@xui/input';
import { IconImports } from '../core/icons';
import { Preferences } from '../core/preferences';
import { paletteOf } from './graph-authoring';
import { GraphStore } from './graph-store';

/** How a dragged node names itself to the canvas. */
export const NodeDragType = 'application/x-patina-node';

const SearchQuiet = 120;

/**
 * Every node a graph can hold: the starred ones and those added last, then every one by category. Typing ranks them,
 * best first and in the order the palette has, since both ask the Server. Double-click to add, or drag onto the
 * canvas; the star keeps a node at the top here and in the palette.
 */
@Component({
  selector: 'ptn-node-library',
  imports: [IconImports, XuiInputImports],
  viewProviders: [provideIcons({ matStar, matStarBorder })],
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
          <p class="section-label mt-2 mb-1" [attr.data-testid]="'node-library-group-' + group.name">
            {{ group.name }}
          </p>
        }
        @for (type of group.types; track type.path) {
          @let starred = favourites().has(type.path);
          <div
            class="group hover:bg-selection flex cursor-grab items-center rounded-sm ps-1.5 text-sm"
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
            @let picture = pictures.url(type.path);
            @if (picture) {
              <img
                class="me-1.5 size-6 shrink-0 rounded-xs"
                alt=""
                data-testid="node-library-picture"
                [src]="picture"
              />
            } @else {
              <span class="me-1.5 size-6 shrink-0" aria-hidden="true"></span>
            }
            <span class="min-w-0 flex-1 truncate py-0.5">{{ type.title }}</span>
            <button
              type="button"
              class="text-foreground-muted hover:text-foreground shrink-0 px-1 focus-visible:opacity-100"
              data-testid="node-library-star"
              [class.opacity-0]="!starred"
              [class.group-hover:opacity-100]="!starred"
              [attr.aria-label]="(starred ? 'Unstar ' : 'Star ') + type.title"
              [attr.aria-pressed]="starred"
              [title]="starred ? 'Takes the star away' : 'Keeps this node at the top here and in the palette'"
              (click)="preferences.toggleFavouriteNode(type.path)"
              (dblclick)="$event.stopPropagation()"
            >
              <ng-icon xui size="sm" [name]="starred ? 'matStar' : 'matStarBorder'" />
            </button>
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
  protected readonly pictures = this.store.nodePictures;
  protected readonly preferences = inject(Preferences);
  private readonly found = signal<{ query: string; types: readonly GraphNodeType[] }>({ query: '', types: [] });
  protected readonly query = signal('');
  protected readonly favourites = computed(() => new Set(this.preferences.favouriteNodes()));

  protected readonly groups = computed(() => {
    const { query, types } = this.found();
    if (query !== '') {
      return types.length === 0 ? [] : [{ name: '', types }];
    }

    const groups = new Map<string, GraphNodeType[]>();
    for (const line of paletteOf(types, this.preferences.favouriteNodes(), this.preferences.recentNodes())) {
      groups.set(line.group, [...(groups.get(line.group) ?? []), line.type]);
    }

    return [...groups].map(([name, held]) => ({ name, types: held }));
  });

  constructor() {
    // Each listed type's picture, in the order the list shows them, so the top rows come first.
    effect(() => {
      const listed = this.groups().flatMap(group => group.types.map(type => type.path));
      untracked(() => this.pictures.want(listed));
    });
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
