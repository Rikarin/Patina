import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { GraphStore } from '../graph/graph-store';
import { ProjectStore } from '../library/project-store';
import { PaintStore } from '../paint/paint-store';
import { menuModel } from './app-menu';
import { acceleratorOf, chordOf, chordText, normalChord } from './chords';
import { CommandTable, KeyPresets, MenuBar, type MenuEntry } from './command-table';
import { Commands, KeyWaitMs } from './commands';
import { readKeySettings, settleKeys } from './key-bindings';
import { ShortcutsDialog } from './shortcuts-dialog';

const key = (init: KeyboardEventInit) => new KeyboardEvent('keydown', { bubbles: true, cancelable: true, ...init });

describe('chords', () => {
  it('names a press with its modifiers, so that ⌘X is never X', () => {
    expect(chordOf(key({ key: 'x', code: 'KeyX', metaKey: true }), true)).toBe('Mod+X');
    expect(chordOf(key({ key: 'x', code: 'KeyX' }), true)).toBe('X');
    expect(chordOf(key({ key: 'x', code: 'KeyX', ctrlKey: true }), false)).toBe('Mod+X');
    expect(chordOf(key({ key: 'x', code: 'KeyX', ctrlKey: true }), true)).toBe('Ctrl+X');
    expect(chordOf(key({ key: 'V', code: 'KeyV', shiftKey: true }), true)).toBe('Shift+V');
  });

  it('reads a key by its place when a modifier changed what it types', () => {
    expect(chordOf(key({ key: '∫', code: 'KeyB', altKey: true }), true)).toBe('Alt+B');
    expect(chordOf(key({ key: '<', code: 'Comma', shiftKey: true, metaKey: true }), true)).toBe('Mod+Shift+,');
    expect(chordOf(key({ key: '+', code: 'Equal', shiftKey: true }), true)).toBe('Plus');
    expect(chordOf(key({ key: '1', code: 'Numpad1', ctrlKey: true }), true)).toBe('Ctrl+Num1');
    expect(chordOf(key({ key: 'ArrowLeft', code: 'ArrowLeft', shiftKey: true }), true)).toBe('Shift+Left');
  });

  it('makes no chord of a modifier alone or of the Windows key', () => {
    expect(chordOf(key({ key: 'Meta', code: 'MetaLeft', metaKey: true }), true)).toBeNull();
    expect(chordOf(key({ key: 'Shift', code: 'ShiftLeft', shiftKey: true }), true)).toBeNull();
    expect(chordOf(key({ key: 'z', code: 'KeyZ', metaKey: true }), false)).toBeNull();
  });

  it('writes a chord as the platform does, for menus and people', () => {
    expect(chordText('Mod+Shift+Z', true)).toBe('⇧⌘Z');
    expect(chordText('Mod+Shift+Z', false)).toBe('Ctrl+Shift+Z');
    expect(chordText('Backspace', true)).toBe('⌫');
    expect(chordText('Num7', false)).toBe('Num 7');
    expect(acceleratorOf('Mod+Shift+Z')).toBe('CmdOrCtrl+Shift+Z');
    expect(acceleratorOf('Ctrl+Num1')).toBe('Ctrl+num1');
  });

  it('reads a chord from a settings file, and drops one that is not', () => {
    expect(normalChord('shift+mod+z')).toBe('Mod+Shift+Z');
    expect(normalChord('Mod++')).toBe('Mod+Plus');
    expect(normalChord('Hyper+Z')).toBeNull();
    expect(normalChord('Mod+Mod+Z')).toBeNull();
    expect(normalChord('Mod+Banana')).toBeNull();
  });
});

describe('the command table', () => {
  it('gives each command one id, and menus only commands it has', () => {
    const ids = CommandTable.map(spec => spec.id);
    expect(new Set(ids).size).toBe(ids.length);
    const named = (entries: readonly MenuEntry[]): string[] =>
      entries.flatMap(entry =>
        typeof entry === 'string' ? (entry === '-' ? [] : [entry]) : 'submenu' in entry ? named(entry.items) : []
      );
    for (const id of MenuBar.flatMap(menu => named(menu.items))) {
      expect(ids).toContain(id);
    }
  });

  it('starts every preset with no key doing two things', () => {
    for (const [preset, { keys }] of Object.entries(KeyPresets)) {
      for (const mac of [true, false]) {
        const settled = settleKeys(CommandTable, { version: 1, preset, keys: {} }, mac);
        for (const spec of CommandTable) {
          // A key dropped while settling is one another command already had.
          expect(settled.get(spec.id)?.length, `${preset}: ${spec.id}`).toBe((keys[spec.id] ?? spec.keys ?? []).length);
        }
      }
    }
  });

  it('lets a person’s own key win over a default, and drops what a hand-edited file got wrong', () => {
    const settings = readKeySettings(
      { preset: 'nonsense', keys: { eraserTool: ['b'], brushTool: 'B', unknown: ['X'], lineTool: ['Mod+Banana'] } },
      CommandTable
    );
    expect(settings).toEqual({ version: 1, preset: 'standard', keys: { eraserTool: ['B'], lineTool: [] } });
    const settled = settleKeys(CommandTable, settings, true);
    expect(settled.get('eraserTool')).toEqual(['B']);
    expect(settled.get('brushTool')).toEqual([]);
  });
});

