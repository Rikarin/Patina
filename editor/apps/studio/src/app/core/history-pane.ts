import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import type { HistoryList, HistoryStep } from '@patina/api-client';
import { XuiNonIdealStateImports } from '@xui/non-ideal-state';
import { GraphStore } from '../graph/graph-store';
import { ProjectStore } from '../library/project-store';
import { PaintStore } from '../paint/paint-store';

/** One row of the list: a step, or the document as it was opened. */
interface Row {
  step: HistoryStep | null;
  label: string;
  undone: boolean;
}

/**
 * The undo history of the document in front, oldest at the top: the steps that stand, then the ones undone, greyed.
 * Clicking one takes the document back or forward to just after it, as one step on the Server, so another client sees
 * one jump. The list is read again whenever the newest standing step changes.
 */
@Component({
  selector: 'ptn-history',
  imports: [XuiNonIdealStateImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col' },
  template: `
    @if (rows(); as rows) {
      <ol class="min-h-0 flex-1 overflow-y-auto py-1" aria-label="Undo history" data-testid="history-list">
        @for (row of rows; track row.step?.id ?? 0; let last = $last) {
          <li>
            <button
              type="button"
              class="hover:bg-hover flex w-full items-center gap-2 px-2 py-0.5 text-left text-sm"
              data-testid="history-step"
              [class.bg-selection]="row.step?.id === current()"
              [class.text-foreground-muted]="row.undone"
              [class.italic]="row.undone"
              [attr.aria-current]="row.step?.id === current() ? 'step' : null"
              [title]="row.undone ? 'Redo up to ' + row.label : 'Go back to just after ' + row.label"
              (click)="jump(row.step)"
            >
              <span class="min-w-0 flex-1 truncate">{{ row.label }}</span>
            </button>
          </li>
        }
      </ol>
    } @else {
      <xui-non-ideal-state class="flex-1" title="No history" description="Open a document to see its undo steps." />
    }
  `
})
export class HistoryPane {
  private readonly project = inject(ProjectStore);
  private readonly graph = inject(GraphStore);
  private readonly paint = inject(PaintStore);
  private readonly list = signal<HistoryList | null>(null);

  /** The history the document in front reports, which says when the list changed. */
  private readonly history = computed(() =>
    this.project.active() === null
      ? null
      : this.project.graphInFront()
        ? (this.graph.document()?.history ?? null)
        : (this.paint.state()?.history ?? null)
  );

  /** The newest step that stands, or null at the start. */
  protected readonly current = computed(() => this.history()?.step ?? null);

  protected readonly rows = computed<Row[] | null>(() => {
    const list = this.list();
    if (this.history() === null || list === null) {
      return null;
    }

    return [
      { step: null, label: 'Opened', undone: false },
      ...list.done.map(step => ({ step, label: step.label, undone: false })),
      ...list.undone.map(step => ({ step, label: step.label, undone: true }))
    ];
  });

  constructor() {
    // A new step, an undo or a redo moves the newest step, which is the time to read the list again.
    effect(() => {
      const history = this.history();
      const key = [this.project.active()?.path, history?.step, history?.canRedo, history?.redoLabel].join('|');
      untracked(() => void this.read(history === null ? null : key));
    });
  }

  private reading = '';

  private async read(key: string | null): Promise<void> {
    if (key === null) {
      this.list.set(null);
      return;
    }

    this.reading = key;
    try {
      const list = await (this.project.graphInFront() ? this.graph.historySteps() : this.paint.historySteps());
      if (this.reading === key) {
        this.list.set(list);
      }
    } catch {
      // The document closed while asking; the next change asks again.
    }
  }

  protected jump(step: HistoryStep | null): void {
    const id = step?.id ?? null;
    if (id === this.current()) {
      return;
    }

    void (this.project.graphInFront() ? this.graph.jumpTo(id) : this.paint.jumpTo(id));
  }
}
