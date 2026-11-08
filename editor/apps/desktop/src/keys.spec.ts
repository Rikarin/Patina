import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { KeyFile } from './keys';

describe('KeyFile', () => {
  let folder: string;

  beforeEach(() => {
    folder = mkdtempSync(join(tmpdir(), 'patina-keys-'));
  });

  afterEach(() => rmSync(folder, { recursive: true, force: true }));

  it('keeps a person’s keys across runs as readable JSON', () => {
    const file = join(folder, 'settings', 'keys.json');
    const settings = { version: 1, preset: 'standard', keys: { brushTool: ['X'] } };

    expect(new KeyFile(file).write(settings)).toBe(true);
    expect(new KeyFile(file).read()).toEqual(settings);
    expect(readFileSync(file, 'utf8')).toContain('"brushTool": [\n      "X"\n    ]');
  });

  it('reads nothing from no file, a broken one or one that is not an object', () => {
    const file = join(folder, 'keys.json');
    expect(new KeyFile(file).read()).toBeNull();
    writeFileSync(file, '{ not json');
    expect(new KeyFile(file).read()).toBeNull();
    writeFileSync(file, '[1, 2]');
    expect(new KeyFile(file).read()).toBeNull();
  });

  it('writes only a small object, and leaves the last good file otherwise', () => {
    const file = join(folder, 'keys.json');
    const keys = new KeyFile(file);
    keys.write({ version: 1, preset: 'standard', keys: {} });

    expect(keys.write('B')).toBe(false);
    expect(keys.write([1])).toBe(false);
    expect(keys.write({ junk: 'x'.repeat(70_000) })).toBe(false);
    expect(keys.read()).toEqual({ version: 1, preset: 'standard', keys: {} });
  });
});
