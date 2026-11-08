import { mkdirSync, readFileSync, renameSync, writeFileSync } from 'node:fs';
import { dirname } from 'node:path';

/** More than a person's keys could ever be: a bigger file is not one. */
const MaxBytes = 64 * 1024;

/**
 * A person's keyboard settings: `keys.json` in the app's per-user settings folder, beside `libraries.json`. Keys are a
 * person's, not a library's, so they live with the app rather than in a library. The studio decides what is in it
 * (apps/studio/src/app/core/key-bindings.ts) and reads it leniently; this keeps it a small JSON object, and writes a
 * whole file or none: a crash in the middle of a write leaves the last good one, since the new one is renamed over it.
 */
export class KeyFile {
  constructor(private readonly file: string) {}

  /** What the file holds, or null when there is none or it is not a JSON object. */
  read(): unknown {
    try {
      const text = readFileSync(this.file, 'utf8');
      const value: unknown = text.length <= MaxBytes ? JSON.parse(text) : null;
      return typeof value === 'object' && value !== null && !Array.isArray(value) ? value : null;
    } catch {
      return null; // first run, or a file the person removed: the defaults
    }
  }

  /** Writes the settings. False, and nothing written, for anything that is not a small JSON object. */
  write(settings: unknown): boolean {
    if (typeof settings !== 'object' || settings === null || Array.isArray(settings)) {
      return false;
    }

    const text = `${JSON.stringify(settings, null, 2)}\n`;
    if (text.length > MaxBytes) {
      return false;
    }

    mkdirSync(dirname(this.file), { recursive: true });
    const next = `${this.file}.next`;
    writeFileSync(next, text);
    renameSync(next, this.file);
    return true;
  }
}
