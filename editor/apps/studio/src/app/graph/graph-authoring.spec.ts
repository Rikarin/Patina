import type { GraphSettingDefinition } from '@patina/api-client';
import { describe, expect, it } from 'vitest';
import { aligned, controlOf, distributed, freePresetName, grouped, holds, titleOf, widgetsFor } from './graph-authoring';

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
