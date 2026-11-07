import { Injectable, signal } from '@angular/core';

const StatsKey = 'patina.view.stats';

function saved(): boolean {
  try {
    return globalThis.localStorage.getItem(StatsKey) === 'true';
  } catch {
    return false;
  }
}

/** What the View menu turns on. Render statistics are for checking the engine, so they start hidden. */
@Injectable({ providedIn: 'root' })
export class ViewOptions {
  readonly stats = signal(saved());

  toggleStats(): void {
    this.stats.update(shown => !shown);
    globalThis.localStorage.setItem(StatsKey, String(this.stats()));
  }
}
