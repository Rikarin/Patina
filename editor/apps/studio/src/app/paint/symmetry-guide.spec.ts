import type { StackView } from '@patina/api-client';
import { project, symmetryCopies, symmetryGuide } from './symmetry-guide';

/** Straight down the z axis at the origin from four units, the picture 200 square and two units across at the origin. */
const Down: StackView = {
  eye: [0, 0, 4],
  right: [1, 0, 0],
  up: [0, 1, 0],
  forward: [0, 0, -1],
  verticalFieldOfView: 2 * Math.atan(0.25),
  width: 200,
  height: 200
};

const columns = (points: string | null) => (points ?? '').split(' ').map(point => Number(point.split(',')[0]));

describe('symmetryGuide', () => {
  it('projects as the Worker does: the middle of the picture, and a unit right of it a quarter across', () => {
    expect(project(Down, [0, 0, 0])).toEqual([100, 100]);
    expect(project(Down, [1, 0, 0])?.[0]).toBeCloseTo(200);
    expect(project(Down, [0, 0, 5])).toBeNull();
  });

  it('draws a mirror across the model where its centre is, not where its origin is', () => {
    const offCentre = [0.2, -0.2, -0.2, 0.6, 0.2, 0.2];
    const centred = symmetryGuide({ kind: 'mirror', axis: 'x', centre: 'model', offset: 0, count: 2 }, Down, offCentre);
    const origin = symmetryGuide({ kind: 'mirror', axis: 'x', centre: 'origin', offset: 0, count: 2 }, Down, offCentre);

    expect(columns(centred)).toHaveLength(4);
    // x = 0.4 is the box's middle, about 140 across at its depths; the origin's plane is the middle column exactly.
    expect(columns(centred).every(column => Math.abs(column - 140) < 5)).toBe(true);
    expect(columns(origin).every(column => Math.abs(column - 100) < 0.5)).toBe(true);
  });

  it('moves a mirror along its axis by a share of the bounding radius, and draws a radial symmetry as its axis', () => {
    const box = [-1, -1, -1, 1, 1, 1];
    const moved = symmetryGuide({ kind: 'mirror', axis: 'x', centre: 'model', offset: 0.5, count: 2 }, Down, box);
    const radial = symmetryGuide({ kind: 'radial', axis: 'z', centre: 'model', offset: 0.5, count: 6 }, Down, box);

    expect(moved).not.toBeNull();
    expect(columns(moved).every(column => column > 130)).toBe(true);
    // An axis seen end on is two points over the middle of the picture, nearer and further.
    expect(columns(radial)).toHaveLength(2);
    expect(columns(radial).every(column => Math.abs(column - 100) < 0.5)).toBe(true);
  });
});

describe('symmetryCopies', () => {
  const box = [0, 0, 0, 2, 2, 2];
  const close = (actual: readonly number[] | undefined, expected: readonly number[]) =>
    expected.forEach((value, at) => expect(actual?.[at]).toBeCloseTo(value, 5));

  it('mirrors a point across the plane through the model centre, moved by its offset', () => {
    const [mirrored] = symmetryCopies(
      { kind: 'mirror', axis: 'x', centre: 'model', offset: 0, count: 2 },
      box,
      [0.25, 0.5, 1.5]
    );
    close(mirrored, [1.75, 0.5, 1.5]);

    // A share of the bounding radius, which is √3 for this box.
    const moved = symmetryCopies(
      { kind: 'mirror', axis: 'y', centre: 'origin', offset: 0.5, count: 2 },
      box,
      [1, 0, 1]
    );
    close(moved[0], [1, Math.sqrt(3), 1]);
  });

  it('turns a point round the axis into every other copy, as many as the count less the stroke', () => {
    const copies = symmetryCopies(
      { kind: 'radial', axis: 'z', centre: 'model', offset: 0.9, count: 4 },
      box,
      [2, 1, 0.5]
    );
    expect(copies).toHaveLength(3);
    // About z through (1, 1): a quarter, a half and three quarters of a turn, the height along the axis kept.
    const sorted = [...copies].sort((a, b) => a[0] - b[0] || a[1] - b[1]);
    close(sorted[0], [0, 1, 0.5]);
    close(sorted[1], [1, 0, 0.5]);
    close(sorted[2], [1, 2, 0.5]);
  });
});
