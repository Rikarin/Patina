import type { GraphSettingDefinition } from '@patina/api-client';
import { describe, expect, it } from 'vitest';
import {
  aligned,
  controlOf,
  copiedWire,
  distributed,
  findMatches,
  flowOf,
  freePresetName,
  grouped,
  holds,
  movedWires,
  paletteOf,
  swapPairOf,
  swappedWires,
  titleOf,
  widgetsFor
} from './graph-authoring';

const box = (id: number, x: number, y: number, width = 100, height = 50) => ({ id, x, y, width, height });

function parameter(overrides: Partial<GraphSettingDefinition> = {}): GraphSettingDefinition {
  return {
    name: 'Wear',
    default: '0.5',
    summary: '',
    kind: 'Float',
    minimum: 0,
    maximum: 1,
    group: '',
    accepted: [],
    ...overrides
  };
}

describe('aligned', () => {
  const boxes = [box(1, 0, 0), box(2, 50, 100, 200), box(3, 30, 40, 40)];

  it('lines up left and right edges and leaves the other axis alone', () => {
    expect(aligned(boxes, 'left')).toEqual(
      new Map([
        [2, { x: 0, y: 100 }],
        [3, { x: 0, y: 40 }]
      ])
    );
    expect(aligned(boxes, 'right').get(1)).toEqual({ x: 150, y: 0 });
    expect(aligned(boxes, 'right').has(2)).toBe(false);
  });

  it('centres each box on the middle of the box round them all', () => {
    // From 0 to 250, so a 100-wide box starts at 75 and a 40-wide one at 105.
    expect(aligned(boxes, 'centre').get(1)).toEqual({ x: 75, y: 0 });
    expect(aligned(boxes, 'centre').get(3)).toEqual({ x: 105, y: 40 });
    expect(aligned(boxes, 'middle').get(1)).toEqual({ x: 0, y: 50 });
  });

  it('needs two boxes', () => {
    expect(aligned([box(1, 10, 10)], 'left').size).toBe(0);
  });
});

describe('distributed', () => {
  it('makes the gaps equal and keeps the ends', () => {
    const moved = distributed([box(1, 0, 0), box(2, 120, 9), box(3, 400, 0)], 'horizontal');
    // 0..500 holds 300 of boxes, so each of the two gaps is 100 and the middle box starts at 200.
    expect(moved).toEqual(new Map([[2, { x: 200, y: 9 }]]));
  });

  it('orders by position, not by selection', () => {
    const moved = distributed([box(3, 0, 300), box(1, 0, 0), box(2, 0, 20)], 'vertical');
    // 0..350 holds 150 of boxes, so each gap is 100 and the middle box starts at 50 + 100.
    expect(moved.get(2)).toEqual({ x: 0, y: 150 });
  });

  it('needs three boxes', () => {
    expect(distributed([box(1, 0, 0), box(2, 300, 0)], 'horizontal').size).toBe(0);
  });
});

describe('holds', () => {
  it('shows a parameter with no condition', () => {
    expect(holds(null, 'anything')).toBe(true);
  });

  it.each([
    ['true', 'true', false, true],
    ['TRUE', 'true', false, true],
    ['false', 'true', false, false],
    ['1.0', '1', false, true],
    ['2', '1', true, true],
    ['Gold', 'Gold', true, false],
    ['', '0', false, false]
  ])('compares %s with %s (negated %s) as Vixen does', (current, value, negated, shown) => {
    expect(holds({ setting: 'Other', value, negated }, current)).toBe(shown);
  });
});

