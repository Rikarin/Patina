import { describe, expect, it } from 'vitest';
import { type FolderContents, canDrop, flatten, parentOf } from './library-tree-store';

const folder = (path: string, projectCount = 0) => ({
  path,
  name: path.split('/').pop() ?? path,
  color: null,
  projectCount
});
const project = (path: string) => ({ id: path, name: path, path, tags: [], referenceCount: 0 });

describe('the Library tree', () => {
  const contents = new Map<string, FolderContents>([
    ['', { folders: [folder('props', 2)], projects: [project('crate')], total: 1 }],
    ['props', { folders: [folder('props/doors')], projects: [project('props/barrel')], total: 3 }]
  ]);

  it('hides what is in a closed folder', () => {
    expect(flatten(contents, new Set()).map(row => row.path)).toEqual(['props', 'crate']);
  });

  it('puts a folder before its projects, one level deeper', () => {
    const rows = flatten(contents, new Set(['props']));

    expect(rows.map(row => [row.path, row.depth])).toEqual([
      ['props', 0],
      ['props/doors', 1],
      ['props/barrel', 1],
      ['props', 1],
      ['crate', 0]
    ]);
    expect(rows[3]).toMatchObject({ kind: 'more', remaining: 2 });
  });

  it('finds the folder a path sits in', () => {
    expect(parentOf('crate')).toBe('');
    expect(parentOf('props/doors/oak')).toBe('props/doors');
  });

  it('refuses a drop that means nothing', () => {
    expect(canDrop(['props'], 'props')).toBe(false);
    expect(canDrop(['props'], 'props/doors')).toBe(false);
    expect(canDrop(['props/doors'], 'props')).toBe(false);
    expect(canDrop([], 'props')).toBe(false);
  });

  it('lets a project move to the top or across', () => {
    expect(canDrop(['props/barrel'], '')).toBe(true);
    expect(canDrop(['crate'], 'props')).toBe(true);
    expect(canDrop(['crate', 'props/barrel'], 'props')).toBe(true);
  });

  it('does not take a folder for its own prefix', () => {
    expect(canDrop(['prop'], 'props')).toBe(true);
  });
});
