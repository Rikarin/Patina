import type { GraphNodeType } from '@patina/api-client';
import { pastedValues } from './graph-values';

const blur: GraphNodeType = {
  path: 'Filters/Blur',
  title: 'Blur',
  category: 'Filters',
  summary: '',
  hasPreview: true,
  ports: [
    { name: 'Input', direction: 'Input', kind: 'Image', default: [], summary: '', range: null },
    { name: 'Radius', direction: 'Input', kind: 'Float', default: [4], summary: '', range: null },
    { name: 'Tint', direction: 'Input', kind: 'Float4', default: [1, 1, 1, 1], summary: '', range: null },
    { name: 'Out', direction: 'Output', kind: 'Image', default: [], summary: '', range: null }
  ],
  settings: [
    {
      name: 'Quality',
      default: 'High',
      summary: '',
      kind: 'Text',
      minimum: null,
      maximum: null,
      group: '',
      accepted: []
    }
  ]
};

const node = (id: number, values: Record<string, number[]>, texts: Record<string, string>) => ({
  id,
  type: 'Filters/Blur',
  x: 0,
  y: 0,
  values,
  texts,
  bypassed: false
});

describe('pastedValues', () => {
  it('sets what was copied and puts back the defaults the copy left alone', () => {
    const copied = node(1, { Radius: [9] }, { Quality: 'Low', '=Tint': 'wear' });
    const target = node(2, { Tint: [0, 0, 0, 1] }, { '=Radius': 'size * 2' });

    expect(pastedValues(blur, copied, target)).toEqual([
      { op: 'setValue', node: 2, port: 'Radius', value: [9] },
      { op: 'setValue', node: 2, port: 'Tint', value: [1, 1, 1, 1] },
      { op: 'setText', node: 2, port: 'Quality', value: 'Low' },
      { op: 'setText', node: 2, port: '=Tint', value: 'wear' },
      { op: 'setText', node: 2, port: '=Radius', value: '' }
    ]);
  });

  it('sends nothing for a node that already matches, or one of another type', () => {
    const copied = node(1, { Radius: [9] }, {});

    expect(pastedValues(blur, copied, node(2, { Radius: [9] }, { Quality: 'High' }))).toEqual([]);
    expect(pastedValues(blur, copied, { ...node(3, {}, {}), type: 'Filters/Sharpen' })).toEqual([]);
  });
});
