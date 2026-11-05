import { computed, signal } from '@angular/core';

interface Draft<T> {
  value: T;
  /** The event that carries it; null while it is still being edited or has not been answered. */
  until: number | null;
}

/**
 * What the user is doing right now, over the Server's document. A slider cannot follow its own echo: the event for an
 * older value arrives while the hand is already somewhere else, and on the way back from the Server a value must not
 * flicker to what it was. So a draft shows until the event carrying the last value sent has been applied, and then
 * goes, leaving the document as the only truth. A refused edit drops its draft, and the document shows through.
 */
export class DraftLayer<T> {
  private readonly drafts = signal<ReadonlyMap<string, Draft<T>>>(new Map());

  /** The drafts by key, for a view that reads many. */
  readonly values = computed(() => {
    const values = new Map<string, T>();
    for (const [key, draft] of this.drafts()) {
      values.set(key, draft.value);
    }

    return values as ReadonlyMap<string, T>;
  });

  set(key: string, value: T): void {
    this.update(drafts => drafts.set(key, { value, until: null }));
  }

  /** Says the value now held under `key` was accepted, and that event `sequence` announces it. */
  settle(key: string, value: T, sequence: number, seen: number): void {
    this.update(drafts => {
      const draft = drafts.get(key);
      if (draft?.value !== value) {
        return; // the hand has moved on, and a newer answer will settle it
      }

      if (sequence <= seen) {
        drafts.delete(key);
      } else {
        drafts.set(key, { value, until: sequence });
      }
    });
  }

  /** Called as events are applied: drafts whose event has come go. */
  reached(sequence: number): void {
    const held = this.drafts();
    if ([...held.values()].some(draft => draft.until !== null && draft.until <= sequence)) {
      this.update(drafts => {
        for (const [key, draft] of drafts) {
          if (draft.until !== null && draft.until <= sequence) {
            drafts.delete(key);
          }
        }
      });
    }
  }

  drop(key: string): void {
    if (this.drafts().has(key)) {
      this.update(drafts => drafts.delete(key));
    }
  }

  clear(): void {
    this.drafts.set(new Map());
  }

  private update(change: (drafts: Map<string, Draft<T>>) => unknown): void {
    this.drafts.update(held => {
      const drafts = new Map(held);
      change(drafts);
      return drafts;
    });
  }
}
