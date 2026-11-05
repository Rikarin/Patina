/** One key of a curve, both axes from zero to one. */
export interface CurveKey {
  x: number;
  y: number;
}

export type CurveChannel = 'r' | 'g' | 'b' | 'a';

/** A curve per channel of a picture. */
export type Curves = Readonly<Record<CurveChannel, readonly CurveKey[]>>;

const Scheme = 'curve:';
const Channels: readonly CurveChannel[] = ['r', 'g', 'b', 'a'];
const Precision = 1000;

export const straightKeys: readonly CurveKey[] = [
  { x: 0, y: 0 },
  { x: 1, y: 1 }
];

export const straightCurves: Curves = { r: straightKeys, g: straightKeys, b: straightKeys, a: straightKeys };

function isChannel(text: string): text is CurveChannel {
  return (Channels as readonly string[]).includes(text);
}

function keysOf(text: string): readonly CurveKey[] | null {
  const keys: CurveKey[] = [];
  for (const pair of text.split(',')) {
    const [x, y, extra] = pair.trim().split(/\s+/);
    if (x === undefined || y === undefined || extra !== undefined || !isFinite(Number(x)) || !isFinite(Number(y))) {
      return null;
    }

    keys.push({ x: Number(x), y: Number(y) });
  }

  return keys.sort((a, b) => a.x - b.x);
}

/**
 * The keys a Curve input holds: `curve:r=0 0,1 1;g=...`, a channel left out being straight. An empty text is the
 * identity, which is what the node makes of it too. Null for anything else, such as a reference to a file.
 */
export function parseCurves(text: string): Curves | null {
  const trimmed = text.trim();
  if (trimmed === '') {
    return straightCurves;
  }

  if (!trimmed.startsWith(Scheme)) {
    return null;
  }

  const curves = { ...straightCurves };
  for (const part of trimmed.slice(Scheme.length).split(';')) {
    const split = part.indexOf('=');
    const channel = part.slice(0, split).trim();
    const keys = split < 0 ? null : keysOf(part.slice(split + 1));
    if (!isChannel(channel) || keys === null) {
      return null;
    }

    curves[channel] = keys;
  }

  return curves;
}

function numberOf(value: number): string {
  return String(Math.round(value * Precision) / Precision);
}

function isStraight(keys: readonly CurveKey[]): boolean {
  return keys.length === 2 && keys[0]?.x === 0 && keys[0].y === 0 && keys[1]?.x === 1 && keys[1].y === 1;
}

/** The text for {@link parseCurves}: empty when nothing is curved, so that the file says nothing. */
export function formatCurves(curves: Curves): string {
  const parts = Channels.filter(channel => !isStraight(curves[channel])).map(
    channel => `${channel}=${curves[channel].map(key => `${numberOf(key.x)} ${numberOf(key.y)}`).join(',')}`
  );
  return parts.length === 0 ? '' : `${Scheme}${parts.join(';')}`;
}

function chord(from: CurveKey, to: CurveKey): number {
  const span = to.x - from.x;
  return span <= 0 ? 0 : (to.y - from.y) / span;
}

function slope(keys: readonly CurveKey[], index: number): number {
  const key = keys[index];
  const before = keys[index - 1];
  const after = keys[index + 1];
  if (!key || (!before && !after)) {
    return 0;
  }

  if (!before) {
    return after ? chord(key, after) : 0;
  }

  return after ? (chord(before, key) + chord(key, after)) / 2 : chord(before, key);
}

/**
 * The curve at a point, for drawing it: the same automatic Hermite as Vixen's `CurveEvaluation`, which is what bakes the
 * table. The picture comes from that bake, so a difference here could only misdraw the line.
 */
export function evaluateCurve(keys: readonly CurveKey[], x: number): number {
  const first = keys[0];
  const last = keys.at(-1);
  if (!first || !last) {
    return x;
  }

  if (x <= first.x) {
    return first.y;
  }

  if (x >= last.x) {
    return last.y;
  }

  const index = keys.findIndex(key => key.x > x) - 1;
  const from = keys[index];
  const to = keys[index + 1];
  if (!from || !to) {
    return x;
  }

  const span = to.x - from.x;
  if (span <= 0) {
    return from.y;
  }

  const t = (x - from.x) / span;
  const t2 = t * t;
  const t3 = t2 * t;
  return (
    (2 * t3 - 3 * t2 + 1) * from.y +
    (t3 - 2 * t2 + t) * slope(keys, index) * span +
    (-2 * t3 + 3 * t2) * to.y +
    (t3 - t2) * slope(keys, index + 1) * span
  );
}