describe('parameters', () => {
  it('are called by their label, or their name when it is blank', () => {
    expect(titleOf({ name: 'Wear', label: 'Wear amount' })).toBe('Wear amount');
    expect(titleOf({ name: 'Wear', label: '  ' })).toBe('Wear');
    expect(titleOf({ name: 'Wear' })).toBe('Wear');
  });

  it('are drawn by their widget and kind', () => {
    expect(controlOf(parameter())).toBe('slider');
    expect(controlOf(parameter({ maximum: null }))).toBe('number');
    expect(controlOf(parameter({ widget: 'Angle' }))).toBe('angle');
    expect(controlOf(parameter({ kind: 'Bool', widget: 'Toggle' }))).toBe('toggle');
    expect(controlOf(parameter({ kind: 'Bool' }))).toBe('switch');
    expect(controlOf(parameter({ kind: 'Text', accepted: ['A', 'B'] }))).toBe('choice');
    expect(controlOf(parameter({ kind: 'Text' }))).toBe('text');
  });

  it('are offered only the widgets the Server accepts for them', () => {
    expect(widgetsFor(parameter())).toEqual(['Default', 'Slider', 'Angle']);
    expect(widgetsFor(parameter({ minimum: null }))).toEqual(['Default', 'Angle']);
    expect(widgetsFor(parameter({ kind: 'Bool' }))).toEqual(['Default', 'Toggle']);
    expect(widgetsFor(parameter({ kind: 'Text' }))).toEqual(['Default']);
  });

  it('sit in their groups with the ungrouped first', () => {
    const groups = grouped([
      parameter({ name: 'A', group: 'Rust' }),
      parameter({ name: 'B' }),
      parameter({ name: 'C', group: 'Dirt' }),
      parameter({ name: 'D', group: 'Rust' })
    ]);
    expect(groups.map(group => [group.group, group.members.map(member => member.name)])).toEqual([
      ['', ['B']],
      ['Rust', ['A', 'D']],
      ['Dirt', ['C']]
    ]);
  });

  it('presets are named by the first free number', () => {
    expect(freePresetName([{ name: 'Preset 2' }])).toBe('Preset 3');
    expect(freePresetName([])).toBe('Preset 1');
  });
});

describe('flowOf', () => {
  // 1 → 2 → 3 → 4, and 5 → 3, with 6 → 7 off to one side.
  const wires = [
    { id: 'a', fromNode: 1, toNode: 2 },
    { id: 'b', fromNode: 2, toNode: 3 },
    { id: 'c', fromNode: 3, toNode: 4 },
    { id: 'd', fromNode: 5, toNode: 3 },
    { id: 'e', fromNode: 6, toNode: 7 }
  ];
  const ids = (selected: number[]) => [...flowOf(wires, new Set(selected))].map(wire => wire.id).sort();

  it('follows the picture back to every source and on to everything it ends up in', () => {
    expect(ids([3])).toEqual(['a', 'b', 'c', 'd']);
    expect(ids([2])).toEqual(['a', 'b', 'c']);
  });

  it('lights nothing with nothing selected, and nothing the selection does not touch', () => {
    expect(ids([])).toEqual([]);
    expect(ids([7])).toEqual(['e']);
  });

  it('stops at a loop rather than walking it for ever', () => {
    const loop = [
      { fromNode: 1, toNode: 2 },
      { fromNode: 2, toNode: 1 }
    ];
    expect(flowOf(loop, new Set([1])).size).toBe(2);
  });
});

describe('paletteOf', () => {
  const type = (path: string, category = 'Filters') => ({ path, category });
  const types = [type('Filters/Blur'), type('Colour/Blend', 'Colour'), type('My/Rust', '')];

  it('offers the starred, then the ones added last, then every one by category', () => {
    const lines = paletteOf(types, ['Colour/Blend'], ['Filters/Blur', 'Colour/Blend']);
    expect(lines.map(line => `${line.group}:${line.type.path}`)).toEqual([
      'Favourites:Colour/Blend',
      'Recent:Filters/Blur',
      'Recent:Colour/Blend',
      'Filters:Filters/Blur',
      'Colour:Colour/Blend',
      'Compounds:My/Rust'
    ]);
  });

  it('leaves out a favourite the list was not given, such as one a dropped wire cannot use', () => {
    expect(paletteOf([type('Filters/Blur')], ['Colour/Blend'], ['Gone/Node']).map(line => line.group)).toEqual([
      'Filters'
    ]);
  });
});

describe('findMatches', () => {
  const nodes = [{ title: 'Auto Levels' }, { title: 'Blur' }, { title: 'Levels' }, { title: 'Noise' }];

  it('keeps the titles that hold what was typed, those that start with it first', () => {
    expect(findMatches(nodes, 'lev').map(node => node.title)).toEqual(['Levels', 'Auto Levels']);
    expect(findMatches(nodes, '  NOISE ').map(node => node.title)).toEqual(['Noise']);
  });

  it('offers every node with nothing typed', () => {
    expect(findMatches(nodes, '')).toHaveLength(4);
  });
});

