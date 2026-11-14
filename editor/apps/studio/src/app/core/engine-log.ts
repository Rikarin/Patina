import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { type components, unwrap } from '@patina/api-client';
import { LibraryStore } from '../library/library-store';
import { messageOf } from './messages';

export type EngineLogLine = components['schemas']['ServerLogEntry'];

/** How often the log is read again while someone looks at it. */
const FollowMs = 1000;

/** The most lines kept here, as many as the engine keeps. */
const MostLines = 1000;

/**
 * What the engine said lately (#192), read from `GET /api/v1/log` while the Log is shown, so nothing is asked while no
 * one looks. Any client of the engine reads the same lines; this is the editor's window on them.
 */
@Injectable({ providedIn: 'root' })
export class EngineLog {
  private readonly library = inject(LibraryStore);
  private readonly mutableLines = signal<readonly EngineLogLine[]>([]);
  private readonly mutableFailure = signal<string | null>(null);
  private after = 0;
  private timer: ReturnType<typeof setTimeout> | null = null;
  private following = 0;

  /** The lines, the oldest first. */
  readonly lines = this.mutableLines.asReadonly();

  /** Why the engine could not be asked, while it cannot. */
  readonly failure = this.mutableFailure.asReadonly();

  constructor() {
    inject(DestroyRef).onDestroy(() => this.stop());
  }

  /** Reads the log now and every second until {@link unfollow} is called as often. */
  follow(): void {
    this.following++;
    if (this.following === 1) {
      void this.read();
    }
  }

  unfollow(): void {
    this.following = Math.max(0, this.following - 1);
    if (this.following === 0) {
      this.stop();
    }
  }

  /** Forgets the lines read so far; the next ones still come. */
  clear(): void {
    this.mutableLines.set([]);
  }

  private async read(): Promise<void> {
    this.stop();
    try {
      const client = await this.library.api();
      const page = unwrap(await client.GET('/api/v1/log', { params: { query: { after: this.after } } }));
      // Lines the engine no longer holds, or a restarted engine, start the list again.
      const kept = page.dropped ? page.entries : [...this.mutableLines(), ...page.entries];
      if (page.dropped || page.entries.length > 0) {
        this.mutableLines.set(kept.slice(-MostLines));
      }

      this.after = page.next;
      this.mutableFailure.set(null);
    } catch (failure) {
      this.mutableFailure.set(messageOf(failure));
    }

    if (this.following > 0) {
      this.timer = setTimeout(() => void this.read(), FollowMs);
    }
  }

  private stop(): void {
    if (this.timer !== null) {
      clearTimeout(this.timer);
      this.timer = null;
    }
  }
}