describe('Commands', () => {
  const written: unknown[] = [];
  let paintState: ReturnType<typeof signal<object | null>>;
  let commands: Commands;

  beforeEach(() => {
    written.length = 0;
    vi.stubGlobal('patina', {
      platform: 'darwin',
      readKeys: () => Promise.resolve(null),
      writeKeys: (settings: unknown) => {
        written.push(settings);
        return Promise.resolve();
      }
    });
    paintState = signal<object | null>({ id: 's' });
    TestBed.configureTestingModule({
      providers: [
        { provide: PaintStore, useValue: { state: paintState } },
        { provide: GraphStore, useValue: { document: signal(null) } },
        { provide: ProjectStore, useValue: { active: signal({ path: 'a', kind: 'texturing' }), graphInFront: signal(false) } }
      ]
    });
    commands = TestBed.inject(Commands);
    document.body.innerHTML = `
      <input data-testid="field" />
      <div role="dialog"><button data-testid="in-dialog">OK</button></div>
      <div data-document-undo><input data-testid="box" /></div>
      <button data-testid="plain">Plain</button>`;
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = '';
  });

  const element = (id: string) => document.querySelector<HTMLElement>(`[data-testid=${id}]`) as HTMLElement;
  const pressOn = (id: string | null, init: KeyboardEventInit) => {
    const target = id ? element(id) : document.body;
    target.focus();
    const event = key(init);
    target.dispatchEvent(event);
    commands.press(event);
    return event;
  };

  it('runs a key’s command, with its modifiers, and nothing for the key alone', () => {
    const cut = vi.fn();
    commands.handle('cut', { run: cut });

    pressOn(null, { key: 'x', code: 'KeyX' });
    expect(cut).not.toHaveBeenCalled();

    const event = pressOn(null, { key: 'x', code: 'KeyX', metaKey: true });
    expect(cut).toHaveBeenCalledWith('key');
    expect(event.defaultPrevented).toBe(true);
  });

  it('leaves keys to a text field and a dialog, except the ones meant to work there', () => {
    const brush = vi.fn();
    const save = vi.fn();
    commands.handle('brushTool', { run: brush });
    commands.handle('save', { run: save });

    pressOn('field', { key: 'b', code: 'KeyB' });
    pressOn('in-dialog', { key: 'b', code: 'KeyB' });
    expect(brush).not.toHaveBeenCalled();
    expect(commands.enabled('brushTool')).toBe(false); // the menu is off behind a dialog

    pressOn('field', { key: 's', code: 'KeyS', metaKey: true });
    expect(save).toHaveBeenCalledOnce();

    pressOn('plain', { key: 'b', code: 'KeyB' });
    expect(brush).toHaveBeenCalledOnce();
  });

  it('keeps a stack’s keys to a stack in front, and leaves a key a control already answered', () => {
    const brush = vi.fn();
    commands.handle('brushTool', { run: brush });
    paintState.set(null);
    pressOn(null, { key: 'b', code: 'KeyB' });
    expect(brush).not.toHaveBeenCalled();

    paintState.set({ id: 's' });
    const answered = key({ key: 'b', code: 'KeyB' });
    answered.preventDefault();
    commands.press(answered);
    expect(brush).not.toHaveBeenCalled();
  });

  it('turns a menu item on only when something would answer it, and says what Undo undoes', () => {
    expect(commands.enabled('undo')).toBe(false);
    const enabled = signal(false);
    commands.handle('undo', { enabled, label: () => 'Undo Paint Stroke', run: vi.fn() });
    expect(commands.enabled('undo')).toBe(false);
    enabled.set(true);
    expect(commands.enabled('undo')).toBe(true);
    expect(commands.label('undo')).toBe('Undo Paint Stroke');
    expect(commands.tooltip('undo')).toBe('Undo Paint Stroke (⌘Z)');
  });

  it('undoes the document from a box of a pane that undoes its document, unless something was typed there', () => {
    const text = vi.fn();
    const graph = vi.fn();
    commands.handle('undo', { text: true, enabled: () => commands.textUndoes(), run: text });
    commands.handle('undo', { run: graph });
    element('box').focus();

    commands.run('undo', 'accelerator');
    expect(graph).toHaveBeenCalledOnce();

    element('box').dispatchEvent(new Event('input', { bubbles: true }));
    commands.run('undo', 'accelerator');
    expect(text).toHaveBeenCalledOnce();

    element('box').dispatchEvent(new Event('change', { bubbles: true }));
    commands.run('undo', 'accelerator');
    expect(graph).toHaveBeenCalledTimes(2);

    element('field').focus(); // a box anywhere else undoes its own typing
    commands.run('undo', 'accelerator');
    expect(text).toHaveBeenCalledTimes(2);
  });

  describe('a key whose command waits for an edit in flight (#223)', () => {
    /** A document whose undo and redo go out one at a time, and whose history comes back with each answer. */
    const history = () => {
      const canUndo = signal(true);
      const canRedo = signal(false);
      const sent: ((done: () => void) => void)[] = [];
      const ran: string[] = [];
      const inFlight = signal(0);
      const step = (name: 'undo' | 'redo') => () => {
        ran.push(name);
        inFlight.update(count => count + 1);
        sent.push(done => {
          done();
          inFlight.update(count => count - 1);
        });
      };
      commands.handle('undo', { enabled: canUndo, waiting: () => inFlight() > 0, run: step('undo') });
      commands.handle('redo', { enabled: canRedo, waiting: () => inFlight() > 0, run: step('redo') });
      const answer = (after: () => void) => {
        sent.shift()?.(after);
        TestBed.tick();
      };
      return { canUndo, canRedo, ran, answer };
    };
    const undoKey = { key: 'z', code: 'KeyZ', metaKey: true };
    const redoKey = { key: 'Z', code: 'KeyZ', metaKey: true, shiftKey: true };

    it('runs Redo pressed before the undo it would redo has come back', () => {
      const { canUndo, canRedo, ran, answer } = history();
      pressOn(null, undoKey);
      const redo = pressOn(null, redoKey);
      expect(ran).toEqual(['undo']);
      expect(redo.defaultPrevented).toBe(true);

      answer(() => {
        canUndo.set(false);
        canRedo.set(true);
      });
      expect(ran).toEqual(['undo', 'redo']);
    });

    it('decides each at its turn, and drops one that has nothing to do then', () => {
      const { canUndo, canRedo, ran, answer } = history();
      pressOn(null, undoKey);
      pressOn(null, undoKey);
      pressOn(null, redoKey);
      expect(ran).toEqual(['undo']);

      // The one step is undone, so the second Undo has nothing to do; Redo, behind it, still runs.
      answer(() => {
        canUndo.set(false);
        canRedo.set(true);
      });
      expect(ran).toEqual(['undo', 'redo']);
    });

    it('never runs a command that stays off, even when it turns on later', () => {
      const { canRedo, ran, answer } = history();
      pressOn(null, undoKey);
      pressOn(null, redoKey);
      answer(() => undefined); // the undo came back with nothing to redo
      expect(ran).toEqual(['undo']);

      canRedo.set(true);
      TestBed.tick();
      expect(ran).toEqual(['undo']);
    });

    it('keeps the keys behind a waiting one in order, and gives up on one kept waiting too long', () => {
      vi.useFakeTimers();
      try {
        const { canUndo, canRedo, ran, answer } = history();
        const cut = vi.fn(() => ran.push('cut'));
        commands.handle('cut', { run: cut });
        pressOn(null, undoKey);
        pressOn(null, redoKey);
        pressOn(null, { key: 'x', code: 'KeyX', metaKey: true });
        expect(ran).toEqual(['undo']); // Cut would overtake Redo

        vi.advanceTimersByTime(KeyWaitMs);
        TestBed.tick();
        expect(ran).toEqual(['undo']);

        answer(() => {
          canUndo.set(false);
          canRedo.set(true);
        });
        expect(ran).toEqual(['undo']);
        pressOn(null, { key: 'x', code: 'KeyX', metaKey: true });
        expect(ran).toEqual(['undo', 'cut']);
      } finally {
        vi.useRealTimers();
      }
    });

    it('waits behind a menu’s key too, but not behind a click', () => {
      const { canUndo, canRedo, ran, answer } = history();
      commands.run('undo', 'accelerator');
      expect(commands.run('redo', 'accelerator')).toBe(true);
      expect(commands.run('redo', 'menu')).toBe(false);
      answer(() => {
        canUndo.set(false);
        canRedo.set(true);
      });
      expect(ran).toEqual(['undo', 'redo']);
    });
  });

  it('refuses a key another command has where both work, and swaps when asked', () => {
    const refused = commands.bind('eraserTool', 'B');
    expect(refused.done ? null : [refused.taken.id, refused.chord]).toEqual(['brushTool', 'B']);
    expect(commands.keys().get('eraserTool')).toEqual(['E']);

    commands.swap('eraserTool', 'brushTool', 'B');
    expect(commands.keys().get('eraserTool')).toEqual(['B']);
    expect(commands.keys().get('brushTool')).toEqual(['E']);
    expect(written.at(-1)).toEqual({ version: 1, preset: 'standard', keys: { eraserTool: ['B'], brushTool: ['E'] } });

    // A graph's key can be a stack's too: the two are never in front at once.
    expect(commands.bind('groupNodes', 'B')).toEqual({ done: true });
  });

  it('starts again from a preset, which moves the view with the right button', () => {
    commands.bind('brushTool', 'Q');
    commands.usePreset('rightDrag');
    expect(commands.keys().get('brushTool')).toEqual(['B']);
    expect(commands.keys().get('eraserTool')).toEqual(['X']);
    const drag = (button: number, more: Partial<MouseEvent> = {}) =>
      commands.navigation().drag({ button, altKey: false, shiftKey: false, ctrlKey: false, metaKey: false, ...more }, true);
    expect([drag(0), drag(2), drag(2, { altKey: true }), drag(2, { ctrlKey: true })]).toEqual([
      'stroke',
      'orbit',
      'pan',
      'zoom'
    ]);
  });
});

