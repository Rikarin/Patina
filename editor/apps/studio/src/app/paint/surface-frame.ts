import type { StackDecal, StackPathPoint, StackSurfacePath, StackView, ViewportPick } from '@patina/api-client';
import { project } from './symmetry-guide';

export type Vector = readonly [number, number, number];

/** A decal's box as the kernel squares it: unit axes, its picture's right and up, and the way it faces. */
export interface DecalFrame {
  centre: Vector;
  right: Vector;
  up: Vector;
  forward: Vector;
}

/** The share of the model's size (its bounds' diagonal) a decal placed with a click starts at. */
const DecalShare = 0.15;

/** The share of the model's size a path drawn with clicks starts at, as its half width. */
const PathShare = 0.012;

const dot = (a: Vector, b: Vector): number => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
const scaled = (a: Vector, by: number): Vector => [a[0] * by, a[1] * by, a[2] * by];
const plus = (a: Vector, b: Vector): Vector => [a[0] + b[0], a[1] + b[1], a[2] + b[2]];
const minus = (a: Vector, b: Vector): Vector => [a[0] - b[0], a[1] - b[1], a[2] - b[2]];
const cross = (a: Vector, b: Vector): Vector => [
  a[1] * b[2] - a[2] * b[1],
  a[2] * b[0] - a[0] * b[2],
  a[0] * b[1] - a[1] * b[0]
];
const length = (a: Vector): number => Math.hypot(a[0], a[1], a[2]);
const unit = (a: Vector): Vector => {
  const reach = length(a);
  return reach > 0 ? scaled(a, 1 / reach) : [0, 0, 0];
};

export const vector = (numbers: readonly number[] | null | undefined): Vector => [
  numbers?.[0] ?? 0,
  numbers?.[1] ?? 0,
  numbers?.[2] ?? 0
];

/** How big the model is: its bounds' diagonal, or one with no bounds. */
export function modelSize(bounds: readonly number[] | null | undefined): number {
  if (!bounds || bounds.length < 6) {
    return 1;
  }

  return (
    Math.hypot(
      (bounds[3] ?? 0) - (bounds[0] ?? 0),
      (bounds[4] ?? 0) - (bounds[1] ?? 0),
      (bounds[5] ?? 0) - (bounds[2] ?? 0)
    ) || 1
  );
}

/**
 * A decal's box squared as Vixen's `SurfaceFrames.Decal` squares it, so what is drawn is where the picture lands: up is
 * made square to the facing, and one along it falls back to a world axis.
 */
export function decalFrame(decal: StackDecal): DecalFrame {
  const forward = unit(vector(decal.facing));
  const up = vector(decal.up);
  let squared = minus(up, scaled(forward, dot(up, forward)));
  if (!(dot(squared, squared) > 1e-8)) {
    const axis: Vector = Math.abs(forward[1]) < 0.9 ? [0, 1, 0] : [0, 0, -1];
    squared = minus(axis, scaled(forward, dot(axis, forward)));
  }

  const upward = unit(squared);
  return { centre: vector(decal.centre), right: cross(upward, forward), up: upward, forward };
}

/** A place on the decal's square, 0 to 1 across and down, in the model's space. */
export function onDecal(decal: StackDecal, frame: DecalFrame, s: number, t: number): Vector {
  return plus(
    plus(frame.centre, scaled(frame.right, (s - 0.5) * decal.width)),
    scaled(frame.up, (0.5 - t) * decal.height)
  );
}

/** The grid a warp leaves alone. */
export const Unwarped: readonly number[] = [0, 0, 0.5, 0, 1, 0, 0, 0.5, 0.5, 0.5, 1, 0.5, 0, 1, 0.5, 1, 1, 1];

