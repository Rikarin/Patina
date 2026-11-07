import { Injectable, inject } from '@angular/core';
import { GraphStore } from '../graph/graph-store';
import { ProjectStore } from '../library/project-store';
import { PaintStore } from '../paint/paint-store';
import { desktop, type TextEdit } from './desktop';

const TextFields = 'input, textarea, select, [contenteditable=true]';

/** What each Edit command is called in a text field. */
const TextCommands: Readonly<Record<string, TextEdit>> = {
  undo: 'undo',
  redo: 'redo',
  delete: 'delete',
  selectAll: 'selectAll',
  cut: 'cut',
  copy: 'copy',
  paste: 'paste'
};

/** The keys a pane already answers, so that a menu item and its key cannot come to mean different things. */
const PaneKeys: Readonly<Record<string, KeyboardEventInit>> = {
  delete: { key: 'Delete' },
  selectAll: { key: 'a', metaKey: true, ctrlKey: true }
};

/**
 * The Edit menu. The menu takes these keys before a page sees them, so this is the one place that says what they mean
 * for what is in front: a text field keeps its own behaviour, and otherwise it is the open document's.
 */
@Injectable({ providedIn: 'root' })
export class EditCommands {
  private readonly graph = inject(GraphStore);
  private readonly paint = inject(PaintStore);
  private readonly project = inject(ProjectStore);

  /** @returns Whether the command was an Edit command. */
  run(command: string): boolean {
    const text = TextCommands[command];
    if (!text) {
      return false;
    }

    const focus = globalThis.document.activeElement;
    if (focus?.closest(TextFields)) {
      desktop()?.editText(text);
    } else if (command === 'undo' || command === 'redo') {
      this.walk(command);
    } else if (command === 'copy' || command === 'cut' || command === 'paste') {
      this.clip(command);
    } else {
      const init = PaneKeys[command];
      (focus ?? globalThis.document.body).dispatchEvent(new KeyboardEvent('keydown', { ...init, bubbles: true }));
    }

    return true;
  }

  /** Cut, copy and paste work on nodes in a graph, layers in a stack, and text in anything that has some selected. */
  private clip(command: 'copy' | 'cut' | 'paste'): void {
    if (this.project.active()?.kind !== 'graph') {
      if (!this.paint.state()) {
        desktop()?.editText(command);
      } else if (command === 'paste') {
        void this.paint.pasteLayers();
      } else if (command === 'copy') {
        void this.paint.copyLayers();
      } else {
        void this.paint.cutLayers();
      }
    } else if (command === 'paste') {
      void this.graph.paste();
    } else {
      this.graph.copySelection();
      if (command === 'cut') {
        this.graph.remove([...this.graph.selection()], []);
      }
    }
  }

  private walk(command: 'undo' | 'redo'): void {
    if (this.project.active()?.kind === 'graph') {
      this.graph[command]();
    } else {
      void this.paint.act(command);
    }
  }
}
