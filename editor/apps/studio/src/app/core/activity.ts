import { Injectable, Injector, type Signal, computed, effect, inject, signal, untracked } from '@angular/core';
import type { JobState } from '@patina/api-client';
import { BakeStore } from '../bake/bake-store';
import { messageOf } from './messages';

/** How a notice reads: something went wrong, something to know, or something finished. */
export type NoticeTone = 'error' | 'warning' | 'info' | 'done';

/** One thing the app said, kept so that it can be read again after it has gone from where it was said. */
export interface Notice {
  readonly id: number;
  readonly at: Date;
  readonly tone: NoticeTone;
  /** Where it came from, in the app's words: Layers, Graph, Bake, Export, Library, Engine. */
  readonly source: string;
  readonly text: string;
}

/** What a notice is made of. {@link Activity.report} is the one call that makes one. */
export interface NoticeReport {
  readonly tone: NoticeTone;
  readonly source: string;
  readonly text: string;
}

/** A job of the Server's, as the Activity panel lists it. */
export interface ActivityJob {
  readonly id: string;
  readonly kind: 'bake' | 'export';
  /** What it is, as a person would say it: "Bake of Body", "Export of the library". */
  readonly title: string;
  readonly state: JobState['state'];
  /** 0 to 1. */
  readonly progress: number;
  /** What a running export is making now, the project, preset and file its last progress named; null for a bake. */
  readonly current: string | null;
  readonly error: string | null;
  readonly created: Date;
  /** Whether it is waiting or running, so Stop would do something. */
  readonly going: boolean;
}

/** How many notices are kept: enough to scroll back over a session, few enough to stay cheap. */
const Kept = 200;
/** A job first heard of already over is told about only when it is this new, as a bake the cache answered is. */
const FreshMs = 15_000;

const Terminal: ReadonlySet<string> = new Set(['done', 'failed', 'cancelled']);

function fileStem(path: string): string {
  return (path.split(/[\\/]/).pop() ?? path).replace(/\.[^.]+$/, '');
}

/** What a job is called, from what it was asked to do. */
export function jobTitle(job: JobState): string {
  if (job.bake) {
    return `Bake of ${job.bake.material ?? job.bake.name ?? fileStem(job.bake.low)}`;
  }

  const projects = job.build?.projects?.length ?? 0;
  if (job.build?.asset) {
    return 'Export of what uses a material';
  }

  return projects === 0 ? 'Export of the library' : projects === 1 ? 'Export of a project' : `Export of ${projects} projects`;
}

/** The job as the panel lists it. */
export function activityJob(job: JobState): ActivityJob {
  return {
    id: job.id,
    kind: job.build ? 'export' : 'bake',
    title: jobTitle(job),
    state: job.state,
    progress: job.progress,
    current: job.state === 'running' ? (job.report?.current ?? null) : null,
    error: job.error ?? null,
    created: new Date(job.created),
    going: job.state === 'queued' || job.state === 'running'
  };
}

/** What a job that ended says, or null for one that has not. */
export function endingOf(job: ActivityJob): NoticeReport | null {
  const source = 'Jobs';
  switch (job.state) {
    case 'done':
      return { tone: 'done', source, text: `${job.title} finished.` };
    case 'failed':
      return { tone: 'error', source, text: `${job.title} failed${job.error ? `: ${job.error}` : '.'}` };
    case 'cancelled':
      return { tone: 'info', source, text: `${job.title} stopped.` };
    default:
      return null;
  }
}

/**
 * One place for what the app is doing and what it said (#192): the Server's jobs, which it owns, with how far each is
 * and a way to stop it, and a history of notices from every part of the app, refusals, failures and finished work
 * alike. The status bar reads it, and the Activity panel shows it.
 *
 * **To report something:** `inject(Activity).report({ tone, source, text })`. A Server job needs nothing: once the
 * {@link BakeStore} knows it (`track`), it is listed and its end is a notice. A pane that already keeps a failure in a
 * signal can hand the signal to {@link listen} instead, and each new value becomes a notice.
 */
