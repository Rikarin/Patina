import { Injectable, computed, effect, signal } from '@angular/core';
import { desktop } from './desktop';

/** Who has edits that are not in a file. Each kind of document reports here, and the desktop app asks one question. */
@Injectable({ providedIn: 'root' })
export class UnsavedWork {
  private readonly sources = signal<ReadonlySet<string>>(new Set());

  readonly any = computed(() => this.sources().size > 0);

  constructor() {
    const bridge = desktop();
    if (bridge) {
      effect(() => {
        bridge.setUnsaved(this.any());
      });
    }
  }

  has(source: string): boolean {
    return this.sources().has(source);
  }

  report(source: string, unsaved: boolean): void {
    this.sources.update(held => {
      if (held.has(source) === unsaved) {
        return held;
      }

      const next = new Set(held);
      if (unsaved) {
        next.add(source);
      } else {
        next.delete(source);
      }

      return next;
    });
  }
}
