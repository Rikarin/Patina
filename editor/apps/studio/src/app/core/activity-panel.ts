import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { unwrap } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiProgressBarImports } from '@xui/progress-bar';
import { LibraryStore } from '../library/library-store';
import { ProjectStore } from '../library/project-store';
import { Activity, type ActivityJob, type NoticeTone } from './activity';
import { messageOf } from './messages';
import { percent } from './numbers';

/** A session the engine holds that no tab shows, which only this panel can reach. */
interface LeftOpen {
  readonly id: string;
  readonly kind: 'graph' | 'stack';
  readonly name: string;
  readonly path: string;
  readonly dirty: boolean;
}

const StateWords: Readonly<Record<string, string>> = {
  queued: 'Waiting',
  running: 'Running',
  done: 'Finished',
  failed: 'Failed',
  cancelled: 'Stopped'
};

const ToneClass: Readonly<Record<NoticeTone, string>> = {
  error: 'bg-error',
  warning: 'bg-warning',
  info: 'bg-foreground-muted',
  done: 'bg-success'
};

const ToneWords: Readonly<Record<NoticeTone, string>> = {
  error: 'Problem',
  warning: 'Warning',
  info: 'Note',
  done: 'Finished'
};

/**
 * The Activity panel (#192), over the status bar: the Server's jobs with how far each is and Stop, documents the engine
 * still holds that no tab shows, with Close, and everything the app has said, newest first. It loads the first time it
 * is shown.
 */
@Component({
  selector: 'ptn-activity',
  imports: [DatePipe, XuiButtonImports, XuiProgressBarImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'pane fixed right-2 bottom-8 z-40 flex max-h-[60vh] w-[26rem] max-w-[calc(100vw-1rem)] flex-col shadow-lg',
    role: 'dialog',
    'aria-label': 'Activity',
    'data-testid': 'activity-panel',
    '(keydown.escape)': 'activity.hide()'
  },
  template: `
    <div class="pane-bar gap-1 px-2" role="toolbar" aria-label="Activity">
      <span class="min-w-0 flex-1 truncate font-medium">Activity</span>
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        data-testid="activity-clear"
        [disabled]="activity.notices().length === 0"
        (click)="activity.clear()"
      >
        Clear
      </button>
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        class="px-1.5"
        aria-label="Close"
        title="Close"
        data-testid="activity-close"
        (click)="activity.hide()"
      >
        <svg viewBox="0 0 16 16" class="size-3.5" aria-hidden="true">
          <path d="M4 4l8 8M12 4l-8 8" stroke="currentColor" stroke-width="1.5" />
        </svg>
      </button>
    </div>

    <div class="min-h-0 flex-1 overflow-y-auto">
      <h3 class="section-label px-3 pt-2 pb-1">Jobs</h3>
      @if (jobs().length === 0) {
        <p class="text-foreground-muted px-3 pb-2 text-sm">Bakes and exports show here while they run, and after.</p>
      } @else {
        <ul aria-label="Jobs" class="pb-1">
          @for (job of jobs(); track job.id) {
            <li class="flex flex-col gap-1 px-3 py-1" data-testid="activity-job" [attr.data-state]="job.state">
              <div class="flex items-center gap-2 text-sm">
                <span class="min-w-0 flex-1 truncate" [title]="job.title">{{ job.title }}</span>
                <span class="readout text-foreground-muted shrink-0 text-xs">{{ stateText(job) }}</span>
                @if (job.going) {
                  <button
                    type="button"
                    xuiButton
                    size="sm"
                    variant="ghost"
                    data-testid="activity-stop"
                    (click)="activity.stop(job.id)"
                  >
                    Stop
                  </button>
                }
              </div>
              @if (job.current; as current) {
                <p class="text-foreground-muted truncate text-xs" data-testid="activity-current" [title]="current">
                  {{ current }}
                </p>
              }
              @if (job.state === 'running') {
                <xui-progress-bar size="sm" [aria-label]="job.title" [value]="job.progress" />
              }
              @if (job.error; as error) {
                <p class="text-error text-xs break-words">{{ error }}</p>
              }
            </li>
          }
        </ul>
      }

      @if (anyLeftOpen()) {
        <h3 class="section-label px-3 pt-2 pb-1">Open in the engine, in no tab</h3>
        <ul aria-label="Documents open in the engine" class="pb-1">
          @for (held of leftOpen(); track held.id) {
            <li class="flex items-center gap-2 px-3 py-1 text-sm" data-testid="activity-left-open">
              <span class="min-w-0 flex-1 truncate" [title]="held.path">{{ held.name }}</span>
              @if (held.dirty) {
                <span class="text-foreground-muted shrink-0 text-xs">Unsaved changes</span>
              }
              <button
                type="button"
                xuiButton
                size="sm"
                variant="ghost"
                data-testid="activity-close-session"
                [title]="held.dirty ? 'Closes it without saving: its changes are lost' : 'Closes it in the engine'"
                (click)="closeSession(held)"
              >
                Close
              </button>
            </li>
          }
        </ul>
      }

      <h3 class="section-label px-3 pt-2 pb-1">Notices</h3>
      @if (activity.notices().length === 0) {
        <p class="text-foreground-muted px-3 pb-3 text-sm">
          Nothing yet. What goes wrong, and what finishes, shows here.
        </p>
      } @else {
        <ol aria-label="Notices" class="pb-2">
          @for (notice of activity.notices(); track notice.id) {
            <li
              class="flex items-start gap-2 px-3 py-1 text-sm"
              data-testid="activity-notice"
              [attr.data-tone]="notice.tone"
            >
              <span
                class="mt-1.5 size-2 shrink-0 rounded-full"
                [class]="toneClass[notice.tone]"
                role="img"
                [attr.aria-label]="toneWords[notice.tone]"
              ></span>
              <span class="min-w-0 flex-1 break-words">
                <span class="text-foreground-muted">{{ notice.source }}:</span> {{ notice.text }}
              </span>
              <time class="readout text-foreground-muted shrink-0 text-xs" [attr.datetime]="notice.at.toISOString()">{{
                notice.at | date: 'shortTime'
              }}</time>
            </li>
          }
        </ol>
      }
    </div>
  `
})
export class ActivityPanel {
  protected readonly activity = inject(Activity);
  private readonly library = inject(LibraryStore);
  private readonly project = inject(ProjectStore);
  private readonly sessions = signal<readonly LeftOpen[]>([]);