/** A decal placed where a pick met the model, facing out of the face it met, its picture upright to the camera. */
export function placedDecal(hit: ViewportPick, held: StackDecal | null | undefined): StackDecal {
  const size = held?.width ?? modelSize(hit.bounds) * DecalShare;
  return {
    angle: 1.3,
    fade: 0.25,
    tiled: false,
    sampling: 'Bilinear',
    warp: [],
    ...held,
    centre: [...vector(hit.position)],
    facing: [...vector(hit.normal ?? hit.view.forward.map(lane => -lane))],
    up: [...vector(hit.view.up)],
    width: size,
    height: held?.height ?? size,
    depth: held?.depth ?? size,
    box: [...(hit.bounds ?? [])]
  };
}

/** The channel a picture dropped on the mesh as a decal colours: the base color where the set has one (#133). */
export function decalChannel(usages: readonly string[]): string {
  return usages.find(usage => usage === 'baseColor') ?? usages[0] ?? 'baseColor';
}

/**
 * Whether a file is a picture a layer can read, by its extension against the formats the engine reads (with or
 * without their dot). With no list, the engine has not said, and any file is let through for it to refuse.
 */
export function isPicture(path: string, formats: readonly string[] | null | undefined): boolean {
  const extension = /\.([^./\\]+)$/u.exec(path)?.[1]?.toLowerCase();
  if (!extension) {
    return false;
  }

  return !formats?.length || formats.some(format => format.replace(/^\./u, '').toLowerCase() === extension);
}

/** A file's name without its folder or its extension, for the layer a dropped picture makes. */
export function pictureName(path: string): string {
  const file = path.split(/[\\/]/u).pop() ?? path;
  return file.replace(/\.[^.]+$/u, '') || file;
}

/** A path point where a pick met the model. */
export function pathPoint(hit: ViewportPick): StackPathPoint {
  return { position: [...vector(hit.position)], normal: [...vector(hit.normal)], pressure: 1 };
}

/** A path with one more point at its end, made the first time a point is placed. */
export function extendedPath(hit: ViewportPick, held: StackSurfacePath | null | undefined): StackSurfacePath {
  return {
    hardness: 0.5,
    ribbon: false,
    repeat: 0,
    angle: 1.3,
    radius: modelSize(hit.bounds) * PathShare,
    ...held,
    points: [...(held?.points ?? []), pathPoint(hit)],
    box: [...(hit.bounds ?? held?.box ?? [])]
  };
}

/** Up turned about the facing by an angle, in radians, counter-clockwise seen from the side the decal faces. */
export function turned(decal: StackDecal, angle: number): StackDecal {
  const { up, right } = decalFrame(decal);
  const [cos, sin] = [Math.cos(angle), Math.sin(angle)];
  return { ...decal, up: [...plus(scaled(up, cos), scaled(right, -sin))] };
}

/** The decal's square on a picture, its four corners from the top left, or null where any is behind the eye. */
export type Corner = [number, number];

/** A decal's square on a picture, from its top left round to its bottom left. */
export type Quad = [Corner, Corner, Corner, Corner];

export function decalOutline(decal: StackDecal, view: StackView): Quad | null {
  const frame = decalFrame(decal);
  const corners = (
    [
      [0, 0],
      [1, 0],
      [1, 1],
      [0, 1]
    ] as const
  ).map(([s, t]) => project(view, onDecal(decal, frame, s, t)));
  return corners.every(corner => corner !== null) ? (corners as Quad) : null;
}

/** Where the decal's warp grid is on a picture, nine points row by row, or null where any is behind the eye. */
export function warpGrid(decal: StackDecal, view: StackView): [number, number][] | null {
  const frame = decalFrame(decal);
  const grid = decal.warp?.length === Unwarped.length ? decal.warp : Unwarped;
  const points = Array.from({ length: 9 }, (_, at) =>
    project(view, onDecal(decal, frame, grid[at * 2] ?? 0, grid[at * 2 + 1] ?? 0))
  );
  return points.every(point => point !== null) ? points : null;
}

/** How many places Warp to Geometry measures along each line across the decal's square (#134). */
export const GeometrySamples = 9;

