import { signal } from '@angular/core';
import type { StackTerm, StackVocabulary } from '@patina/api-client';

/**
 * What a person calls the words the engine uses. The Server sends a label beside every word of a stack's vocabulary
 * (#198), so the words a stack's fields take are learnt from it rather than listed here.
 */
const learnt = signal<ReadonlyMap<string, string>>(new Map());
/** The kinds of ports and parameters, kept apart because a kind's value, such as Image, may name other things too. */
const learntKinds = signal<ReadonlyMap<string, string>>(new Map());

/** Takes the labels a vocabulary carries, which every label below then reads. */
export function learnTerms(vocabulary: StackVocabulary): void {
  const lists: readonly (readonly StackTerm[])[] = [
    vocabulary.usages,
    vocabulary.blends,
    vocabulary.masks,
    vocabulary.fills,
    vocabulary.projections,
    vocabulary.generators,
    vocabulary.meshMaps,
    vocabulary.alphas
  ];
  learnt.set(new Map(lists.flat().map(term => [term.value, term.label] as const)));
  learntKinds.set(new Map((vocabulary.kinds ?? []).map(term => [term.value, term.label] as const)));
}

/**
 * Splits a word the engine spells as code into a person's words, for anything the Server has not labelled yet:
 * `Generators/Edge Wear` is Edge Wear, and `hardLight` is Hard Light.
 */
export function wordsOf(value: string): string {
  const name = value.slice(value.lastIndexOf('/') + 1).replace(/[-_]/gu, ' ');
  return name.replace(/([a-z])([A-Z])/gu, '$1 $2').replace(/(^|\s)\S/gu, letter => letter.toUpperCase());
}

/** What a person calls a word of a stack's vocabulary. */
export function termName(value: string): string {
  return learnt().get(value) ?? wordsOf(value);
}

/** The label of a value from a list the Server sent with its labels. */
export function labelIn(terms: readonly StackTerm[] | undefined, value: string): string {
  return terms?.find(term => term.value === value)?.label ?? termName(value);
}

const noTerms: readonly string[] = [];
const valueLists = new WeakMap<readonly StackTerm[], readonly string[]>();
const labelReaders = new WeakMap<readonly StackTerm[], (value: string) => string>();

/**
 * The values of a list the Server sent with its labels, for an `xui-select`'s `items`. The same list gives the same
 * array each time, so a template can ask for it on every check without the select starting over.
 */
export function valuesOf(terms: readonly StackTerm[] | undefined): readonly string[] {
  if (!terms) {
    return noTerms;
  }

  let values = valueLists.get(terms);
  if (!values) {
    values = terms.map(term => term.value);
    valueLists.set(terms, values);
  }

  return values;
}

/** What each value of {@link valuesOf} is called, for the same select's `itemText`: one reader for one list. */
export function labelsOf(terms: readonly StackTerm[] | undefined): (value: string) => string {
  if (!terms) {
    return termName;
  }

  let read = labelReaders.get(terms);
  if (!read) {
    read = value => labelIn(terms, value);
    labelReaders.set(terms, read);
  }

  return read;
}

/** What a setting's chosen value is called, from the labels its definition carries. */
export function choiceName(
  setting: { accepted: readonly string[]; labels?: readonly string[] | null },
  value: string
): string {
  const at = setting.accepted.indexOf(value);
  return (at >= 0 ? setting.labels?.[at] : undefined) ?? wordsOf(value);
}

const JobStates: Readonly<Record<string, string>> = {
  queued: 'Waiting',
  running: 'Working',
  done: 'Done',
  failed: 'Failed',
  cancelled: 'Cancelled'
};

const ImageFormats: Readonly<Record<string, string>> = {
  png8: 'PNG 8-bit',
  png16: 'PNG 16-bit',
  exr16: 'EXR 16-bit float',
  exr32: 'EXR 32-bit float'
};

export const channelName = termName;

export const blendName = termName;

export function jobStateName(state: string): string {
  return JobStates[state] ?? state;
}

export function imageFormatName(format: string): string {
  return ImageFormats[format] ?? format;
}

/** What a person calls the kind of a port or a parameter, from the Server's labels for them (#198). */
export function kindName(kind: string): string {
  return learntKinds().get(kind) ?? wordsOf(kind);
}

/** What each kind of document is called (ADR 0012). */
const DocumentKinds: Readonly<Record<string, string>> = {
  texturing: 'Texturing',
  'layered-material': 'Layered Material',
  'image-material': 'Image Material',
  graph: 'Graph',
  'brush-preset': 'Brush Preset'
};

/** What a person calls a kind of document or asset. */
export function documentKindName(kind: string): string {
  return DocumentKinds[kind] ?? wordsOf(kind);
}
