import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  signal,
  untracked
} from '@angular/core';
import { XuiNonIdealStateImports } from '@xui/non-ideal-state';
import { XuiSelectImports } from '@xui/select';
import { Activity } from '../core/activity';
import { ProjectStore } from '../library/project-store';
import { GraphPane } from './graph-pane';
import { GraphStore } from './graph-store';
import { GraphViews } from './graph-views';

/** The choice that shows no graph here. */
const None = '';

/**
 * A second graph beside the one in front, so two can be seen and edited side by side (#40). It has a store of its own,
 * which joins the session of one of the open graphs: the canvas, selection, palette and keys are its own, and
 * whichever of the two was touched last is what the Inspector, the 2D View, History, Undo and Save follow. It never
 * opens or closes a session itself, so the tab stays what owns it, and a tab that closes takes it from here too.
 */
@Component({
  selector: 'ptn-graph-beside',
  imports: [GraphPane, XuiNonIdealStateImports, XuiSelectImports],
  providers: [GraphStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col', 'data-testid': 'graph-beside' },
  template: `
    <div class="chrome-bar flex min-h-8 shrink-0 items-center gap-2 px-2 py-1">
      <xui-select
        class="max-w-64 min-w-0 flex-1"
        aria-label="Graph beside"
        placeholder="Choose a graph…"
        data-testid="graph-beside-choice"
        title="One of the open graphs, to see and edit beside the one in front"
        [filterable]="false"
        [itemText]="graphName()"
        [items]="choices()"
        [value]="shown()"
        (valueChange)="choose($event)"
      />
      @if (views.front() === store && store.document() !== null) {
        <span class="readout text-xs" data-testid="graph-beside-front">In front</span>
      }
    </div>
    @if (shown() !== null) {
      <ptn-graph class="min-h-0 flex-1" />
    } @else {
      <xui-non-ideal-state
        class="flex-1"
        title="A second graph"
        data-testid="graph-beside-empty"
        [description]="emptyText()"
      />
    }
  `
})
export class GraphBeside {
  protected readonly store = inject(GraphStore);
  protected readonly views = inject(GraphViews);
  private readonly project = inject(ProjectStore);
  private readonly activity = inject(Activity);

  /** The open graphs, each a tab that holds its session. */
  protected readonly graphs = computed(() => this.project.tabs().filter(tab => tab.kind === 'graph'));

  /** What the choice offers: None, which lets the graph go, then each open graph. */
  protected readonly choices = computed(() => [None, ...this.graphs().map(tab => tab.path)]);
  protected readonly graphName = computed(() => {
    const tabs = this.graphs();
    return (path: string) => (path === None ? 'None' : (tabs.find(tab => tab.path === path)?.name ?? path));
  });

  protected readonly emptyText = computed(() =>
    this.graphs().length > 0
      ? 'Choose one of the open graphs to see and edit it beside the one in front.'
      : 'Open another graph in a tab, and it can be shown here.'
  );

  /** The tab chosen to show here. */
  private readonly chosen = signal<string | null>(null);

  /**
   * The tab whose graph is shown here, while it is open and its session lasts. Going into a compound keeps it, since
   * the store keeps the way back.
   */
  protected readonly shown = computed(() => {
    const chosen = this.chosen();
    const kind = this.store.session().kind;
    const open = kind === 'opening' || kind === 'open';
    return open && chosen !== null && this.graphs().some(tab => tab.path === chosen) ? chosen : null;
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.views.release(this.store);
    });

    // A tab behind keeps what it last said of its edits, and this graph's edits are made here.
    effect(() => {
      const path = this.shown();
      const dirty = this.store.unsaved();
      if (path !== null && path !== this.project.active()?.path) {
        untracked(() => {
          this.project.noteDirty(path, dirty);
        });
      }
    });

    // What goes wrong here is said where everything else is.
    for (const [said, tone] of [
      [this.store.problem, 'error'],
      [this.store.notice, 'info']
    ] as const) {
      let last: string | null = null;
      effect(() => {
        const text = said();
        if (text !== null && text !== last) {
          untracked(() => {
            this.activity.report({ tone, source: 'Graph', text });
          });
        }

        last = text;
      });
    }

    // A graph whose tab closed, or that did not open, or a project that changed, leaves nothing here.
    effect(() => {
      const session = this.store.session();
      if (this.chosen() !== null && this.shown() === null) {
        untracked(() => {
          if (session.kind === 'failed') {
            this.activity.report({ tone: 'error', source: 'Graph', text: session.reason });
          }

          this.leave();
        });
      }
    });
  }

  protected choose(path: string | null): void {
    if (!path || path === None) {
      this.leave();
      return;
    }

    this.chosen.set(path);
    this.views.touch(this.store);
    void this.store.open(path);
  }

  /** Lets the graph go, leaving its session to its tab. */
  private leave(): void {
    this.chosen.set(null);
    this.views.release(this.store);
    this.store.detach();
  }
}
