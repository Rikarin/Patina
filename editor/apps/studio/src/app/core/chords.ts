/**
 * Key chords as the command table writes them: modifiers in a fixed order, then one key, joined with `+`.
 *
 * - `Mod` is the platform's command key: ⌘ on a Mac, Ctrl elsewhere. `Ctrl` is the Mac's Control key, and on other
 *   platforms means the same as `Mod`, which is how the table says Ctrl on both.
 * - A letter is upper case (`B`), and Shift is written when it is held (`Shift+V`). A symbol is named by the key that
 *   types it, with Shift if held (`Shift+,`), as Electron's menus write it. `Plus` is the one exception: it is typed with
 *   Shift on some layouts and without on others, so it never carries Shift.
 * - Named keys follow Electron's accelerators, so the native menu shows the same chord: `Delete`, `Backspace`, `Enter`,
 *   `Escape`, `Space`, `Tab`, `Up`, `Down`, `Left`, `Right`, `Home`, `End`, `PageUp`, `PageDown`, `F1` to `F12`, and the
 *   number pad's digits as `Num0` to `Num9`.
 */
export type Chord = string;

const Modifiers = ['Mod', 'Ctrl', 'Alt', 'Shift'] as const;
type Modifier = (typeof Modifiers)[number];

/** What `event.key` calls a named key, as a chord writes it. */
const NamedKeys: Readonly<Record<string, string>> = {
  ' ': 'Space',
  '+': 'Plus',
  ArrowUp: 'Up',
  ArrowDown: 'Down',
  ArrowLeft: 'Left',
  ArrowRight: 'Right',
  Esc: 'Escape',
  Del: 'Delete'
};

const Named = new Set([
  'Space',
  'Plus',
  'Up',
  'Down',
  'Left',
  'Right',
  'Escape',
  'Delete',
  'Backspace',
  'Enter',
  'Tab',
  'Home',
  'End',
  'PageUp',
  'PageDown',
  'Insert',
  'ContextMenu',
  ...Array.from({ length: 12 }, (_, at) => `F${at + 1}`),
  ...Array.from({ length: 10 }, (_, at) => `Num${at}`)
]);

/** What a physical key types when Alt has changed its character, for symbols a chord can name. */
const CodeSymbols: Readonly<Record<string, string>> = {
  BracketLeft: '[',
  BracketRight: ']',
  Minus: '-',
  Equal: '=',
  Comma: ',',
  Period: '.',
  Slash: '/',
  Semicolon: ';',
  Quote: "'",
  Backquote: '`',
  Backslash: '\\',
  NumpadSubtract: '-',
  NumpadMultiply: '*',
  NumpadDivide: '/',
  NumpadEnter: 'Enter',
  NumpadDecimal: '.'
};

/** Whether this runs on a Mac, where the command key is ⌘ and the menus write keys as symbols. */
export function isMac(platform: string = globalThis.navigator.platform): boolean {
  return platform === 'darwin' || /mac/iu.test(platform);
}

/**
 * The chord a key press makes, or null for a press of a modifier alone or a key no chord names. The Windows key is
 * not a modifier any command uses, so a chord with it is null too.
 */
export function chordOf(event: KeyboardEvent, mac = isMac()): Chord | null {
  const found = keyOf(event);
  if (found === null || (!mac && event.metaKey)) {
    return null;
  }

  const held: Modifier[] = [];
  if (mac ? event.metaKey : event.ctrlKey) {
    held.push('Mod');
  }

  if (mac && event.ctrlKey) {
    held.push('Ctrl');
  }

  if (event.altKey) {
    held.push('Alt');
  }

  if (event.shiftKey && found.shift) {
    held.push('Shift');
  }

  return [...held, found.key].join('+');
}

