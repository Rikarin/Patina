import { ApiError } from '@patina/api-client';
import { bytes, percent } from './numbers';

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
  'stack-not-open': 'That document is not open in the engine any more. Open it again from the Library.',
  'stroke-not-paint-layer':
    '{layer} is a {kind} layer, with nothing of its own to paint. Choose a paint layer, or paint its mask.',
  'stack-no-mesh': 'There is no mesh to paint on. Choose one in Project Settings.',
  'stroke-repeated-channel': 'That stroke could not be painted. Try it again.',
  'channel-missing': 'This texture set has no {channel} channel. It may have been removed by another client.',
  'channel-last': '{channel} is this texture set’s only channel, so it can’t be removed. Add another channel first.',
  'stroke-brush-refused': 'That brush can’t be used. {reason}',
  'stroke-canvas-larger':
    '{layer} holds {held} texels and the texture set is {size}. Painting it smaller would lose detail: set the size back to {held} to paint this layer.',
  'stroke-canvas-painted-at-size':
    '{layer} was painted at {held} earlier in this session. Save, close and open the document again to paint it at {size}.',
  'history-step-gone': 'That step is no longer in the history: an edit made since replaced it.',
  'anchor-taken': 'Another layer is already the anchor “{anchor}”. Choose another name.',
  'anchor-in-use': '{layer} reads the anchor “{anchor}”. Point it elsewhere before taking the anchor away.',
  'flatten-refused': '{layer} can’t be flattened. {reason}',
  'not-flattened': '{layer} isn’t flattened.',
  'bake-occluder-minimum':
    'The minimum occluder distance has to be less than the occluder distance, or every ray starts past its reach and the occlusion comes out white.',
  'mask-layer-missing': 'The mask of {layer} has changed since that was asked. Try again.',
  'mask-layer-not-paint':
    'That layer of the mask of {layer} is not painted, so a brush can’t reach it. Add a Paint layer to the mask.',
  'instance-source-missing': 'The layer to make an instance of is no longer in {set}.',
  'instance-cycle': 'An instance of {layer} there would stand for itself. Choose another layer.',
  'stack-layer-missing': 'That layer is no longer in the stack. It may have been removed by another client.',
  'node-unknown': '{node} isn’t a node or a compound this project has. It may have been renamed or removed.',
  'preset-invalid': 'That preset can’t be kept as it is. {reason}',
  'package-refused': 'This graph can’t be published as a package. {reason}',
  'preset-factory': '{preset} is a Starter preset, so it can’t be changed or deleted. Duplicate it to change a copy.',
  'preset-missing': 'There is no preset {preset} there any more. It may have been deleted outside Patina.',
  'export-unbuilt': 'The maps of this project are not built as they are now. Build them first, then look again.',
  'file-kind-unknown': 'The library lists pictures and meshes, and nothing else.',
  'history-empty': 'There is nothing to {what}.',
  'file-exists': 'There is a {file} there already. Choose another name.',
  'file-busy':
    '{file} is in use by another program, so it couldn’t be changed. Close it there, or wait a moment, and try again.',
  'file-missing': '{file} isn’t there any more. It may have been moved or deleted outside Patina.',
  'id-map-missing': 'This document has no ID map yet. Bake one, with ID among the maps.',
  'job-missing': 'That bake or export is no longer in Activity. The engine may have restarted since.',
  'graph-no-output': 'This graph has no Output node, so there is nothing to show on the mesh. Add one.',
  'mesh-layout-changed': 'Something moved on the new mesh, so reloading it moves paint. {reason}',
  'graph-template-unknown': 'There is no template {template} to start a graph from. Choose another in Start from.',
  'export-settings-invalid': 'Those export settings can’t be written. {reason}',
  'mesh-unnamed': 'Choose the mesh to reload first.',
  'uv-layout-undrawn': 'The UV layout could not be drawn. Try again once the GPU engine is running.',
  'selection-refused': 'A Color Selection picks out up to {most} colors. Remove some before adding another.',
  'selection-needs-picture':
    'Several colors are picked out of a mesh map or a picture, so this mask needs one as its source. Choose a mesh map or a picture first.',
  'library-conflict': 'Something in the library is in the way. {reason}',
  'library-refused': 'The library can’t do that. {reason}',
  'snapshot-unsaved': 'Save the document first, so the snapshot holds what you see.',
  'snapshot-document-open': 'Close the document first, so the snapshot isn’t written over.',
  'material-missing': 'That material isn’t in the library any more. It may have been deleted outside Patina.',
  'compound-needs-graph': 'A compound is copied beside the graph that uses it. Open that graph first.',
  'picture-unreadable': '{file} doesn’t read as a picture, so it wasn’t brought in. It may be damaged or cut short.'
};

