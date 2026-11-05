/** One stop of a colour ramp. */
export interface RampStop {
  /** Along the ramp, from zero to one. */
  position: number;
  /** `RRGGBBAA`, in upper case, without a hash. */
  colour: string;
}

const Scheme = 'ramp:';
const Precision = 1000;
const Hex = /^[0-9a-f]{8}$/i;

export const blackToWhite: readonly RampStop[] = [
  { position: 0, colour: '000000FF' },
  { position: 1, colour: 'FFFFFFFF' }
];

/**
 * The stops a Ramp input holds: `ramp:0 000000ff,1 ffffffff`. An empty text is black to white, which is what the node
 * makes of it too. Null for anything else, such as a reference to a file.
 */
export function parseRamp(text: string): readonly RampStop[] | null {
  const trimmed = text.trim();
  if (trimmed === '') {
    return blackToWhite;
  }

  if (!trimmed.startsWith(Scheme)) {
    return null;
  }

  const stops: RampStop[] = [];
  for (const stop of trimmed.slice(Scheme.length).split(',')) {
    const [position, colour, extra] = stop.trim().split(/\s+/);
    if (colour === undefined || extra !== undefined || !Hex.test(colour) || !isFinite(Number(position))) {
      return null;
    }

    stops.push({ position: Number(position), colour: colour.toUpperCase() });
  }

  return stops.sort((a, b) => a.position - b.position);
}

/** The text for {@link parseRamp}. */
export function formatRamp(stops: readonly RampStop[]): string {
  const parts = [...stops]
    .sort((a, b) => a.position - b.position)
    .map(stop => `${String(Math.round(stop.position * Precision) / Precision)} ${stop.colour.toLowerCase()}`);
  return `${Scheme}${parts.join(',')}`;
}

function channel(colour: string, index: number): number {
  return parseInt(colour.slice(index * 2, index * 2 + 2), 16);
}

/** The colour between two neighbouring stops, mixed in the stored values as the bake mixes them. */
export function colourAt(stops: readonly RampStop[], position: number): string {
  const after = stops.findIndex(stop => stop.position >= position);
  const to = stops[after === -1 ? stops.length - 1 : after];
  const from = stops[after <= 0 ? -1 : after - 1];
  if (!to) {
    return 'FFFFFFFF';
  }

  if (!from) {
    return to.colour;
  }

  const span = to.position - from.position;
  const t = span > 0 ? (position - from.position) / span : 1;
  return [0, 1, 2, 3]
    .map(index => {
      const mixed = Math.round(channel(from.colour, index) * (1 - t) + channel(to.colour, index) * t);
      return mixed.toString(16).padStart(2, '0');
    })
    .join('')
    .toUpperCase();
}

/** A CSS gradient that shows a ramp. */
export function cssOfRamp(stops: readonly RampStop[]): string {
  const parts = [...stops]
    .sort((a, b) => a.position - b.position)
    .map(stop => `#${stop.colour} ${String(stop.position * 100)}%`);
  return `linear-gradient(to right, ${parts.join(', ')})`;
}
