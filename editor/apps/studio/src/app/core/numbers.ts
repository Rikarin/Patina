/**
 * One way to show a number (#197). A share of something, such as an opacity, a flow or how much a mask lets through,
 * is a percentage with %. An angle is in degrees with °. A material's own value, such as roughness, stays 0 to 1 with
 * two places, as the graph and the exported map hold it. Every slider has one of these beside it.
 */
export function percent(share: number): string {
  return `${Math.round(share * 100)}%`;
}

export function degrees(radians: number): string {
  return `${Math.round(toDegrees(radians))}°`;
}

export function toRadians(degrees: number): number {
  return (degrees * Math.PI) / 180;
}

export function toDegrees(radians: number): number {
  return (radians * 180) / Math.PI;
}

/** A material's value, 0 to 1. */
export function amount(value: number): string {
  return value.toFixed(2);
}
