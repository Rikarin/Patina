import { type DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { GraphStore } from '../graph/graph-store';
import { ProjectStore } from '../library/project-store';
import { PaintStore } from '../paint/paint-store';
import { type Chord, chordOf, chordText, isMac, normalChord } from './chords';
import { type CommandArea, type CommandSpec, CommandTable, KeyPresets, presetOf } from './command-table';
import { desktop } from './desktop';
import { type KeySettings, NoKeySettings, readKeySettings, settleKeys, takenBy } from './key-bindings';

/** How a command was asked for, which decides what it may do: a key in a text field leaves the text alone. */
export type CommandVia = 'key' | 'accelerator' | 'menu' | 'button';

/** What does a command's work, from the place that owns it. A command can have several; the first live one runs. */
export interface CommandHandler {
  /** Whether it would do something now. The signals it reads keep the menu's items on and off. */
  readonly enabled?: () => boolean;
  run(via: CommandVia): unknown;
  /** What the menu says instead of the table's label, such as Undo Paint Stroke. */
  readonly label?: () => string | null;
  /** A tick beside the menu item. */
  readonly checked?: () => boolean;
  /**
   * It edits the focused text field. Such a handler runs first in a field, even for a command whose keys a field keeps,
   * since that is what the field's own key would do; and a key the page sees is left to the field itself.
   */
  readonly text?: boolean;
}

/** Where focus is, as commands care: which part of the app, and whether it is a text field or inside a dialog. */
export interface CommandFocus {
  /** The nearest `data-commands` above the focused element: `stack`, `graph`, `viewport`, `library`, or null. */
  area: string | null;
  text: boolean;
  dialog: boolean;
}

const TextFields = 'input:not([type=checkbox]):not([type=radio]):not([type=range]):not([type=button]), textarea, select, [contenteditable=true], [contenteditable=""]';

/** What Space and Enter press when it has focus. */
const Pressable =
  'button, a[href], input, select, textarea, summary, [role=button], [role=menuitem], [role=option], [role=tab], [role=treeitem], [role=checkbox], [role=switch]';

/** Where an element is, for commands. */
export function focusOf(element: Element | null): CommandFocus {
  if (!element || element === globalThis.document.body) {
    return { area: null, text: false, dialog: false };
  }

  return {
    area: element.closest('[data-commands]')?.getAttribute('data-commands') ?? 'other',
    text: element.matches(TextFields),
    dialog: element.closest('[role=dialog], [role=alertdialog]') !== null
  };
}

/** The outcome of a rebinding: done, or refused because another command has the key where this one's keys work. */
export type Rebinding = { done: true } | { done: false; taken: CommandSpec; chord: Chord };

/**
 * The one command table at work: what each key does, what the native menu shows and which of its items are on, and
 * what tooltips say. Panes register handlers for what they own; a key press, a menu item and a button all come here.
 */
@Injectable({ providedIn: 'root' })
export class Commands {
  private readonly paint = inject(PaintStore);
  private readonly graph = inject(GraphStore);
  private readonly project = inject(ProjectStore);
  private readonly mac = isMac(desktop()?.platform);

  readonly table: readonly CommandSpec[] = CommandTable;
  private readonly specs = new Map(this.table.map(spec => [spec.id, spec]));

  /** A person's keys and preset, as the settings file holds them. */
  readonly settings = signal<KeySettings>(NoKeySettings);
  /** Each command's keys now, the first being the one a menu shows. */
  readonly keys = computed(() => settleKeys(this.table, this.settings(), this.mac));
  /** How drags move a view, from the preset. */
  readonly navigation = computed(() => presetOf(this.settings().preset).navigation);

  private readonly byChord = computed(() => {
    const found = new Map<Chord, string[]>();
    for (const [id, chords] of this.keys()) {
      for (const chord of chords) {
        found.set(chord, [...(found.get(chord) ?? []), id]);
      }
    }

    return found;
  });

  private readonly handlers = signal<ReadonlyMap<string, readonly CommandHandler[]>>(new Map());
  private readonly focused = signal<Element | null>(null);
  /** Where focus is now. */
  readonly focus = computed(() => focusOf(this.focused()));
  /** The text field typed in since it last committed, which is where Undo goes in a pane that undoes its document. */
  private typedIn: EventTarget | null = null;

  constructor() {
    const document = globalThis.document;
    document.addEventListener('focusin', event => this.focused.set(event.target as Element), true);
    document.addEventListener(
      'focusout',
      event => {
        this.typedIn = null;
        if (!event.relatedTarget) {
          this.focused.set(null);
        }
      },
      true
    );
    document.addEventListener('input', event => (this.typedIn = event.target), true);
    document.addEventListener('change', () => (this.typedIn = null), true);

    void desktop()
      ?.readKeys()
      .then(saved => this.settings.set(readKeySettings(saved, this.table)))
      .catch(() => undefined); // no file is the defaults
  }

  /**
   * Says what does a command's work, until `destroyRef` ends or the returned function is called. A text handler goes
   * before the rest; otherwise the earlier registration goes first.
   */
  handle(id: string, handler: CommandHandler, destroyRef?: DestroyRef): () => void {
    if (!this.specs.has(id)) {
      throw new Error(`No command '${id}' in the command table.`);
    }

    this.handlers.update(all => {
      const next = new Map(all);
      const list = [...(next.get(id) ?? []), handler];
      next.set(id, [...list.filter(held => held.text), ...list.filter(held => !held.text)]);
      return next;
    });
    const release = () =>
      this.handlers.update(all => {
        const next = new Map(all);
        next.set(id, (next.get(id) ?? []).filter(held => held !== handler));
        return next;
      });
    destroyRef?.onDestroy(release);
    return release;
  }

  spec(id: string): CommandSpec | undefined {
    return this.specs.get(id);
  }

  /** Whether a command would do something if asked from a menu now. */
  enabled(id: string): boolean {
    return this.live(id, 'menu') !== null;
  }

  /** What a menu calls a command now. */
  label(id: string): string {
    const spec = this.specs.get(id);
    const handler = this.live(id, 'menu') ?? this.handlers().get(id)?.[0];
    return handler?.label?.() ?? spec?.label ?? id;
  }

  checked(id: string): boolean | undefined {
    const handler = this.handlers().get(id)?.find(held => held.checked);
    return handler?.checked?.();
  }

  /** Whether a key press is one of a command's keys now, for a control that has to catch it before its own handler. */
  isKeyOf(id: string, event: KeyboardEvent): boolean {
    const chord = chordOf(event, this.mac);
    return chord !== null && (this.keys().get(id) ?? []).includes(chord);
  }

  /** A command's first key as a person reads it, or an empty string. */
  keyText(id: string): string {
    const chord = this.keys().get(id)?.[0];
    return chord ? chordText(chord, this.mac) : '';
  }

  /** What a button that runs a command says on hover: its name, its key and what it does. */
  tooltip(id: string, label = this.label(id)): string {
    const key = this.keyText(id);
    const hint = this.specs.get(id)?.hint;
    return [key ? `${label} (${key})` : label, hint].filter(Boolean).join(' — ');
  }

  /** Runs a command if something would answer it. False if nothing did. */
  run(id: string, via: CommandVia = 'button'): boolean {
    const handler = this.live(id, via);
    if (!handler) {
      return false;
    }

    void handler.run(via);
    return true;
  }

  /**
   * A key pressed anywhere in the window. A control that answered it already said so (a list moving its rows, a field
   * typing); otherwise the first live command with that key runs. ⚠ It returns nothing on purpose: a listener that returns
   * false makes Angular prevent the default, which swallowed Tab and Delete everywhere.
   */
  press(event: KeyboardEvent): void {
    if (event.defaultPrevented) {
      return;
    }

    const chord = chordOf(event, this.mac);
    // Space and Enter press whatever control has focus, as they do everywhere.
    if ((chord === 'Space' || chord === 'Enter') && event.target instanceof Element && event.target.closest(Pressable)) {
      return;
    }

    for (const id of chord ? (this.byChord().get(chord) ?? []) : []) {
      const handler = this.live(id, 'key');
      // A field types its own keys: the page's default does what a text handler would.
      if (handler && !handler.text) {
        event.preventDefault();
        void handler.run('key');
        return;
      }
    }
  }

  /**
   * Gives a command a key, in place of the keys it had. Refused when another command has it where this one's keys
   * work: one key never does two things, and the caller can offer {@link swap}.
   */
  bind(id: string, chord: Chord): Rebinding {
    const taken = takenBy(this.table, this.keys(), id, chord);
    if (taken) {
      return { done: false, taken, chord };
    }

    this.write({ ...this.settings().keys, [id]: [chord] });
    return { done: true };
  }

  /** Gives `id` the key `taken` had, and `taken` the keys `id` had, so that neither is left unreachable by surprise. */
  swap(id: string, taken: string, chord: Chord): void {
    const mine = (this.keys().get(id) ?? []).filter(held => held !== chord);
    const theirs = (this.keys().get(taken) ?? []).filter(held => held !== chord);
    this.write({ ...this.settings().keys, [id]: [chord], [taken]: [...theirs, ...mine] });
  }

  /** Takes a command's keys away. */
  clear(id: string): void {
    this.write({ ...this.settings().keys, [id]: [] });
  }

  /** Starts again from a preset, without the keys changed since. */
  usePreset(preset: string): void {
    this.save({ version: 1, preset: preset in KeyPresets ? preset : 'standard', keys: {} });
  }

  private write(keys: Record<string, string[]>): void {
    this.save({ ...this.settings(), keys });
  }

  private save(settings: KeySettings): void {
    this.settings.set(settings);
    void desktop()
      ?.writeKeys(settings)
      .catch(() => undefined); // kept for this session; the file says what it can
  }

  /** The handler that would run, or null: nothing answers, or where focus is keeps this command's keys. */
  private live(id: string, via: CommandVia): CommandHandler | null {
    const spec = this.specs.get(id);
    const handlers = this.handlers().get(id) ?? [];
    if (!spec || handlers.length === 0) {
      return null;
    }

    const focus = this.focus();
    const ready = handlers.filter(handler => (!handler.text || focus.text) && (handler.enabled?.() ?? true));
    const text = ready.find(handler => handler.text);
    if (text) {
      // A key the page sees is left to the field, which does the same by itself.
      return via === 'key' ? null : text;
    }

    const keyed = via === 'key' || via === 'accelerator';
    // A field that declined its own handler (one that undoes its document) lets the document's run.
    const declined = handlers.some(handler => handler.text);
    if (
      (focus.dialog && !spec.inDialog) ||
      (keyed && focus.text && !spec.typing && !declined) ||
      (keyed && !this.areaLive(spec.area))
    ) {
      return null;
    }

    return ready.find(handler => !handler.text) ?? null;
  }

  private areaLive(area: CommandArea | undefined): boolean {
    switch (area) {
      case 'stack':
        return this.paint.state() !== null && this.project.active() !== null && !this.project.graphInFront();
      case 'graph':
        return this.graph.document() !== null && this.project.graphInFront();
      case 'viewport':
        return this.focus().area === 'viewport';
      default:
        return true;
    }
  }

  /**
   * Whether the focused text field holds typing not yet committed, or is not in a pane whose Undo is the document's.
   * Such a pane marks itself `data-document-undo`: Undo in one of its boxes undoes the document unless something was
   * typed there.
   */
  textUndoes(): boolean {
    const active = globalThis.document.activeElement;
    return active !== null && (active === this.typedIn || active.closest('[data-document-undo]') === null);
  }

  /** Normalises a chord typed into a settings file or a test. */
  readonly chord = normalChord;
}
