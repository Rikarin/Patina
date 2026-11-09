import { Injectable, signal } from '@angular/core';

/**
 * Asks for autosaved work to be offered for a document, from a store that cannot reach the project store that offers
 * it: a graph or a stack that came back from its file after the Server restarted, with edits the old Server had kept
 * by autosave and never saved (#162, #226). The project store answers each ask once.
 */
@Injectable({ providedIn: 'root' })
export class RecoveryOffers {
  private readonly mutableAsked = signal<{ path: string; count: number } | null>(null);
  private count = 0;

  /** The latest document asked about, with a count, so asking twice for one document is two asks. */
  readonly asked = this.mutableAsked.asReadonly();

  /** Asks for whatever autosave kept of a document, below the library, to be offered. */
  ask(path: string): void {
    this.count += 1;
    this.mutableAsked.set({ path, count: this.count });
  }
}
