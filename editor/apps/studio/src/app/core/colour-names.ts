/** Hue bands, each named by where it ends in degrees, the way a person would sort a swatch. */
const Hues: readonly (readonly [number, string])[] = [
  [15, 'red'],
  [40, 'orange'],
  [65, 'yellow'],
  [160, 'green'],
  [190, 'teal'],
  [250, 'blue'],
  [290, 'purple'],
  [335, 'pink'],
  [360, 'red']
];

/**
 * What a colour is called, near enough to tell swatches apart by ear: "dark blue", "light grey". A screen reader has
 * nothing to say about a hex code.
 *
 * @param colour Red, green and blue from 0 to 1.
 */
export function colourName(colour: readonly number[]): string {
  const [r = 0, g = 0, b = 0] = colour.map(lane => Math.min(1, Math.max(0, lane)));
  const high = Math.max(r, g, b);
  const low = Math.min(r, g, b);
  const lightness = (high + low) / 2;
  const chroma = high - low;
  if (chroma < 0.08) {
    return lightness < 0.12 ? 'black' : lightness > 0.9 ? 'white' : `${shade(lightness)}grey`.trim();
  }

  const hue = hueOf(r, g, b, high, chroma);
  const name = Hues.find(([end]) => hue < end)?.[1] ?? 'red';
  if (name === 'orange' && lightness < 0.35) {
    return 'brown';
  }

  return `${shade(lightness)}${name}`;
}

function hueOf(r: number, g: number, b: number, high: number, chroma: number): number {
  const sector = high === r ? ((g - b) / chroma + 6) % 6 : high === g ? (b - r) / chroma + 2 : (r - g) / chroma + 4;
  return sector * 60;
}

function shade(lightness: number): string {
  return lightness < 0.3 ? 'dark ' : lightness > 0.75 ? 'light ' : '';
}

/** A colour written as #rrggbb, named. */
export function hexColourName(hex: string): string {
  const lanes = /^#?([\da-f]{2})([\da-f]{2})([\da-f]{2})$/iu.exec(hex);
  return lanes ? colourName(lanes.slice(1, 4).map(lane => Number.parseInt(lane, 16) / 255)) : hex;
}
