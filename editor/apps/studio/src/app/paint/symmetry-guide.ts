import type { StackSymmetry, StackView } from '@patina/api-client';

type Point = readonly [number, number, number];

const Axes: Readonly<Record<string, number>> = { x: 0, y: 1, z: 2 };

/** How far past the model's box the drawn plane reaches, as a share of the box, so its edge reads as a plane's. */
const Margin = 0.06;

/**
 * Where a point of the mesh lands on a picture, as Vixen's `PaintView.Project` puts it, or null behind the eye.
 */
export function project(view: StackView, point: Point): [number, number] | null {
  const offset = [0, 1, 2].map(axis => (point[axis] ?? 0) - (view.eye[axis] ?? 0));
  const dot = (direction: readonly number[]) => offset.reduce((sum, value, axis) => sum + value * (direction[axis] ?? 0), 0);
  const depth = dot(view.forward);
  if (depth <= 1e-6) {
    return null;
  }

  const tangent = Math.tan(view.verticalFieldOfView / 2);
  return [
    (dot(view.right) / ((depth * tangent * view.width) / view.height) + 1) * 0.5 * view.width,
    (1 - dot(view.up) / (depth * tangent)) * 0.5 * view.height
  ];
}

/**
 * What to draw of a symmetry over the viewport: a mirror's plane as the rectangle where it cuts the model's box, or a
 * radial symmetry's axis as a line through the box, as SVG points on the picture the view was picked for. The plane or
 * the axis goes where the Worker puts it: through the box's centre or the origin, a mirror moved along its axis by a
 * share of the bounding radius. Null when any of it is behind the eye.
 *
 * @param bounds The model's box, its least corner then its greatest: six numbers in the mesh's own space.
 */
export function symmetryGuide(symmetry: StackSymmetry, view: StackView, bounds: readonly number[]): string | null {
  const axis = Axes[symmetry.axis] ?? 0;
  const least = [bounds[0] ?? 0, bounds[1] ?? 0, bounds[2] ?? 0];
  const most = [bounds[3] ?? 0, bounds[4] ?? 0, bounds[5] ?? 0];
  const middle = least.map((low, at) => (low + (most[at] ?? 0)) / 2);
  const radius = Math.hypot(...most.map((high, at) => high - (least[at] ?? 0))) / 2;
  const centre = symmetry.centre === 'origin' ? [0, 0, 0] : middle;
  const through = centre.map((value, at) => value + (at === axis && symmetry.kind !== 'radial' ? symmetry.offset * radius : 0));
  const grow = (low: number, high: number) => [low - (high - low) * Margin, high + (high - low) * Margin] as const;
  const point = (along: number, first: number, second: number): Point => {
    const made = [0, 0, 0];
    made[axis] = along;
    made[(axis + 1) % 3] = first;
    made[(axis + 2) % 3] = second;
    return [made[0] ?? 0, made[1] ?? 0, made[2] ?? 0];
  };

  const [firstLow, firstHigh] = grow(least[(axis + 1) % 3] ?? 0, most[(axis + 1) % 3] ?? 0);
  const [secondLow, secondHigh] = grow(least[(axis + 2) % 3] ?? 0, most[(axis + 2) % 3] ?? 0);
  const at = through[axis] ?? 0;
  const corners: Point[] =
    symmetry.kind === 'radial'
      ? [
          point(grow(least[axis] ?? 0, most[axis] ?? 0)[0], through[(axis + 1) % 3] ?? 0, through[(axis + 2) % 3] ?? 0),
          point(grow(least[axis] ?? 0, most[axis] ?? 0)[1], through[(axis + 1) % 3] ?? 0, through[(axis + 2) % 3] ?? 0)
        ]
      : [
          point(at, firstLow, secondLow),
          point(at, firstHigh, secondLow),
          point(at, firstHigh, secondHigh),
          point(at, firstLow, secondHigh)
        ];
  const shown = corners.map(corner => project(view, corner));
  return shown.every(found => found !== null)
    ? shown.map(([column, row]) => `${column.toFixed(1)},${row.toFixed(1)}`).join(' ')
    : null;
}
