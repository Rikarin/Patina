import { describe, expect, it } from 'vitest';
import { combineOf, combinedParts } from './mesh-selection';

const all = ['lid', 'box', 'hinge'];

describe('combinedParts (#110)', () => {
  it('replaces, adds, removes and toggles in the model order', () => {
    expect(combinedParts(all, ['lid'], ['hinge', 'box'], 'replace')).toEqual(['box', 'hinge']);
    expect(combinedParts(all, ['hinge'], ['lid'], 'add')).toEqual(['lid', 'hinge']);
    expect(combinedParts(all, ['lid', 'box'], ['lid'], 'remove')).toEqual(['box']);
    expect(combinedParts(all, ['lid', 'box'], ['box'], 'toggle')).toEqual(['lid']);
    expect(combinedParts(all, ['lid'], ['box'], 'toggle')).toEqual(['lid', 'box']);
  });

  it('reads no meshes as every one, and gives every one back as none', () => {
    expect(combinedParts(all, null, ['lid'], 'remove')).toEqual(['box', 'hinge']);
    expect(combinedParts(all, [], ['lid'], 'toggle')).toEqual(['box', 'hinge']);
    expect(combinedParts(all, ['lid', 'box'], ['hinge'], 'add')).toEqual([]);
    expect(combinedParts(all, ['lid'], all, 'replace')).toEqual([]);
  });

  it('leaves the layer as it is rather than keep it to nothing, and ignores names the model lacks', () => {
    expect(combinedParts(all, ['lid'], ['lid'], 'remove')).toBeNull();
    expect(combinedParts(all, ['lid'], [], 'replace')).toBeNull();
    expect(combinedParts(all, ['lid'], ['gone'], 'replace')).toBeNull();
    expect(combinedParts(all, ['lid'], ['gone'], 'add')).toEqual(['lid']);
  });
});

describe('combineOf', () => {
  const keys = { shiftKey: false, ctrlKey: false, metaKey: false, altKey: false };

  it('adds with Shift, removes with Ctrl, Cmd or Alt, and otherwise keeps the mode chosen', () => {
    expect(combineOf(keys, 'replace')).toBe('replace');
    expect(combineOf({ ...keys, shiftKey: true }, 'replace')).toBe('add');
    expect(combineOf({ ...keys, ctrlKey: true }, 'toggle')).toBe('remove');
    expect(combineOf({ ...keys, metaKey: true }, 'toggle')).toBe('remove');
    expect(combineOf({ ...keys, altKey: true }, 'add')).toBe('remove');
  });
});
