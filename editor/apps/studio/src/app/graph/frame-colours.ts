/**
 * The colours a frame can take, as a graph file holds them: plain `#rrggbb`, which any client can draw (#225). They are
 * xUI's chart ramp as its light theme has it, in its order, so the studio shows each as that slot's token and a frame
 * follows the theme on screen while the file keeps one colour.
 */
export const FrameColours: readonly string[] = [
  '#2a78d6',
  '#eb6834',
  '#1baf7a',
  '#eda100',
  '#e87ba4',
  '#008300',
  '#4a3aa7',
  '#e34948'
];

const ChartSlots = FrameColours.length;

function slotOf(colour: string): number {
  return FrameColours.indexOf(colour.toLowerCase());
}

/**
 * What a frame is drawn in: one of the ramp's colours as its chart token, which follows the theme; any other colour,
 * such as one another client chose, as itself; and none as a slot picked from the frame's number.
 */
export function frameShown(colour: string, id: number): string {
  const slot = colour === '' ? id % ChartSlots : slotOf(colour);
  return slot >= 0 ? `var(--color-chart-${String(slot + 1)})` : colour;
}

/** What the colour picker calls a frame's colour: Auto for none, the ramp's by number, any other by its value. */
export function frameColourName(colour: string): string {
  if (colour === '') {
    return 'Auto';
  }

  const slot = slotOf(colour);
  return slot >= 0 ? `Color ${String(slot + 1)}` : colour;
}
