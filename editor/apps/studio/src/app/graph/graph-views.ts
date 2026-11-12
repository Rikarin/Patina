import { Directive, Injectable, inject, signal } from '@angular/core';
import { GraphStore } from './graph-store';

/**
 * The graphs on screen. The document in front has the app's own store; a second graph beside it has a store of its
 * own, so two graphs are open side by side, each with its own canvas, selection and session (#40). Whichever graph the
 * user touched last is the one in front of the panes that follow a graph (the Inspector, the 2D View, History, Nodes
 * and the viewport) and of Undo, Redo, Save and the canvas keys.
 */
@Injectable({ providedIn: 'root' })
export class GraphViews {
  /** The store of the document in front, which every pane shows when there is no second graph. */
  readonly main = inject(GraphStore);
  private readonly mutableFront = signal<GraphStore>(this.main);
  /** The graph the user touched last. */
  readonly front = this.mutableFront.asReadonly();

  /** A graph's canvas was touched: it is in front from now on. */
  touch(store: GraphStore): void {
    this.mutableFront.set(store);
  }

  /** A second graph went away: the document in front is in front again. */
  release(store: GraphStore): void {
    if (this.mutableFront() === store) {
      this.mutableFront.set(this.main);
    }
  }
}

/**
 * Gives what is inside the graph in front's store. A pane under it is made again when the front changes, which an
 * `@for` over `[views.front()]` does, so its injected store is always the front's.
 */
@Directive({
  selector: '[ptnFrontGraph]',
  providers: [{ provide: GraphStore, useFactory: () => inject(FrontGraph).store }]
})
export class FrontGraph {
  /** The store of the graph in front when this was made. */
  readonly store = inject(GraphViews).front();
}