@Injectable({ providedIn: 'root' })
export class Activity {
  private readonly bakes = inject(BakeStore);
  private readonly injector = inject(Injector);
  private readonly mutableNotices = signal<readonly Notice[]>([]);
  private readonly mutableUnread = signal(0);
  private next = 1;
  /** Each job's state when it was last looked at, so its end is told once. */
  private readonly heard = new Map<string, string>();

  /** Newest first. */
  readonly notices = this.mutableNotices.asReadonly();
  /** Errors and warnings since the panel was last open. */
  readonly unread = this.mutableUnread.asReadonly();
  /** Whether the Activity panel is showing. */
  readonly open = signal(false);

  /** Every job the Server remembers, newest first. */
  readonly jobs = computed(() => this.bakes.jobs().map(activityJob));
  /** The jobs waiting or running, oldest first: the order the Server takes them in. */
  readonly going = computed(() =>
    this.jobs()
      .filter(job => job.going)
      .reverse()
  );

  /** What the status bar says about the work going on, or null with none. */
  readonly readout = computed(() => {
    const going = this.going();
    const [first] = going;
    if (!first) {
      return null;
    }

    const at = `${first.title} ${Math.round(first.progress * 100)}%`;
    const doing = first.state === 'queued' ? `${first.title}, waiting` : first.current ? `${at}, ${first.current}` : at;
    return going.length > 1 ? `${doing}, and ${going.length - 1} more` : doing;
  });

  constructor() {
    effect(() => {
      const jobs = this.jobs();
      untracked(() => {
        this.hearJobs(jobs);
      });
    });
  }

  /** Keeps a notice. One the same as the newest from the same place is not kept twice. */
  report(notice: NoticeReport): void {
    const text = notice.text.trim();
    if (!text) {
      return;
    }

    const [newest] = this.mutableNotices();
    if (newest?.source === notice.source && newest.text === text && newest.tone === notice.tone) {
      return;
    }

    this.mutableNotices.update(held => [
      { id: this.next++, at: new Date(), tone: notice.tone, source: notice.source, text },
      ...held.slice(0, Kept - 1)
    ]);
    if ((notice.tone === 'error' || notice.tone === 'warning') && !this.open()) {
      this.mutableUnread.update(count => count + 1);
    }
  }

  /** Keeps each new value of a signal that says what went wrong, or what to know, as a notice. */
  listen(source: string, tone: NoticeTone, said: Signal<string | null>): void {
    let last: string | null = null;
    effect(
      () => {
        const text = said();
        if (text !== null && text !== last) {
          untracked(() => {
            this.report({ tone, source, text });
          });
        }

        last = text;
      },
      { injector: this.injector }
    );
  }

  /** Shows the panel, which counts as reading what it holds. */
  show(): void {
    this.open.set(true);
    this.mutableUnread.set(0);
  }

  hide(): void {
    this.open.set(false);
  }

  toggle(): void {
    if (this.open()) {
      this.hide();
    } else {
      this.show();
    }
  }

  /** Forgets every notice. The jobs are the Server's, so they stay. */
  clear(): void {
    this.mutableNotices.set([]);
    this.mutableUnread.set(0);
  }

  /** Asks the Server to stop a job. Why it would not is a notice. */
  async stop(id: string): Promise<void> {
    try {
      await this.bakes.cancel(id);
    } catch (error) {
      this.report({ tone: 'error', source: 'Activity', text: messageOf(error) });
    }
  }

  private hearJobs(jobs: readonly ActivityJob[]): void {
    const now = Date.now();
    for (const job of jobs) {
      const before = this.heard.get(job.id);
      this.heard.set(job.id, job.state);
      if (before === job.state || !Terminal.has(job.state)) {
        continue;
      }

      // A job that was already over when first heard of is history the list shows, unless it has only just begun.
      if (before === undefined && now - job.created.getTime() > FreshMs) {
        continue;
      }

      const ending = endingOf(job);
      if (ending) {
        this.report(ending);
      }
    }
  }
}
