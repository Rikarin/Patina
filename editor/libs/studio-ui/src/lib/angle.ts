/** The angle in degrees, from 0 to 360, that a point makes with the centre: 0 points right, and it turns anticlockwise. */
export function angleOf(x: number, y: number): number {
  const degrees = (Math.atan2(-y, x) * 180) / Math.PI;
  // Plus zero turns the -0 that atan2 gives on the right into 0.
  return degrees < 0 ? degrees + 360 : degrees + 0;
}

/** An angle snapped to a step, still within 0 to 360. */
export function snapAngle(degrees: number, step: number): number {
  const snapped = step > 0 ? Math.round(degrees / step) * step : degrees;
  return snapped >= 360 ? snapped - 360 : snapped;
}
