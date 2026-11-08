import { type Chord, normalChord, platformChord } from './chords';
import {
  type CommandArea,
  type CommandSpec,
  DefaultPreset,
  FixedKeys,
  KeyPresets,
  overlaps,
  presetOf,
  reachesFocus,
  reservedKeys
} from './command-table';

/**
 * A person's keys as the desktop app keeps them, in `keys.json` in its settings folder: per user, not per library, and
 * plain text a person can read. Only what differs from the preset is written, so a new default reaches everyone who did
 * not change that command.
 */
export interface KeySettings {
  version: 1;
  /** One of {@link KeyPresets}. */
  preset: string;
  /** Command id to its keys, for the commands rebound. An empty list is a command with no key. */
  keys: Record<string, string[]>;
}

export const NoKeySettings: KeySettings = { version: 1, preset: DefaultPreset, keys: {} };

/** Settings as read from a file a person may have edited: anything that is not a command or a chord is dropped. */
export function readKeySettings(value: unknown, table: readonly CommandSpec[]): KeySettings {
  if (typeof value !== 'object' || value === null) {
    return NoKeySettings;
  }

  const held = value as { preset?: unknown; keys?: unknown };
  const preset = typeof held.preset === 'string' && held.preset in KeyPresets ? held.preset : DefaultPreset;
  const keys: Record<string, string[]> = {};
  const given = typeof held.keys === 'object' && held.keys !== null ? (held.keys as Record<string, unknown>) : {};
  for (const spec of table) {
    const chords = given[spec.id];
    if (Array.isArray(chords)) {
      keys[spec.id] = chords.flatMap(chord => (typeof chord === 'string' ? (normalChord(chord) ?? []) : []));
    }
  }

  return { version: 1, preset, keys };
}

/** What holds a key that no command can take from it: the app menu's own item, or a control's key, and where. */
export interface KeyHolder {
  readonly label: string;
  readonly where: string;
}

/**
 * What holds a chord that `spec` may not have, or null: an item of the app menu, which takes its key before the window
 * sees it, or a key a control answers itself while its keys would work there, unless that key does this very command.
 */
export function heldBy(spec: CommandSpec, chord: Chord, mac: boolean): KeyHolder | null {
  const reserved = reservedKeys(mac).find(key => key.chord === chord);
  if (reserved) {
    return { label: reserved.label, where: 'the app menu' };
  }

  const fixed = FixedKeys.find(
    key =>
      key.command !== spec.id &&
      reachesFocus(spec, key.focus) &&
      key.chords.some(held => platformChord(held, mac) === chord)
  );
  return fixed ? { label: fixed.label, where: fixed.section.toLowerCase() } : null;
}

/**
 * Each command's keys: a person's own first, then the preset's, then the table's. A key two commands would share where
 * both can be live goes to the first that claims it, a person's own before a default, so a hand-edited file can never
 * make a command unreachable without saying which one won. A key the app menu or a control holds ({@link heldBy}) goes
 * to none.
 */
export function settleKeys(table: readonly CommandSpec[], settings: KeySettings, mac: boolean): Map<string, Chord[]> {
  const preset = presetOf(settings.preset);
  const settled = new Map<string, Chord[]>(table.map(spec => [spec.id, []]));
  const claimed: { chord: Chord; area: CommandArea }[] = [];
  const claim = (spec: CommandSpec, chords: readonly Chord[]) => {
    for (const chord of chords.map(held => platformChord(held, mac))) {
      const area = spec.area ?? 'app';
      if (
        heldBy(spec, chord, mac) === null &&
        !claimed.some(other => other.chord === chord && overlaps(other.area, area))
      ) {
        claimed.push({ chord, area });
        settled.get(spec.id)?.push(chord);
      }
    }
  };

  for (const spec of table) {
    const own = settings.keys[spec.id];
    if (own) {
      claim(spec, own);
    }
  }

  for (const spec of table) {
    if (!settings.keys[spec.id]) {
      claim(spec, preset.keys[spec.id] ?? spec.keys ?? []);
    }
  }

  return settled;
}

/** The command that already answers a chord where `id`'s keys work, or null if the chord is free there. */
export function takenBy(
  table: readonly CommandSpec[],
  keys: ReadonlyMap<string, readonly Chord[]>,
  id: string,
  chord: Chord
): CommandSpec | null {
  const area = table.find(spec => spec.id === id)?.area ?? 'app';
  return (
    table.find(
      spec => spec.id !== id && overlaps(spec.area ?? 'app', area) && (keys.get(spec.id) ?? []).includes(chord)
    ) ?? null
  );
}
