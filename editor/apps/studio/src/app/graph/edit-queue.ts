/** One request to the Server. `merge` is passed in because coalescing decides it, not the caller. */
type Send<R> = (merge: boolean) => Promise<R>;

interface Entry<R> {
  key: string | null;
  merge: boolean;
  send: Send<R>;
  waiters: { resolve: (result: R) => void; reject: (reason: unknown) => void }[];
}

/**
 * Every edit, one at a time and in order, so that the Server sees them as they were made. A live edit, such as a slider,
 * names a key, and a newer edit with the same key replaces one still waiting: however fast a slider moves, one request
 * is out and the newest value is next. A replacement folds into the undo step only if both did, or a drag's first batch,
 * which has to open a step, could be dropped in favour of one that folds into the step before it.
 */
export class EditQueue<R> {
  private readonly waiting: Entry<R>[] = [];
  private running = false;

  /** Requests not yet answered, the one in flight included. */
  get pending(): number {
    return this.waiting.length + (this.running ? 1 : 0);
  }

  submit(send: Send<R>, options: { key?: string; merge?: boolean } = {}): Promise<R> {
    const key = options.key ?? null;
    return new Promise<R>((resolve, reject) => {
      const last = this.waiting.at(-1);
      if (last && key !== null && last.key === key) {
        last.send = send;
        last.merge &&= options.merge ?? false;
        last.waiters.push({ resolve, reject });
        return;
      }

      this.waiting.push({ key, merge: options.merge ?? false, send, waiters: [{ resolve, reject }] });
      void this.drain();
    });
  }

  /** Drops what has not been sent, as when the session it belongs to has gone. */
  clear(reason: unknown): void {
    for (const entry of this.waiting.splice(0)) {
      for (const waiter of entry.waiters) {
        waiter.reject(reason);
      }
    }
  }

  private async drain(): Promise<void> {
    if (this.running) {
      return;
    }

    this.running = true;
    for (let entry = this.waiting.shift(); entry; entry = this.waiting.shift()) {
      try {
        const result = await entry.send(entry.merge);
        entry.waiters.forEach(waiter => {
          waiter.resolve(result);
        });
      } catch (reason) {
        entry.waiters.forEach(waiter => {
          waiter.reject(reason);
        });
      }
    }

    this.running = false;
  }
}
