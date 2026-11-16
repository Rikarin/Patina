/** Two numbers, an x and a y, as a Float2 parameter holds them. */
export interface Position {
  x: number;
  y: number;
}

/** The range a position is placed within: each lane from `minimum` to `maximum`. */
export interface PositionRange {
  minimum: number;
  maximum: number;
}

/**
 * The position a point on a pad stands for: across is x from the range's bottom to its top, and down is y the same
 * way, so the pad reads as a texture does, its origin at the top left. A point outside the pad is held at its edge.
 */
export function positionOf(across: number, down: number, range: PositionRange): Position {
  const span = range.maximum - range.minimum;
  const clamp = (share: number) => Math.min(1, Math.max(0, share));
  return { x: range.minimum + clamp(across) * span, y: range.minimum + clamp(down) * span };
}

/** Where on a pad a position lies, as shares of its width and height, held at the edges. */
export function shareOf(position: Position, range: PositionRange): Position {
  const span = range.maximum - range.minimum;
  const clamp = (value: number) => (span > 0 ? Math.min(1, Math.max(0, (value - range.minimum) / span)) : 0);
  return { x: clamp(position.x), y: clamp(position.y) };
}

/** A position nudged by a step on one lane, held in its range, as a key moves it. */
export function nudged(position: Position, lane: 'x' | 'y', step: number, range: PositionRange): Position {
  const moved = Math.min(range.maximum, Math.max(range.minimum, position[lane] + step));
  return { ...position, [lane]: moved };
}
