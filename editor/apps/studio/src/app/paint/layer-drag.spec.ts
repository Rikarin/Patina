import { describe, expect, it } from 'vitest';
import { type DragRow, dropCommands } from './layer-drag';

const rows: DragRow[] = [
  { id: 'top', parent: null, folder: false },
  { id: 'folder', parent: null, folder: true },
  { id: 'inner', parent: 'folder', folder: false },
  { id: 'deep', parent: 'folder', folder: true },
  { id: 'deepest', parent: 'deep', folder: false },
  { id: 'middle', parent: null, folder: false },
  { id: 'base', parent: null, folder: false }
];

const up = (layer: string) => ({ op: 'moveLayer', layer, delta: 1 });
const down = (layer: string) => ({ op: 'moveLayer', layer, delta: -1 });

describe('dropping a layer', () => {
  it('moves it up through its siblings, a step at a time', () => {
    expect(dropCommands(rows, 'base', 'top', 'above')).toEqual([up('base'), up('base'), up('base')]);
  });

  it('moves it down through its siblings', () => {
    expect(dropCommands(rows, 'top', 'middle', 'below')).toEqual([down('top'), down('top')]);
  });

  it('does nothing when it lands where it already is', () => {
    expect(dropCommands(rows, 'folder', 'top', 'below')).toEqual([]);
    expect(dropCommands(rows, 'folder', 'middle', 'above')).toEqual([]);
  });

  it('puts it in a folder', () => {
    expect(dropCommands(rows, 'base', 'folder', 'into')).toEqual([{ op: 'nestLayer', layer: 'base', parent: 'folder' }]);
  });

  it('takes it out of a folder to beside a row', () => {
    expect(dropCommands(rows, 'inner', 'middle', 'below')).toEqual([
      { op: 'nestLayer', layer: 'inner', parent: null },
      down('inner'),
      down('inner'),
      down('inner')
    ]);
  });

  it('lands it at the top of the stack from a folder with no steps', () => {
    expect(dropCommands(rows, 'inner', 'top', 'above')).toEqual([{ op: 'nestLayer', layer: 'inner', parent: null }]);
  });

  it('will not put a folder inside itself', () => {
    expect(dropCommands(rows, 'folder', 'deep', 'into')).toEqual([]);
    expect(dropCommands(rows, 'folder', 'deepest', 'above')).toEqual([]);
    expect(dropCommands(rows, 'folder', 'folder', 'into')).toEqual([]);
  });

  it('only a folder can be dropped into', () => {
    expect(dropCommands(rows, 'base', 'middle', 'into')).toEqual([]);
  });

  it('does nothing for a folder it is already directly in', () => {
    expect(dropCommands(rows, 'inner', 'folder', 'into')).toEqual([]);
  });
});
