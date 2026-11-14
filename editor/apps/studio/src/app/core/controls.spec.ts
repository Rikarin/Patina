import { elementsOf, sources } from './source-testing';

/** Where an element is, for a failure that names every one at once. */
const at = (path: string, line: number, what: string) => `${path}:${String(line)} ${what}`;

describe('controls written into the code', () => {
  it('uses xUI’s controls, never a native select, checkbox, color, range or number field (#195)', async () => {
    const native = /^\s*type="(checkbox|radio|color|range)"/mu;
    const found = (await sources()).flatMap(({ path, text }) => [
      ...elementsOf(text, /select/u).map(element => at(path, element.line, '<select>')),
      ...elementsOf(text, /input/u)
        .filter(element => native.test(element.attributes))
        .map(element => at(path, element.line, `<input ${native.exec(element.attributes)?.[1] ?? ''}>`)),
      // A number field is xUI's numeric input, or an xUI input where a value is kept when it is done being typed.
      ...elementsOf(text, /input/u)
        .filter(element => element.attributes.includes('type="number"') && !/\bxuiInput\b/u.test(element.attributes))
        .map(element => at(path, element.line, '<input number>'))
    ]);

    expect(found).toEqual([]);
  });

  it('shows progress with xUI’s progress bar, never a hand-made one (#195)', async () => {
    const found = (await sources()).flatMap(({ path, text }) =>
      elementsOf(text, /[a-z]+/u)
        .filter(element => element.tag !== 'xui-progress-bar' && /\brole="progressbar"/u.test(element.attributes))
        .map(element => at(path, element.line, `<${element.tag} role=progressbar>`))
    );

    expect(found).toEqual([]);
  });

  it('labels an xUI control through its aria-label input, which ariaLabel never reaches (#195)', async () => {
    const found = (await sources()).flatMap(({ path, text }) =>
      elementsOf(text, /xui-[a-z-]+/u)
        .filter(element => /(\[ariaLabel\]|(?<![\w.[])ariaLabel)=/u.test(element.attributes))
        .map(element => at(path, element.line, `<${element.tag}>`))
    );

    expect(found).toEqual([]);
  });

  it('gives every button that names itself to a screen reader a tooltip that says the same (#190)', async () => {
    const found = (await sources()).flatMap(({ path, text }) =>
      elementsOf(text, /button/u)
        .filter(
          element =>
            /(\[attr\.aria-label\]|\[aria-label\]|(?<![\w.[-])aria-label)=/u.test(element.attributes) &&
            !/(\[title\]|(?<![\w.[-])title)=/u.test(element.attributes)
        )
        .map(element => at(path, element.line, '<button>'))
    );

    expect(found).toEqual([]);
  });
});
