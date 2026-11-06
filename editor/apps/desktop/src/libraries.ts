import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';

const MaxRecent = 10;
const LibraryFile = 'library.yaml';
const Extension = '.ptlib';

/** A library to move to, and whether it has to be laid out first. */
export interface LibraryChoice {
  path: string;
  create: boolean;
}

/** The libraries this user has opened, newest first, kept beside the app's other per-user files. */
export class LibraryPlace {
  private list: string[];

  constructor(private readonly file: string) {
    this.list = read(file);
  }

  static isLibrary(path: string): boolean {
    return existsSync(join(path, LibraryFile));
  }

  /** The folder for a name typed into a save dialog. */
  static named(path: string): string {
    return path.endsWith(Extension) ? path : path + Extension;
  }

  recent(): string[] {
    return [...this.list];
  }

  /** The library to reopen, or null when there is none left to reopen. */
  last(): string | null {
    return this.list.find(path => LibraryPlace.isLibrary(path)) ?? null;
  }

  remember(path: string): void {
    this.list = [path, ...this.list.filter(known => known !== path)].slice(0, MaxRecent);
    this.save();
  }

  forget(path: string): void {
    this.list = this.list.filter(known => known !== path);
    this.save();
  }

  private save(): void {
    mkdirSync(dirname(this.file), { recursive: true });
    writeFileSync(this.file, JSON.stringify({ recent: this.list }, null, 2));
  }
}

function read(file: string): string[] {
  try {
    const saved = JSON.parse(readFileSync(file, 'utf8')) as { recent?: unknown };
    return Array.isArray(saved.recent) ? saved.recent.filter((path): path is string => typeof path === 'string') : [];
  } catch {
    return []; // first run, or a file the user removed: nothing to remember
  }
}