describe('the Keyboard Shortcuts dialog', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('binds the chord pressed, modifiers and all, and says who has a key already', async () => {
    vi.stubGlobal('patina', { platform: 'darwin', readKeys: () => Promise.resolve(null), writeKeys: () => Promise.resolve() });
    TestBed.configureTestingModule({
      providers: [
        { provide: PaintStore, useValue: { state: signal(null) } },
        { provide: GraphStore, useValue: { document: signal(null) } },
        { provide: ProjectStore, useValue: { active: signal(null), graphInFront: signal(false) } }
      ]
    });
    const fixture = TestBed.createComponent(ShortcutsDialog);
    fixture.componentRef.setInput('open', true);
    await fixture.whenStable();
    const row = (id: string) =>
      document.querySelector<HTMLButtonElement>(`[data-command=${id}] [data-testid=shortcut-key]`) as HTMLButtonElement;

    const press = async (init: KeyboardEventInit) => {
      row('brushTool').click();
      await fixture.whenStable();
      row('brushTool').dispatchEvent(key(init));
      await fixture.whenStable();
    };
    const commands = TestBed.inject(Commands);

    await press({ key: 'q', code: 'KeyQ', metaKey: true });
    expect(commands.keys().get('brushTool')).toEqual(['Mod+Q']);

    await press({ key: 'x', code: 'KeyX', metaKey: true });
    expect(commands.keys().get('brushTool')).toEqual(['Mod+Q']);
    expect(document.querySelector('[data-testid=shortcuts-taken]')?.textContent).toContain('⌘X is already Cut');
  });
});