  protected readonly toneClass = ToneClass;
  protected readonly toneWords = ToneWords;
  protected readonly jobs = this.activity.jobs;

  /** What the engine holds that no tab shows: a session a failed close left, or one another client opened. */
  protected readonly leftOpen = computed(() => {
    const shown = new Set(this.project.tabs().map(tab => tab.session));
    return this.sessions().filter(held => !shown.has(held.id));
  });
  protected readonly anyLeftOpen = computed(() => this.leftOpen().length > 0);

  constructor() {
    // Read again each time the panel shows, and when tabs come and go, since closing a tab ends its session.
    effect(() => {
      if (this.activity.open()) {
        this.project.tabs();
        untracked(() => void this.readSessions());
      }
    });
  }

  protected stateText(job: ActivityJob): string {
    return job.state === 'running' ? percent(job.progress) : (StateWords[job.state] ?? job.state);
  }

  protected async closeSession(held: LeftOpen): Promise<void> {
    try {
      const client = await this.library.api();
      const params = { params: { path: { id: held.id }, query: { discard: true } } };
      unwrap(
        held.kind === 'graph'
          ? await client.DELETE('/api/v1/graphs/{id}', params)
          : await client.DELETE('/api/v1/stacks/{id}', params)
      );
      this.activity.report({ tone: 'info', source: 'Activity', text: `${held.name} closed in the engine.` });
    } catch (error) {
      this.activity.report({ tone: 'error', source: 'Activity', text: messageOf(error) });
    }

    await this.readSessions();
  }

  private async readSessions(): Promise<void> {
    try {
      const client = await this.library.api();
      const [graphs, stacks] = await Promise.all([client.GET('/api/v1/graphs'), client.GET('/api/v1/stacks')]);
      const of = (kind: LeftOpen['kind']) => (held: Omit<LeftOpen, 'kind'>) => ({
        id: held.id,
        kind,
        name: held.name,
        path: held.path,
        dirty: held.dirty
      });
      this.sessions.set([...unwrap(graphs).map(of('graph')), ...unwrap(stacks).map(of('stack'))]);
    } catch {
      this.sessions.set([]); // no library or no engine: nothing it holds is reachable, so nothing to list
    }
  }
}
