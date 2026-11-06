import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { LibraryPlace } from './libraries';

describe('LibraryPlace', () => {
  let folder: string;

  beforeEach(() => {
    folder = mkdtempSync(join(tmpdir(), 'patina-places-'));
  });

  afterEach(() => rmSync(folder, { recursive: true, force: true }));

  const library = (name: string) => {
    const path = join(folder, name);
    mkdirSync(path);
    writeFileSync(join(path, 'library.yaml'), '');
    return path;
  };

  it('remembers the newest first, once each, across runs', () => {
    const file = join(folder, 'libraries.json');
    const places = new LibraryPlace(file);
    places.remember('/a');
    places.remember('/b');
    places.remember('/a');

    expect(new LibraryPlace(file).recent()).toEqual(['/a', '/b']);
  });

  it('skips a library that has gone when it reopens the last one', () => {
    const places = new LibraryPlace(join(folder, 'libraries.json'));
    const kept = library('Kept.ptlib');
    places.remember(kept);
    places.remember(join(folder, 'Gone.ptlib'));

    expect(places.last()).toBe(kept);
  });

  it('starts empty on a first run, and from a damaged file', () => {
    const file = join(folder, 'libraries.json');
    expect(new LibraryPlace(file).recent()).toEqual([]);

    writeFileSync(file, 'not json');
    expect(new LibraryPlace(file).last()).toBeNull();
  });

  it('gives a typed name the library extension once', () => {
    expect(LibraryPlace.named('/games/Rust')).toBe('/games/Rust.ptlib');
    expect(LibraryPlace.named('/games/Rust.ptlib')).toBe('/games/Rust.ptlib');
  });
});
