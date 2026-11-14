/**
 * How what a click or a marquee reaches on the model joins the meshes a layer is kept to (#110). Replace keeps only
 * what was reached, Add takes it in, Remove takes it out, and Toggle, a plain click's, flips the one mesh reached.
 */
export type PartCombine = 'replace' | 'add' | 'remove' | 'toggle';

/** The combine modes a marquee offers, in the order the meshes list shows them. */
export const PartCombines: readonly { value: Exclude<PartCombine, 'toggle'>; label: string }[] = [
  { value: 'replace', label: 'Replace' },
  { value: 'add', label: 'Add' },
  { value: 'remove', label: 'Remove' }
];

/** A drag shorter than this, in pixels, is a click and not a marquee. */
export const MarqueeSlack = 4;

/**
 * The meshes a layer keeps after a pick: every name in the model's order, or none for the whole model. Null when the
 * pick would leave no mesh at all, which the Server reads as the whole model, so the layer stays as it is, as the list
 * keeps its last mesh.
 *
 * @param all The model's named meshes.
 * @param held The meshes the layer is kept to, or null or none for every one.
 * @param reached What the click or the marquee reached.
 */
export function combinedParts(
  all: readonly string[],
  held: readonly string[] | null | undefined,
  reached: readonly string[],
  combine: PartCombine
): string[] | null {
  const now = new Set(held?.length ? held : all);
  const picked = new Set(reached.filter(part => all.includes(part)));
  const keeps = (part: string): boolean => {
    switch (combine) {
      case 'replace':
        return picked.has(part);
      case 'add':
        return now.has(part) || picked.has(part);
      case 'remove':
        return now.has(part) && !picked.has(part);
      case 'toggle':
        return picked.has(part) ? !now.has(part) : now.has(part);
    }
  };
  const kept = all.filter(keeps);
  if (kept.length === 0) {
    return null;
  }

  return kept.length === all.length ? [] : kept;
}

/** What a modifier held down asks of a pick: Shift adds, Ctrl, Cmd or Alt removes, and otherwise the default. */
export function combineOf(
  event: { shiftKey: boolean; ctrlKey: boolean; metaKey: boolean; altKey: boolean },
  otherwise: PartCombine
): PartCombine {
  if (event.shiftKey) {
    return 'add';
  }

  return event.ctrlKey || event.metaKey || event.altKey ? 'remove' : otherwise;
}
