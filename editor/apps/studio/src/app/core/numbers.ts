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

const ByteUnits = ['bytes', 'KB', 'MB', 'GB', 'TB'] as const;

/** A size on disk, in the largest unit that keeps it at 1 or more: one place below 10, none above. */
export function bytes(count: number): string {
  let value = Math.max(0, count);
  let unit = 0;
  while (value >= 1024 && unit < ByteUnits.length - 1) {
    value /= 1024;
    unit += 1;
  }

  const shown = unit === 0 || value >= 10 ? Math.round(value).toString() : value.toFixed(1);
  return `${shown} ${ByteUnits[unit]}`;
}

/** A length in metres, in the largest of m, cm and mm that keeps it at 1 or more: one place below 10, none above. */
export function length(metres: number): string {
  const size = Math.abs(metres);
  const [value, unit] = size >= 1 ? [size, 'm'] : size >= 0.01 ? [size * 100, 'cm'] : [size * 1000, 'mm'];
  return `${value >= 10 || value === 0 ? Math.round(value).toString() : value.toFixed(1)} ${unit}`;
}

/** A count of things, with its thousands grouped. */
export function count(value: number): string {
  return Math.round(value).toLocaleString('en-US');
}
