import { type Signal, computed, signal } from '@angular/core';

/**
 * A field the Server owns and a hand is on. While it is being edited what shows is what the hand said: an echo of an
 * older value, which on a slow machine arrives after the hand has moved on, must not pull the field back. Letting go
 * hands it back to the Server.
 */
export class LiveValue<T> {
  private readonly draft = signal<{ value: T } | null>(null);

  /** What the field shows. */
  readonly shown: Signal<T>;

  constructor(server: () => T) {
    this.shown = computed(() => {
      const draft = this.draft();
      return draft ? draft.value : server();
    });
  }

  edit(value: T): void {
    this.draft.set({ value });
  }

  /** The hand came off: the next echo is the truth. */
  release(): void {
    this.draft.set(null);
  }
}
