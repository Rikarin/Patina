import { sources } from './source-testing';

/** Where a line is, for a failure that names every one at once. */
const at = (path: string, line: number, what: string) => `${path}:${String(line)} ${what}`;

/** The sources, with their paths written the same way on every system. */
async function files(): Promise<{ path: string; text: string }[]> {
  return (await sources()).map(({ path, text }) => ({ path: path.replaceAll('\\', '/'), text }));
}

/** Each line of a file, with its number, for sweeps that read what the code says line by line. */
function linesOf(text: string): { line: number; text: string }[] {
  return text.split('\n').map((line, index) => ({ line: index + 1, text: line }));
}

/**
 * Files that write numbers no person reads as numbers: points of an SVG path, a CSS position, the geometry of a dial.
 * Each keeps its arithmetic, since the formatters in core/numbers.ts are for what a person reads.
 */
const Drawings = new Set([
  'apps/studio/src/app/paint/surface-guide.ts',
  'apps/studio/src/app/paint/symmetry-guide.ts',
  'libs/studio-ui/src/lib/angle-dial.ts',
  'libs/studio-ui/src/lib/angle.ts',
  'libs/studio-ui/src/lib/curve-editor.ts',
  'libs/studio-ui/src/lib/gradient-ramp.ts',
  'libs/studio-ui/src/lib/ramp.ts'
]);

describe('one way to show a number (#197)', () => {
  it('formats every number a person reads through core/numbers.ts, never inline', async () => {
    const inline: [RegExp, string][] = [
      [/\.toFixed\(/u, 'toFixed'],
      [/\*\s*100\b/u, 'a share times 100'],
      [/\}\}?\s*[%°]/u, 'a unit written after an interpolation'],
      [/180\s*\)?\s*\/\s*Math\.PI|Math\.PI\s*\)?\s*\/\s*180/u, 'degrees converted by hand']
    ];
    const found = (await files())
      .filter(({ path }) => !path.endsWith('core/numbers.ts') && !Drawings.has(path))
      .flatMap(({ path, text }) =>
        linesOf(text).flatMap(({ line, text: code }) =>
          inline.filter(([pattern]) => pattern.test(code)).map(([, what]) => at(path, line, what))
        )
      );

    expect(found).toEqual([]);
  });
});

/**
 * Words docs/02-glossary.md keeps out of what a person reads, with the word it uses instead. Code may keep the old
 * identifiers; a template, a label, a title, a hint or a summary may not.
 */
const NotWords: readonly [RegExp, string][] = [
  [/\bColours?\b|\bcolours?\b/u, 'Color'],
  [/\bResolution\b/u, 'Size'],
  [/\bMaterial Graph\b/iu, 'Graph'],
  [/\bbaked maps?\b/iu, 'mesh map'],
  [/\bshared assets?\b/iu, 'material'],
  [/\bsub-?graphs?\b/iu, 'compound'],
  [/\bfactory\b/iu, 'Starter'],
  [/\bWorker\b|\bRenderer\b/u, 'the GPU engine'],
  [/\bServer\b|\bBackend\b/u, 'the engine'],
  [/\bstacks?\b/iu, 'document, or layers']
];

/** The text of each template, and of each label, title, hint, summary and text a table gives a person. */
function shownText(text: string): { line: number; text: string }[] {
  const shown: { line: number; text: string }[] = [];
  for (const template of text.matchAll(/template: `([^`]*)`/gu)) {
    const first = text.slice(0, template.index).split('\n').length;
    // Code a person never reads is left out: interpolations, bindings, test ids, control flow and element names. Each
    // keeps its line, so a failure names the right one.
    const blank = (code: string) => code.replace(/[^\n]/gu, '');
    const body = (template[1] ?? '')
      .replace(/\{\{[^}]*\}\}/gu, blank)
      .replace(/\s(?:data-testid|class|\[[^\]]+\]|\([^)]+\)|\*?ng\w*|#\w+)="[^"]*"/gu, blank)
      .replace(/@(?:if|for|switch|case|else|default|empty)\b[^\n]*/gu, '')
      .replace(/<\/?[\w-]+/gu, '<');
    shown.push(...linesOf(body).map(({ line, text: words }) => ({ line: first + line - 1, text: words })));
  }

  for (const field of text.matchAll(/\b(?:label|title|hint|summary|text):\s*'([^']*)'/gu)) {
    shown.push({ line: text.slice(0, field.index).split('\n').length, text: field[1] ?? '' });
  }

  return shown;
}

describe('one word for each thing (#196)', () => {
  it('uses the glossary’s word in every template and every label a table gives', async () => {
    const found = (await files()).flatMap(({ path, text }) =>
      shownText(text).flatMap(({ line, text: words }) =>
        NotWords.filter(([pattern]) => pattern.test(words)).map(([, word]) =>
          at(path, line, `“${words.trim().slice(0, 60)}” says what the glossary calls ${word}`)
        )
      )
    );

    expect(found).toEqual([]);
  });
});
