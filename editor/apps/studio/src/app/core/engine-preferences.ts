import { Injectable, computed, inject, signal } from '@angular/core';
import { type BakeOptions, type ServerPreferences, unwrap } from '@patina/api-client';
import { LibraryStore } from '../library/library-store';
import { messageOf } from './messages';

/**
 * The person's settings the engine acts on for every app attached to it (#189): autosave, and how a new project bakes.
 * The Server keeps them in one file and a change replaces all of it, so every change here sends what it holds with the
 * one setting changed, and a setting this app does not know is never dropped by one it does.
 */
@Injectable({ providedIn: 'root' })
export class EnginePreferences {
  private readonly library = inject(LibraryStore);

  /** The settings as the engine last said, or null until it has. */
  readonly held = signal<ServerPreferences | null>(null);
  /** Why the last read or change failed, or null. */
  readonly failure = signal<string | null>(null);

  readonly autosave = computed(() => this.held()?.autosave ?? null);
  /** The options a new project bakes with, or null for Patina's own. */
  readonly bake = computed(() => this.held()?.bake ?? null);

  /** Asks the engine for the settings. */
  async read(): Promise<void> {
    try {
      const client = await this.library.api();
      this.held.set(unwrap(await client.GET('/api/v1/preferences')));
      this.failure.set(null);
    } catch (failure) {
      this.failure.set(messageOf(failure));
    }
  }

  /**
   * Changes some of the settings, shown at once and put back if the engine refuses them. Reads the rest first when
   * nothing is held yet. Whether the engine kept the change.
   */
  async change(change: Partial<ServerPreferences>): Promise<boolean> {
    if (!this.held()) {
      await this.read();
    }

    const held = this.held();
    if (!held) {
      return false;
    }

    this.held.set({ ...held, ...change });
    try {
      const client = await this.library.api();
      this.held.set(unwrap(await client.PUT('/api/v1/preferences', { body: { ...held, ...change } })));
      this.failure.set(null);
      return true;
    } catch (failure) {
      this.held.set(held);
      this.failure.set(messageOf(failure));
      return false;
    }
  }

  /** Makes these the options every project made from now on bakes with, or Patina's own for null. */
  makeBakeDefault(options: BakeOptions | null): Promise<boolean> {
    return this.change({ bake: options });
  }
}

/** Whether two sets of options bake the same, whatever order their fields were written in. */
export function sameOptions(a: BakeOptions, b: BakeOptions): boolean {
  return canonical(a) === canonical(b);
}

function canonical(value: unknown): string {
  return JSON.stringify(value, (_key, held: unknown) =>
    held && typeof held === 'object' && !Array.isArray(held)
      ? Object.fromEntries(Object.entries(held).sort(([a], [b]) => a.localeCompare(b)))
      : held
  );
}