/** Where Warp to Geometry measures the surface: three rows across the square, then three columns down it. */
export function geometryLines(samples = GeometrySamples): { s: number; t: number }[][] {
  const along = Array.from({ length: samples }, (_, at) => at / (samples - 1));
  return [...[0, 0.5, 1].map(t => along.map(s => ({ s, t }))), ...[0, 0.5, 1].map(s => along.map(t => ({ s, t })))];
}

/**
 * Whether a place a pick found on the mesh is the surface under the decal: inside its box's depth and facing it within
 * its angle. A pick that met something in front of or behind the decal says nothing about its surface.
 */
export function underDecal(decal: StackDecal, at: Vector, normal: Vector | null): boolean {
  const frame = decalFrame(decal);
  const depth = Math.abs(dot(minus(at, frame.centre), frame.forward));
  const facing = normal ? dot(unit(normal), frame.forward) : 1;
  return depth <= decal.depth / 2 && facing >= Math.cos(decal.angle);
}

/**
 * The share of a line across the square at which half its length along the surface is reached: places measured evenly
 * across the square, null where the surface was not found, which counts as flat for its step.
 */
export function halfwayAlong(places: readonly (Vector | null)[], step: number): number {
  const lengths = places.slice(1).map((place, at) => {
    const before = places[at];
    return place && before ? length(minus(place, before)) : step;
  });
  const total = lengths.reduce((sum, value) => sum + value, 0);
  if (!(total > 0) || lengths.length === 0) {
    return 0.5;
  }

  let walked = 0;
  for (const [at, value] of lengths.entries()) {
    if (walked + value >= total / 2 && value > 0) {
      return (at + (total / 2 - walked) / value) / lengths.length;
    }

    walked += value;
  }

  return 0.5;
}

/**
 * Warp to Geometry (#134): the warp that spreads the picture evenly along the surface under the decal rather than evenly
 * across its flat square, so a picture over a curve is not stretched where the surface turns away. The picture's edges
 * stay on the square's; its middle row and column move to where half the surface's length is reached. `lines` are the
 * places {@link geometryLines} asked for, in its order.
 */
export function geometryWarp(decal: StackDecal, lines: readonly (readonly (Vector | null)[])[]): number[] {
  const warp = [...Unwarped];
  for (let row = 0; row < 3; row++) {
    const places = lines[row] ?? [];
    warp[(row * 3 + 1) * 2] = halfwayAlong(places, decal.width / Math.max(places.length - 1, 1));
  }

  for (let column = 0; column < 3; column++) {
    const places = lines[3 + column] ?? [];
    warp[(3 + column) * 2 + 1] = halfwayAlong(places, decal.height / Math.max(places.length - 1, 1));
  }

  return warp;
}

/**
 * Where a place on a picture lands on the decal's plane, 0 to 1 across and down its square, by the camera's ray: a
 * warp point is dragged in the decal's own plane, so it needs no pick.
 */
export function onDecalPlane(decal: StackDecal, view: StackView, x: number, y: number): [number, number] | null {
  const frame = decalFrame(decal);
  const tangent = Math.tan(view.verticalFieldOfView / 2);
  const across = ((x / view.width) * 2 - 1) * tangent * (view.width / view.height);
  const down = (1 - (y / view.height) * 2) * tangent;
  const ray = plus(plus(vector(view.forward), scaled(vector(view.right), across)), scaled(vector(view.up), down));
  const eye = vector(view.eye);
  const facing = dot(ray, frame.forward);
  if (Math.abs(facing) < 1e-9) {
    return null;
  }

  const distance = dot(minus(frame.centre, eye), frame.forward) / facing;
  if (!(distance > 0)) {
    return null;
  }

  const offset = minus(plus(eye, scaled(ray, distance)), frame.centre);
  return [0.5 + dot(offset, frame.right) / decal.width, 0.5 - dot(offset, frame.up) / decal.height];
}

/** A path's points on a picture, or null for any behind the eye. */
export function pathOutline(path: StackSurfacePath, view: StackView): ([number, number] | null)[] {
  return path.points.map(point => project(view, vector(point.position)));
}
