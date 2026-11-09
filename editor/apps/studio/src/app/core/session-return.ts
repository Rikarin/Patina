import { Injectable, signal } from '@angular/core';
import { ApiError } from '@patina/api-client';

/** What a document says when it came back from its file and the edits the Server held went with the old one. */
export const RestartedNotice = 'Patina restarted its engine and opened this document again from its last save.';

const FirstWaitMs = 250;
const LastWaitMs = 4_000;

/**
 * How many times a document found the Server restarted without its session. The tabs behind hear it here: their
 * sessions went with the old Server too, and so did their edits, so a tab that showed unsaved changes no longer has any.
 */
@Injectable({ providedIn: 'root' })
export class EngineRestarts {
  readonly count = signal(0);
}

/** What a document store does at each step of coming back, for the session a drop happened in. */
export interface SessionSteps<T> {
  /** Asks the Server for the session, and throws as the API does: a 404 is a Server that has never heard of it. */
  probe(session: T): Promise<void>;
  /** The session is still there: listen to it again from the last event applied. */
  follow(session: T): void | Promise<void>;
  /** The Server restarted without it: open the document again from its file, which is the truth (plan §3.1). */
  reopen(session: T): Promise<void>;
  /** The file is gone as well, so there is nothing to come back to. */
  lost(session: T, error: ApiError): void;
}

/**
 * How a document's session comes back after its socket drops, for a graph and a layer stack alike (#226). The Server
 * owns every session and can die at any time, and the app starts it again on the same address. So a drop asks for the
 * session first, which tells a Server that is down from one that restarted and has no such session, which a failed
 * WebSocket handshake cannot say. Down, it asks again, waiting longer each time up to a few seconds; restarted, the
 * document opens again from its file.
 */
export class SessionReturn<T> {
  private timer: ReturnType<typeof setTimeout> | null = null;
  private waitMs = FirstWaitMs;

  constructor(
    private readonly steps: SessionSteps<T>,
    private readonly restarts?: EngineRestarts
  ) {}

  /** The socket is open again, so the next drop starts with a short wait. */
  opened(): void {
    this.waitMs = FirstWaitMs;
  }

  /** The socket dropped: tries again after a wait, unless a try is already waiting. */
  dropped(session: T): void {
    if (this.timer !== null) {
      return;
    }

    this.timer = setTimeout(() => {
      this.timer = null;
      void this.attempt(session);
    }, this.waitMs);
    this.waitMs = Math.min(this.waitMs * 2, LastWaitMs);
  }

  /** Stops waiting, when the document closes or another one takes its place. */
  cancel(): void {
    if (this.timer !== null) {
      clearTimeout(this.timer);
      this.timer = null;
    }
  }

  private async attempt(session: T): Promise<void> {
    try {
      await this.steps.probe(session);
    } catch (error) {
      if (missing(error)) {
        await this.reopen(session);
      } else {
        this.dropped(session);
      }

      return;
    }

    await this.steps.follow(session);
  }

  private async reopen(session: T): Promise<void> {
    // The Server answered and has never heard of the session, which is a Server that started again.
    this.restarts?.count.update(count => count + 1);
    try {
      await this.steps.reopen(session);
    } catch (error) {
      if (missing(error)) {
        this.steps.lost(session, error);
      } else {
        this.dropped(session);
      }
    }
  }
}

function missing(error: unknown): error is ApiError {
  return error instanceof ApiError && error.status === 404;
}
