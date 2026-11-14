import type { ViewportDisplay } from '@patina/api-client';
import type { DisplayStart } from '../core/preferences';

/**
 * What a display made one's own leaves out: what is shown rather than how, which belongs to the project in front.
 */
const ShownNow: ReadonlySet<string> = new Set(['view', 'picture', 'pictureLane', 'pictureScale', 'focus']);

/** The settings of a display a person keeps as their own start (#189): how the model is lit, shown and finished. */
export function startOf(display: ViewportDisplay): DisplayStart {
  return Object.fromEntries(Object.entries(display).filter(([key]) => !ShownNow.has(key)));
}

/**
 * How the viewport opens in a project that kept no display of its own: Patina's, under what the person made their own.
 * A setting the file holds that Patina's has not, or of another kind, is left out: a person may have edited it.
 */
export function displayStart(patina: ViewportDisplay, own: DisplayStart | null): ViewportDisplay {
  const base = patina as unknown as Record<string, unknown>;
  const start: Record<string, unknown> = { ...base };
  for (const [key, value] of Object.entries(own ?? {})) {
    const held = base[key];
    const fits = held === null ? value === null || typeof value === 'string' : typeof value === typeof held;
    if (key in base && !ShownNow.has(key) && fits) {
      start[key] = value;
    }
  }

  return start as unknown as ViewportDisplay;
}
