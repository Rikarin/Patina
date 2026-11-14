import { elementsOf, sources } from './source-testing';

/** Where an element is, for a failure that names every one at once. */
const at = (path: string, line: number, what: string) => `${path}:${String(line)} ${what}`;

/** The bezel bars that are not a pane's own: the title bar, the status bar, and two badges over a picture. */
const notPaneBars = ['apps/studio/src/app/app.ts', 'paint/material-shelf.ts', 'paint/paint-atlas.ts'];

describe('pane bars (#200)', () => {
  it('gives every pane its bar through pane-bar, so bars side by side are one height', async () => {
    const found = (await sources())
      .filter(({ path }) => !notPaneBars.some(allowed => path.replaceAll('\\', '/').endsWith(allowed)))
      .flatMap(({ path, text }) =>
        text
          .split('\n')
          .map((line, index) => ({ line, index }))
          .filter(({ line }) => /\bchrome-bar\b/u.test(line))
          .map(({ index }) => at(path, index + 1, 'chrome-bar'))
      );

    expect(found).toEqual([]);
  });

  it('leaves a pane bar its height and says what each one is', async () => {
    const found = (await sources()).flatMap(({ path, text }) =>
      elementsOf(text, /[a-z]+/u)
        .filter(element => /class="[^"]*\bpane-bar\b/u.test(element.attributes))
        .filter(
          element =>
            /class="[^"]*\b(h|min-h|max-h|py|pt|pb)-[\w.]+/u.test(element.attributes) ||
            !/\brole="(toolbar|group)"/u.test(element.attributes) ||
            !/(?<![\w.[-])aria-label="[^"]+"/u.test(element.attributes)
        )
        .map(element => at(path, element.line, `<${element.tag}>`))
    );

    expect(found).toEqual([]);
  });
});
