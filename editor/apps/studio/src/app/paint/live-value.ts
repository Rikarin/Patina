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

/**
 * A {@link LiveValue} in a field that shows whichever layer is chosen, and that remembers which layer it was typed for.
 * ⚠ A commit writes only to that layer: pressing another row chooses it before the field loses focus, so by the blur
 * the field shows the newly chosen layer's value, and an unguarded commit would write it onto the old one.
 */
export class LayerDraft<T> {
  private readonly value: LiveValue<T>;
  private typedFor: string | null = null;

  /** What the field shows. */
  readonly shown: Signal<T>;

  constructor(server: () => T) {
    this.value = new LiveValue(server);
    this.shown = this.value.shown;
  }

  /** Something was typed into the field for one layer. */
  edit(layer: string, value: T): void {
    this.typedFor = layer;
    this.value.edit(value);
  }

  /**
   * What was typed for this layer, handing the field back to the Server; or null when nothing was typed for it, which
   * leaves the field alone.
   */
  take(layer: string): T | null {
    if (this.typedFor !== layer) {
      return null;
    }

    const typed = this.value.shown();
    this.release();
    return typed;
  }

  /** Another layer is chosen: nothing typed is any layer's now. */
  release(): void {
    this.typedFor = null;
    this.value.release();
  }
}
