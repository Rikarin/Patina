import { describe, expect, it } from 'vitest';
import { type DragRow, dropCommands, dropManyCommands, nudgeCommands } from './layer-drag';

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
    expect(dropCommands(rows, 'base', 'folder', 'into')).toEqual([
      { op: 'nestLayer', layer: 'base', parent: 'folder' }
    ]);
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

describe('dropping several layers', () => {
  const flat: readonly DragRow[] = ['a', 'b', 'c', 'd'].map(id => ({ id, parent: null, folder: false }));

  /** Plays the moves on a flat list, as the Server would. */
  function play(commands: ReturnType<typeof dropManyCommands>): string[] {
    const order = flat.map(row => row.id);
    for (const command of commands) {
      if (command.op !== 'moveLayer') {
        throw new Error(`Unexpected ${command.op}`);
      }

      const at = order.indexOf(command.layer);
      const to = at - command.delta;
      [order[at], order[to]] = [order[to] as string, order[at] as string];
    }

    return order;
  }

  it('keeps their order above a row', () => {
    expect(play(dropManyCommands(flat, ['c', 'd'], 'a', 'above'))).toEqual(['c', 'd', 'a', 'b']);
  });

  it('keeps their order below a row', () => {
    expect(play(dropManyCommands(flat, ['a', 'b'], 'd', 'below'))).toEqual(['c', 'd', 'a', 'b']);
  });

  it('means nothing on a layer that is itself moving', () => {
    expect(dropManyCommands(flat, ['a', 'b'], 'b', 'below').length).toBeGreaterThanOrEqual(0);
    expect(dropManyCommands(flat, ['a'], 'a', 'below')).toEqual([]);
  });

  it('leaves what is inside a moving folder to the folder', () => {
    const rows: DragRow[] = [
      { id: 'folder', parent: null, folder: true },
      { id: 'in', parent: 'folder', folder: false },
      { id: 'last', parent: null, folder: false }
    ];

    expect(dropManyCommands(rows, ['folder', 'in'], 'last', 'below')).toEqual(
      dropCommands(rows, 'folder', 'last', 'below')
    );
  });
});

describe('moving the chosen layers from the keyboard', () => {
  it('moves one layer a place up or down', () => {
    expect(nudgeCommands(rows, new Set(['middle']), 1)).toEqual([up('middle')]);
    expect(nudgeCommands(rows, new Set(['middle']), -1)).toEqual([down('middle')]);
  });

  it('leaves a layer at the end it moves to, and the block stacked against it', () => {
    expect(nudgeCommands(rows, new Set(['top']), 1)).toEqual([]);
    expect(nudgeCommands(rows, new Set(['base']), -1)).toEqual([]);
    expect(nudgeCommands(rows, new Set(['middle', 'base']), -1)).toEqual([]);
    expect(nudgeCommands(rows, new Set(['top', 'middle']), 1)).toEqual([up('middle')]);
  });

  it('moves a block top first going up and bottom first going down', () => {
    expect(nudgeCommands(rows, new Set(['middle', 'base']), 1)).toEqual([up('middle'), up('base')]);
    expect(nudgeCommands(rows, new Set(['folder', 'middle']), -1)).toEqual([down('middle'), down('folder')]);
  });

  it('moves a layer among the siblings of its folder, and one inside a chosen folder with the folder', () => {
    expect(nudgeCommands(rows, new Set(['inner']), -1)).toEqual([down('inner')]);
    expect(nudgeCommands(rows, new Set(['folder', 'inner']), 1)).toEqual([up('folder')]);
  });
});
