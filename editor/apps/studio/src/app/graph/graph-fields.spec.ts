import type { GraphNodeType } from '@patina/api-client';
import { fieldsOf, hexOfLanes, isWireable, lanesOfHex, sliderRange } from './graph-fields';

const type: GraphNodeType = {
  path: 'Test/Node',
  title: 'Node',
  category: 'Test',
  summary: '',
  hasPreview: true,
  ports: [
    { name: 'Input', direction: 'Input', kind: 'Image', default: [], summary: '' },
    { name: 'Amount', direction: 'Input', kind: 'Float', default: [0.5], summary: '' },
    { name: 'Count', direction: 'Input', kind: 'Int', default: [4], summary: '' },
    { name: 'Flip', direction: 'Input', kind: 'Bool', default: [0], summary: '' },
    { name: 'Tint', direction: 'Input', kind: 'Float4', default: [1, 1, 1, 1], summary: '' },
    { name: 'Out', direction: 'Output', kind: 'Image', default: [], summary: '' }
  ],
  settings: [
    {
      name: 'Mode',
      default: 'A',
      summary: '',
      kind: 'Text',
      group: '',
      accepted: ['A', 'B']
    },
    { name: 'Path', default: '', summary: '', kind: 'Text', group: 'Files', accepted: [] },
    { name: 'edge', default: '0.5', summary: '', kind: 'Float', minimum: 0, maximum: 1, group: 'Files', accepted: [] },
    { name: 'on', default: 'true', summary: '', kind: 'Bool', group: 'Files', accepted: [] }
  ]
};

describe('graph fields', () => {
  it('picks a control for each input and setting, and none for an image', () => {
    const [main, files] = fieldsOf(type);

    expect(main?.fields.map(field => [field.name, field.control])).toEqual([
      ['Amount', 'number'],
      ['Count', 'number'],
      ['Flip', 'switch'],
      ['Tint', 'colour'],
      ['Mode', 'choice']
    ]);
    expect(files?.name).toBe('Files');
    expect(files?.fields.map(field => field.control)).toEqual(['text', 'number', 'switch']);
  });

  it('leaves out headings with nothing under them', () => {
    expect(fieldsOf({ ...type, ports: [], settings: [] })).toEqual([]);
  });

  it('offers wires only where an image can go', () => {
    expect(type.ports.filter(isWireable).map(port => port.name)).toEqual(['Input', 'Out']);
  });

  it('guesses a slider range around a default, and always reaches the value', () => {
    expect(sliderRange(0.5, 0.5)).toEqual({ min: 0, max: 2 });
    expect(sliderRange(8, 8)).toEqual({ min: 0, max: 32 });
    expect(sliderRange(100, 8).max).toBe(100);
    expect(sliderRange(-3, 0).min).toBe(-3);
  });

  it('takes a declared range over a guess', () => {
    expect(sliderRange(0.5, 0.5, { minimum: 0, maximum: 0.75 })).toEqual({ min: 0, max: 0.75 });
  });

  it("turns lanes into the picker's hex and back", () => {
    expect(hexOfLanes([1, 0, 0.5, 1])).toBe('#FF0080FF');
    expect(hexOfLanes([2, -1, 0])).toBe('#FF0000FF');
    expect(lanesOfHex('#ff0000')).toEqual([1, 0, 0, 1]);
    expect(lanesOfHex('#00ff0080')?.[3]).toBeCloseTo(0.502, 2);
    expect(lanesOfHex('red')).toBeNull();
  });
});
