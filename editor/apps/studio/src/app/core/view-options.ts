import { Injectable, inject } from '@angular/core';
import { Preferences } from './preferences';

/** What the View menu turns on. Render statistics are for checking the engine, so they start hidden. */
@Injectable({ providedIn: 'root' })
export class ViewOptions {
  private readonly preferences = inject(Preferences);

  /** Whether the viewport shows render statistics, which the person's preferences keep. */
  readonly stats = this.preferences.stats;

  toggleStats(): void {
    this.preferences.set({ stats: !this.stats() });
  }
}