describe('rewiring', () => {
  const wire = (fromNode: number, fromPort: string, toNode: number, toPort: string) => ({
    fromNode,
    fromPort,
    toNode,
    toPort
  });
  // A noise (1) feeds a blend (3) and a levels (4); a gradient (2) feeds the blend's background.
  const wires = [wire(1, 'Out', 3, 'Foreground'), wire(1, 'Out', 4, 'Input'), wire(2, 'Out', 3, 'Background')];

  it('moves every wire leaving one output to leave another', () => {
    expect(movedWires(wires, { node: 1, port: 'Out' }, { node: 5, port: 'Out' })).toEqual([
      { op: 'connect', fromNode: 5, fromPort: 'Out', toNode: 3, toPort: 'Foreground' },
      { op: 'connect', fromNode: 5, fromPort: 'Out', toNode: 4, toPort: 'Input' }
    ]);
  });

  it('leaves a wire into the node the wires move to, which would loop, and does nothing onto itself', () => {
    expect(movedWires(wires, { node: 1, port: 'Out' }, { node: 4, port: 'Out' })).toEqual([
      { op: 'connect', fromNode: 4, fromPort: 'Out', toNode: 3, toPort: 'Foreground' }
    ]);
    expect(movedWires(wires, { node: 1, port: 'Out' }, { node: 1, port: 'Out' })).toEqual([]);
  });

  it('copies what feeds an input into another, and moves it when asked', () => {
    const from = { node: 3, port: 'Foreground' };
    const to = { node: 4, port: 'Input' };
    const connect = { op: 'connect', fromNode: 1, fromPort: 'Out', toNode: 4, toPort: 'Input' };
    expect(copiedWire(wires, from, to, false)).toEqual([connect]);
    expect(copiedWire(wires, from, to, true)).toEqual([connect, { op: 'disconnect', toNode: 3, toPort: 'Foreground' }]);
  });

  it('copies nothing from an input with no wire, onto itself, or back into what feeds it', () => {
    expect(copiedWire(wires, { node: 4, port: 'Mask' }, { node: 3, port: 'Mask' }, false)).toEqual([]);
    expect(copiedWire(wires, { node: 4, port: 'Input' }, { node: 4, port: 'Input' }, true)).toEqual([]);
    expect(copiedWire(wires, { node: 3, port: 'Background' }, { node: 2, port: 'Input' }, false)).toEqual([]);
  });

  it('swaps the wires into two inputs, finding both before either changes', () => {
    expect(swappedWires(wires, 3, 'Foreground', 'Background')).toEqual([
      { op: 'connect', fromNode: 2, fromPort: 'Out', toNode: 3, toPort: 'Foreground' },
      { op: 'connect', fromNode: 1, fromPort: 'Out', toNode: 3, toPort: 'Background' }
    ]);
  });

  it('swaps a wire with an empty input, and nothing with nothing', () => {
    expect(swappedWires(wires, 4, 'Input', 'Mask')).toEqual([
      { op: 'disconnect', toNode: 4, toPort: 'Input' },
      { op: 'connect', fromNode: 1, fromPort: 'Out', toNode: 4, toPort: 'Mask' }
    ]);
    expect(swappedWires(wires, 5, 'Input', 'Mask')).toEqual([]);
  });

  it('swaps the first two inputs of one kind, never a blend’s mask, and only when one is wired', () => {
    const blend = [
      { name: 'Foreground', family: 'Image' },
      { name: 'Background', family: 'Image' },
      { name: 'Mask', family: 'Image' },
      { name: 'Opacity', family: 'Float' }
    ];
    expect(swapPairOf(blend, new Set(['Background']))).toEqual(['Foreground', 'Background']);
    expect(swapPairOf(blend, new Set(['Mask']))).toBeNull();
    expect(swapPairOf(blend.slice(2), new Set(['Mask']))).toBeNull();
  });
});
