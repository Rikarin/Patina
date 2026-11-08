import { ApiError } from '@patina/api-client';

/**
 * What the app says for a refusal the Server sends with a code (engine/src/Patina.Server/Api/Refusals.cs). The Server's
 * own text stays neutral for every client; these are this app's words for the ones a person meets (#198). Values the
 * Server names fill the braces.
 */
const Words: Readonly<Record<string, string>> = {
  'document-unsaved': 'This document has unsaved changes. Save it first, or close it and choose Don’t Save.',
  'renderer-missing': 'The GPU engine is not running yet, so nothing could be drawn. It starts again on its own.',
  'library-none': 'No library is open. Open one, or make a new one, from the start page.',
  'graph-not-open': 'That graph was closed elsewhere. Open it again from the Library.',
  'stroke-not-paint-layer': '{layer} is a {kind} layer, with nothing of its own to paint. Choose a paint layer, or paint its mask.',
  'stack-no-mesh': 'There is no mesh to paint on. Choose one in Project Settings.',
  'stroke-repeated-channel': 'That stroke could not be painted. Try it again.',
  'anchor-taken': 'Another layer is already the anchor “{anchor}”. Choose another name.',
  'anchor-in-use': '{layer} reads the anchor “{anchor}”. Point it elsewhere before taking the anchor away.',
  'flatten-refused': '{layer} can’t be flattened. {reason}',
  'not-flattened': '{layer} isn’t flattened.'
};

const KindWords: Readonly<Record<string, string>> = { Fill: 'fill', Filter: 'filter', Group: 'folder', Paint: 'paint' };

/** What to tell a person about a failure: this app's words for a refusal it knows, and the Server's text otherwise. */
export function messageOf(failure: unknown): string {
  if (failure instanceof ApiError && failure.code && Words[failure.code]) {
    return fill(Words[failure.code] ?? '', failure.values);
  }

  return failure instanceof Error ? failure.message : String(failure);
}

function fill(text: string, values: Readonly<Record<string, string>>): string {
  return text.replace(/\{(\w+)\}/gu, (whole, name: string) => {
    const value = values[name];
    if (value === undefined) {
      return whole;
    }

    return name === 'kind' ? (KindWords[value] ?? value.toLowerCase()) : value;
  });
}

/**
 * A compiler diagnostic as a person reads it: its first sentence, with its code (`TG0002`) and the rest kept for
 * whoever opens the details. A message with no code is all summary.
 */
export function diagnosticOf(text: string): { summary: string; code: string | null; detail: string } {
  const coded = /^([A-Z]{2,3}\d{3,4})[:\s]\s*(.*)$/su.exec(text.trim());
  const body = coded?.[2] ?? text.trim();
  const end = body.search(/[.!?](\s|$)/u);
  const summary = end >= 0 ? body.slice(0, end + 1) : body;
  return { summary, code: coded?.[1] ?? null, detail: body.slice(summary.length).trim() };
}
