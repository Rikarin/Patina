/**
 * One way to show a number (#197). A share of something, such as an opacity, a flow or how much a mask lets through,
 * is a percentage with %. An angle is in degrees with °. A material's own value, such as roughness, stays 0 to 1 with
 * two places, as the graph and the exported map hold it. Every slider has one of these beside it.
 */
export function percent(share: number, places = 0): string {
  return `${places === 0 ? toPercent(share).toString() : (share * 100).toFixed(places)}%`;
}

/** A share as the number a 0 to 100 slider, field or progress bar holds, rounded to whole percents or to `places`. */
export function toPercent(share: number, places = 0): number {
  const scale = 10 ** places;
  return Math.round(share * 100 * scale) / scale;
}

/** A number from a 0 to 100 slider or field, as the share it stands for. */
export function fromPercent(percent: number): number {
  return percent / 100;
}

/** An angle the settings already keep in degrees. */
export function angle(degrees: number): string {
  return `${Math.round(degrees)}°`;
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
export function amount(value: number, places = 2): string {
  return value.toFixed(places);
}

/** A number with no unit of its own, such as a length in model units: two places below 10, none above. */
export function figure(value: number): string {
  return value.toFixed(Math.abs(value) >= 10 ? 0 : 2);
}

/** A multiple, such as a stretch: 1.5×. */
export function times(value: number, places = 1): string {
  return `${value.toFixed(places)}×`;
}

/** A time in whole milliseconds. */
export function milliseconds(value: number): string {
  return `${Math.round(value).toString()} ms`;
}

/** A short time, as a GPU's: hundredths of a millisecond below one, tenths below ten, then whole. */
export function brief(value: number): string {
  if (value < 0.01) {
    return '< 0.01 ms';
  }

  const places = value < 1 ? 2 : value < 10 ? 1 : 0;
  return `${value.toFixed(places)} ms`;
}

/** An exposure in photographic stops, signed. */
export function stops(value: number): string {
  return `${value > 0 ? '+' : ''}${value.toFixed(1)} stops`;
}

/** A signal-to-noise ratio in decibels, or Lossless when nothing was lost. */
export function decibels(value: number | null | undefined): string {
  return value === null || value === undefined ? 'Lossless' : `${value.toFixed(1)} dB`;
}

/** An error from 0 to 1 in 8-bit levels, which is how far off a person reads it: one place below 10. */
export function levels(error: number): string {
  const level = error * 255;
  return `${level.toFixed(level < 10 ? 1 : 0)} / 255`;
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

/** What a setting from the Server says of itself that decides how its number is shown and slid. */
interface Bounds {
  readonly kind: string;
  readonly minimum?: number | null;
  readonly maximum?: number | null;
  readonly widget?: string | null;
}

/**
 * The range a setting slides over, when it has both ends: whole steps for a whole number, a hundredth of the range for
 * the rest. Null for a setting with an open end, which keeps a number field.
 */
export function knobRange(setting: Bounds): { minimum: number; maximum: number; step: number } | null {
  const { minimum, maximum } = setting;
  if (setting.kind !== 'Float' && setting.kind !== 'Int') {
    return null;
  }

  if (minimum === null || minimum === undefined || maximum === null || maximum === undefined || maximum <= minimum) {
    return null;
  }

  return { minimum, maximum, step: setting.kind === 'Int' ? 1 : (maximum - minimum) / 100 };
}

/** A setting's number beside its slider: an angle in degrees, a share of 0 to 1 as a percentage, a count whole. */
export function knobShown(setting: Bounds, value: number): string {
  if (setting.widget === 'Angle') {
    return angle(value);
  }

  if (setting.kind === 'Int') {
    return count(value);
  }

  return setting.minimum === 0 && setting.maximum === 1 ? percent(value) : amount(value);
}
