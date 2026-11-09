import { Injectable, inject, signal } from '@angular/core';
import { GraphStore } from '../graph/graph-store';
import { ProjectStore } from '../library/project-store';
import { PaintStore } from '../paint/paint-store';
import { Commands } from './commands';
import { desktop, type TextEdit } from './desktop';

/** The Edit commands a text field answers itself, by what the browser's editor calls them. */
const TextEdits: readonly TextEdit[] = ['undo', 'redo', 'cut', 'copy', 'paste', 'delete', 'selectAll'];

/**
 * The Edit menu's own handlers: a text field's edits, and Undo and Redo for the document in front. Panes register what
 * the rest mean for them (layers in the stack, nodes in a graph, a folder in the Library), so this is no longer where
 * each command is routed: {@link Commands} runs the first handler that would act.
 */
@Injectable({ providedIn: 'root' })
export class EditCommands {
  private readonly commands = inject(Commands);
  private readonly graph = inject(GraphStore);
  private readonly paint = inject(PaintStore);
  private readonly project = inject(ProjectStore);
  private installed = false;
  /** Undos and redos of a stack still going out: what the next one would do is known when they are back. */
  private readonly stepping = signal(0);

  /** Registers the handlers, once. */
  install(): void {
    if (this.installed) {
      return;
    }

    this.installed = true;
    for (const edit of TextEdits) {
      this.commands.handle(edit, {
        text: true,
        // Undo in a box of a pane that undoes its document is the document's, unless something was typed there.
        enabled: () => (edit !== 'undo' && edit !== 'redo') || this.commands.textUndoes(),
        run: () => desktop()?.editText(edit)
      });
    }

    const graphInFront = this.project.graphInFront;
    const history = () => (graphInFront() ? this.graph.document()?.history : this.paint.state()?.history) ?? null;
    // A key for either waits for the edits in flight, since each can turn the other on (#223): in a stack, its undos
    // and redos and every other edit, a mask entry, an instance or a filter's input as much as a stroke.
    const waiting = () => (graphInFront() ? this.graph.settling() : this.stepping() > 0 || this.paint.settling());
    const step = (action: 'undo' | 'redo') => {
      if (graphInFront()) {
        this.graph[action]();
        return;
      }

      this.stepping.update(count => count + 1);
      void this.paint.act(action).finally(() => this.stepping.update(count => count - 1));
    };
    this.commands.handle('undo', {
      enabled: () => (graphInFront() ? this.graph.editable() : true) && history()?.canUndo === true,
      waiting,
      label: () => (history()?.undoLabel ? `Undo ${history()?.undoLabel}` : null),
      run: () => step('undo')
    });
    this.commands.handle('redo', {
      enabled: () => (graphInFront() ? this.graph.editable() : true) && history()?.canRedo === true,
      waiting,
      label: () => (history()?.redoLabel ? `Redo ${history()?.redoLabel}` : null),
      run: () => step('redo')
    });
  }
}