describe('the native menus', () => {
  const source = (on: readonly string[], keys: Record<string, string[]> = {}) => ({
    enabled: (id: string) => on.includes(id),
    label: (id: string) => id,
    checked: (id: string) => (id === 'brushTool' ? true : undefined),
    keys: () => new Map(Object.entries(keys))
  });
  const labels = (model: ReturnType<typeof menuModel>) => model.map(menu => menu.label);

  it('shows the menus of the document in front, and nothing else', () => {
    expect(labels(menuModel(source([]), { stack: false, graph: false, viewport: false }))).toEqual([
      'File',
      'Edit',
      'View',
      'Window',
      'Help'
    ]);
    expect(labels(menuModel(source([]), { stack: true, graph: false, viewport: true }))).toEqual([
      'File',
      'Edit',
      'View',
      'Layer',
      'Tools',
      'Viewport',
      'Window',
      'Help'
    ]);
    expect(labels(menuModel(source([]), { stack: false, graph: true, viewport: false }))).toContain('Graph');
  });

  it('turns items off that nothing answers, and takes only keys with the command key', () => {
    const model = menuModel(source(['save'], { save: ['Mod+S'], brushTool: ['B'] }), {
      stack: true,
      graph: false,
      viewport: false
    });
    const items = model.flatMap(menu => menu.items);
    expect(items).toContainEqual({
      type: 'command',
      id: 'save',
      label: 'save',
      enabled: true,
      accelerator: 'CmdOrCtrl+S',
      registered: true
    });
    expect(items).toContainEqual({
      type: 'command',
      id: 'brushTool',
      label: 'brushTool',
      enabled: false,
      accelerator: 'B',
      registered: false,
      checked: true
    });
    for (const menu of model) {
      expect(menu.items[0]?.type, menu.label).not.toBe('separator');
      expect(menu.items.at(-1)?.type, menu.label).not.toBe('separator');
    }
  });
});
