import { Injectable } from '@angular/core';

/** What a part of the app holds that names files, and no file of the library has yet. */
export type HeldSource = () => readonly string[];

/**
 * The paths this client holds unsaved (#301), such as a bake setting the project's file has not caught up with, so Clear
 * Uploads keeps an upload only an open dialog names. Each holder says what it holds when asked; the Server reads names
 * out of any text, so a holder may give a whole setting's JSON.
 */
@Injectable({ providedIn: 'root' })
export class HeldFiles {
  private readonly sources = new Set<HeldSource>();

  /** Starts counting what a holder holds; the returned function stops it. */
  hold(source: HeldSource): () => void {
    this.sources.add(source);
    return () => this.sources.delete(source);
  }

  /** Everything held now, each entry a path or a text naming paths. */
  all(): string[] {
    return [...this.sources].flatMap(source => source()).filter(text => text.length > 0);
  }
}