/** The key a press names, and whether Shift is part of the chord or only of typing the key. */
function keyOf(event: KeyboardEvent): { key: string; shift: boolean } | null {
  if (/^Numpad\d$/u.test(event.code)) {
    return { key: `Num${event.code.slice(-1)}`, shift: true };
  }

  // `+` is Shift and `=` on one layout and a key of its own on another, so it carries its own Shift.
  if (event.key === '+' || event.code === 'NumpadAdd') {
    return { key: 'Plus', shift: false };
  }

  const named = NamedKeys[event.key] ?? event.key;
  if (Named.has(named)) {
    return { key: named, shift: true };
  }

  if (/^[a-z0-9]$/iu.test(event.key)) {
    return { key: event.key.toUpperCase(), shift: true };
  }

  // Alt changes what a key types on a Mac (Alt+B is ∫) and Shift what a symbol key types, so its place says which it
  // was: Shift and the comma key is ⇧, as the menu writes it, not <.
  const place = /^(?:Key([A-Z])|Digit(\d))$/u.exec(event.code);
  const key = place ? (place[1] ?? place[2]) : CodeSymbols[event.code];
  if (key) {
    return { key, shift: true };
  }

  // A symbol of a layout the table does not know, as it was typed.
  return event.key.length === 1 ? { key: event.key, shift: false } : null;
}

/** A chord written in the table's order, or null if it is not one: a typo in a settings file is dropped, not run. */
export function normalChord(text: string): Chord | null {
  const parts = text.split('+');
  // `Mod++` is Mod and Plus; `+` alone is Plus.
  const key = text.endsWith('++') || text === '+' ? 'Plus' : parts.pop();
  if (text.endsWith('++')) {
    parts.splice(-2, 2);
  }

  if (!key) {
    return null;
  }

  const held = parts.map(part => Modifiers.find(name => name.toLowerCase() === part.toLowerCase()));
  if (held.some(part => part === undefined) || new Set(held).size !== held.length) {
    return null;
  }

  const name = Named.has(key) ? key : key.length === 1 ? key.toUpperCase() : null;
  if (name === null) {
    return null;
  }

  return [...Modifiers.filter(modifier => held.includes(modifier)), name].join('+');
}

/** A chord as this platform presses it: off a Mac, `Ctrl` is the command key, so it is `Mod`. */
export function platformChord(chord: Chord, mac = isMac()): Chord {
  if (mac || !chord.split('+').includes('Ctrl')) {
    return chord;
  }

  return normalChord([...new Set(chord.split('+').map(part => (part === 'Ctrl' ? 'Mod' : part)))].join('+')) ?? chord;
}

/** Whether a chord holds the command key, which is what a native menu may take before a text field sees it. */
export function hasCommandKey(chord: Chord): boolean {
  return chord.split('+').some(part => part === 'Mod' || part === 'Ctrl');
}

const MacSymbols: Readonly<Record<string, string>> = { Mod: '⌘', Ctrl: '⌃', Alt: '⌥', Shift: '⇧' };

const MacKeys: Readonly<Record<string, string>> = {
  Delete: '⌦',
  Backspace: '⌫',
  Enter: '↩',
  Escape: '⎋',
  Tab: '⇥',
  Up: '↑',
  Down: '↓',
  Left: '←',
  Right: '→',
  Plus: '+',
  Space: 'Space'
};

const OtherKeys: Readonly<Record<string, string>> = {
  Mod: 'Ctrl',
  Delete: 'Del',
  Escape: 'Esc',
  Up: '↑',
  Down: '↓',
  Left: '←',
  Right: '→',
  Plus: '+'
};

/** A chord as a person reads it: `⇧⌘Z` on a Mac, `Ctrl+Shift+Z` elsewhere. */
export function chordText(chord: Chord, mac = isMac()): string {
  const parts = chord.split('+');
  const key = parts.pop() ?? '';
  const numpad = /^Num(\d)$/u.exec(key);
  const keyText = numpad ? `Num ${numpad[1]}` : ((mac ? MacKeys[key] : OtherKeys[key]) ?? key);
  if (mac) {
    // Apple's order is ⌃⌥⇧⌘.
    const order = ['Ctrl', 'Alt', 'Shift', 'Mod'];
    return [...order.filter(part => parts.includes(part)).map(part => MacSymbols[part]), keyText].join('');
  }

  return [...parts.map(part => OtherKeys[part] ?? part), keyText].join('+');
}

/** A chord as Electron's menu writes it, so the native menu shows the same key. */
export function acceleratorOf(chord: Chord): string {
  return chord
    .split('+')
    .map(part => {
      if (part === 'Mod') {
        return 'CmdOrCtrl';
      }

      const numpad = /^Num(\d)$/u.exec(part);
      return numpad ? `num${numpad[1]}` : part;
    })
    .join('+');
}
