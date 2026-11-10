/**
 * What each colour tag a layer can carry is drawn as (#106). The Server names the tags and their labels; these are only
 * the swatches, mid-tone so that each reads on the light skin and the dark one.
 */
export const TagSwatches: Readonly<Record<string, string>> = {
  Red: '#d1495b',
  Orange: '#e07a2f',
  Yellow: '#d9b334',
  Green: '#4f9d5d',
  Blue: '#3f7fc4',
  Violet: '#8a62c4',
  Grey: '#8a8f98'
};

/** A tag's swatch, or null for none. */
export function tagSwatch(tag: string | null | undefined): string | null {
  return (tag && TagSwatches[tag]) || null;
}
