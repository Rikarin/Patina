import { ApiError, type StackVocabulary } from '@patina/api-client';
import { colourName, hexColourName } from './colour-names';
import { blendName, choiceName, kindName, learnTerms, termName, wordsOf } from './labels';
import { asksForMenu } from './menu-keys';
import { checkWords, diagnosticOf, messageOf } from './messages';
import { amount, bytes, degrees, percent } from './numbers';

const term = (value: string, label: string) => ({ value, label });

describe('labels', () => {
  it('shows what the Server calls a word, once its vocabulary has come', () => {
    expect(blendName('Copy')).toBe('Copy');
    learnTerms({
      blends: [term('Copy', 'Normal'), term('ColourDodge', 'Color Dodge')],
      usages: [term('metalness', 'Metallic')],
      masks: [],
      fills: [],
      projections: [],
      generators: [term('Generators/Edge Wear', 'Edge Wear')],
      meshMaps: [term('ao', 'Ambient Occlusion')],
      alphas: []
    } as unknown as StackVocabulary);

    expect(blendName('Copy')).toBe('Normal');
    expect(termName('ColourDodge')).toBe('Color Dodge');
    expect(termName('metalness')).toBe('Metallic');
    expect(termName('ao')).toBe('Ambient Occlusion');
    expect(termName('Generators/Edge Wear')).toBe('Edge Wear');
  });

  it('never shows a word as code, even one nothing has labelled', () => {
    expect(wordsOf('Generators/Dirt')).toBe('Dirt');
    expect(wordsOf('hardLight')).toBe('Hard Light');
    expect(termName('SomethingNew')).toBe('Something New');
  });

  it('reads a choice by the labels its setting carries, and kinds by name', () => {
    const usage = { accepted: ['baseColor', 'metalness'], labels: ['Base Color', 'Metallic'] };
    expect(choiceName(usage, 'metalness')).toBe('Metallic');
    expect(choiceName({ accepted: ['x'] }, 'roughness')).toBe('Roughness');
    expect(kindName('Float2')).toBe('2 Numbers');
  });
});

describe('messages', () => {
  it('says a refusal it knows in its own words, with the names the Server sent', () => {
    const refused = new ApiError(422, 'Only a paint layer can be painted.', [], 'stroke-not-paint-layer', {
      layer: 'Rust',
      kind: 'Fill'
    });
    expect(messageOf(refused)).toBe(
      'Rust is a fill layer, with nothing of its own to paint. Choose a paint layer, or paint its mask.'
    );
  });

  it('has words for wave 4’s refusals, with the reason or the names the Server sent', () => {
    const say = (code: string, values: Record<string, string> = {}) =>
      messageOf(new ApiError(422, 'Server text.', [], code, values));
    expect(say('stroke-brush-refused', { reason: 'A brush has a radius.' })).toBe(
      'That brush can’t be used. A brush has a radius.'
    );
    expect(say('anchor-taken', { anchor: 'Ground' })).toBe(
      'Another layer is already the anchor “Ground”. Choose another name.'
    );
    expect(say('flatten-refused', { layer: 'Folder', reason: 'It passes through.' })).toBe(
      'Folder can’t be flattened. It passes through.'
    );
    expect(say('not-flattened', { layer: 'Folder' })).toBe('Folder isn’t flattened.');
    expect(say('history-step-gone', { step: '4' })).toBe(
      'That step is no longer in the history: an edit made since replaced it.'
    );
    expect(say('anchor-in-use', { anchor: 'Ground', layer: 'Rust' })).toBe(
      'Rust reads the anchor “Ground”. Point it elsewhere before taking the anchor away.'
    );
  });

  it('shows the Server’s text for a refusal it has no words for', () => {
    expect(messageOf(new ApiError(409, 'The file changed.', [], 'something-new'))).toBe('The file changed.');
    expect(messageOf(new Error('Plain.'))).toBe('Plain.');
  });

  it('keeps a diagnostic’s code and reasoning for its details', () => {
    expect(diagnosticOf("TG0002 'Input' has no image connected. There is no literal image.")).toEqual({
      summary: "'Input' has no image connected.",
      code: 'TG0002',
      detail: 'There is no literal image.'
    });
    expect(diagnosticOf('One sentence')).toEqual({ summary: 'One sentence', code: null, detail: '' });
  });
});

describe('check words', () => {
  it('says a problem in the app’s words, with shares as percentages and bytes as mebibytes', () => {
    expect(checkWords('pbr-albedo-dark', { share: '0.25' }, 'server')).toBe(
      'In 25% of texels a surface that is not metal is darker than sRGB 30.'
    );
    expect(checkWords('uv-overlap', { share: '0.004' }, 'server')).toMatch(/^0\.4% of the laid-out texels/u);
    expect(
      checkWords(
        'texture-over-budget',
        { bytes: String(12 * 1024 * 1024), budget: String(8 * 1024 * 1024), preset: 'Unreal' },
        ''
      )
    ).toBe('Takes 12.0 MiB of GPU memory as Unreal writes it, over the budget of 8.0 MiB.');
  });

  it('falls back to the Server’s sentence for a code it does not know', () => {
    expect(checkWords('something-new', { share: '1' }, 'The Server’s own sentence.')).toBe(
      'The Server’s own sentence.'
    );
  });
});

describe('numbers', () => {
  it('shows shares as percentages, angles in degrees and values to two places', () => {
    expect(percent(0.995)).toBe('100%');
    expect(percent(0.42)).toBe('42%');
    expect(degrees(Math.PI / 2)).toBe('90°');
    expect(amount(0.5)).toBe('0.50');
  });

  it('shows sizes on disk in the largest unit that keeps them at 1 or more', () => {
    expect(bytes(512)).toBe('512 bytes');
    expect(bytes(1536)).toBe('1.5 KB');
    expect(bytes(300 * 1024 * 1024)).toBe('300 MB');
    expect(bytes(2.25 * 1024 ** 3)).toBe('2.3 GB');
  });
});

describe('colour names', () => {
  it('names a swatch a screen reader can say', () => {
    expect(colourName([1, 0, 0])).toBe('red');
    expect(colourName([0, 0, 0.3])).toBe('dark blue');
    expect(colourName([0.5, 0.5, 0.5])).toBe('grey');
    expect(colourName([1, 1, 1])).toBe('white');
    expect(hexColourName('#46a758')).toBe('green');
  });
});

describe('menu keys', () => {
  it('opens a menu on the Menu key and Shift+F10, and on nothing else', () => {
    expect(asksForMenu(new KeyboardEvent('keydown', { key: 'ContextMenu' }))).toBe(true);
    expect(asksForMenu(new KeyboardEvent('keydown', { key: 'F10', shiftKey: true }))).toBe(true);
    expect(asksForMenu(new KeyboardEvent('keydown', { key: 'F10' }))).toBe(false);
  });
});