const KindWords: Readonly<Record<string, string>> = { Fill: 'fill', Filter: 'filter', Group: 'folder', Paint: 'paint' };

/** This app's words for a refusal code, for a check the app makes itself before the Server would refuse. */
export function refusalWords(code: string, values: Readonly<Record<string, string>> = {}): string {
  return fill(Words[code] ?? code, values);
}

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

/**
 * What the app says for a problem a check before export finds (#159), by its code (`CheckCodes` in
 * engine/src/Patina.Contracts/CheckContracts.cs). Values the Server measured fill the braces, shares as percentages and
 * bytes in MB.
 */
const CheckWords: Readonly<Record<string, string>> = {
  'texture-over-budget': 'Takes {bytes} of GPU memory as {preset} writes it, over the budget of {budget}.',
  'texture-too-large': 'Is {width} × {height} texels, larger than the limit of {limit}.',
  'channel-missing': '{preset} reads {channel} into {file}, and this set has no {channel}, so a default fills it.',
  'density-low': 'Has {density} texels per unit, short of the {target} the project asks for.',
  'density-high': 'Has {density} texels per unit, more than the {target} the project asks for.',
  'bake-stale':
    'Its mesh maps were baked on another mesh, in another UV set than its UV {uvSet} or at another shape than its ' +
    'Width and Height, so what reads them lands in the wrong place. Bake it again.',
  'uv-outside': '{count} triangles reach past the first UV tile, which is the only one painted.',
  'uv-overlap': '{share} of the laid-out texels are under more than one triangle, so a stroke lands twice.',
  'uv-degenerate': '{count} triangles have no area in the UV layout, so no texel lands on them.',
  'uv-flipped': '{count} triangles are turned over in the UV layout, so a normal map reads them backwards.',
  'pbr-albedo-dark': 'In {share} of texels a surface that is not metal is darker than sRGB 30.',
  'pbr-albedo-bright': 'In {share} of texels a surface that is not metal is brighter than sRGB 240.',
  'pbr-metal-dark': 'In {share} of texels bare metal is darker than sRGB 180.',
  'pbr-metal-partial': 'In {share} of texels metalness is between 0.1 and 0.9, neither metal nor not.'
};

/** A check's measured number as a person reads it: shares as percentages, bytes in MB, the rest as given. */
function measured(name: string, value: string): string {
  const number = Number(value);
  if (!Number.isFinite(number)) {
    return value;
  }

  switch (name) {
    case 'share':
      return percent(number, number < 0.1 ? 1 : 0);
    case 'bytes':
    case 'budget':
      return bytes(number);
    default:
      return value;
  }
}

/** This app's words for a problem a check found, or the Server's sentence for a code it does not know. */
export function checkWords(code: string, values: Readonly<Record<string, string>>, detail: string): string {
  const words = CheckWords[code];
  if (words === undefined) {
    return detail;
  }

  return words.replace(/\{(\w+)\}/gu, (whole, name: string) => {
    const value = values[name];
    return value === undefined ? whole : measured(name, value);
  });
}
