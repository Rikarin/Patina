import { sources } from './source-testing';

/**
 * Headings drawn by the bezel skin, not by hand (#13, #195): a group heading inside a pane or a dialog is the skin's
 * `section-label`, so every pane's headings share one size, weight, spacing and color.
 */
describe('the bezel skin’s headings', () => {
  it('draws a group heading with section-label, never with its own uppercase or muted small bold text', async () => {
    const handMade = [
      /class="(?![^"]*section-label)[^"]*\buppercase\b[^"]*"/u,
      /class="(?![^"]*section-label)(?=[^"]*\btext-foreground-muted\b)(?=[^"]*\btext-xs\b)(?=[^"]*\bfont-semibold\b)[^"]*"/u
    ];
    const found = (await sources()).flatMap(({ path, text }) =>
      text
        .split('\n')
        .map((code, index) => ({ code, line: index + 1 }))
        .filter(({ code }) => handMade.some(pattern => pattern.test(code)))
        .map(({ line }) => `${path.replaceAll('\\', '/')}:${String(line)}`)
    );

    expect(found).toEqual([]);
  });
});
