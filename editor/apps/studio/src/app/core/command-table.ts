import type { Chord } from './chords';

/**
 * Where a command's keys work. `app` is anywhere; `stack` and `graph` are while that kind of document is in front;
 * `viewport` is while the viewport has focus. Two commands may share a key only where they can never both be live:
 * `stack` and `graph` never are, and everything else can be (see {@link overlaps}).
 */
export type CommandArea = 'app' | 'stack' | 'graph' | 'viewport';

/** The sections of the Keyboard Shortcuts dialog, in its order. */
export const Sections = ['File', 'Edit', 'Painting', 'Layers', 'Graph', 'Viewport', 'View', 'Window', 'Help'] as const;
export type Section = (typeof Sections)[number];

/** One thing the app can do: what the native menu, the shortcuts dialog, the key handler and a tooltip all read. */
export interface CommandSpec {
  /** What handlers, menus and a person's key settings name it by. Never renamed: the settings file keeps it. */
  readonly id: string;
  /** What a menu, the shortcuts dialog and a tooltip call it. A handler can say more, such as what Undo undoes. */
  readonly label: string;
  readonly section: Section;
  /** The keys it starts with in the standard preset; the first is the one a menu shows. */
  readonly keys?: readonly Chord[];
  /** Where its keys work. Defaults to `app`. */
  readonly area?: CommandArea;
  /** Its keys work in a text field too, as Save's do. Otherwise a text field keeps its keys. */
  readonly typing?: boolean;
  /** Its keys and menu item work while a dialog is open. Otherwise a dialog keeps its keys and the menu is off. */
  readonly inDialog?: boolean;
  /** A tooltip's words after the label, for a button that runs this. */
  readonly hint?: string;
}

/**
 * Every command. **To add one:** add a line here, then `commands.handle('id', …)` where the work is done, and put the id
 * in {@link MenuBar} if it belongs in a menu. The dialog, the key handler, the menu and tooltips follow.
 *
 * A key a control would take first, as xUI's graph canvas takes Delete, is caught ahead of it with
 * {@link Commands.isKeyOf} and handed to {@link Commands.press}, as Delete and Relink's are.
 */
export const CommandTable: readonly CommandSpec[] = [
  { id: 'newProject', label: 'New Project…', section: 'File', keys: ['Mod+N'] },
  { id: 'newFolder', label: 'New Folder', section: 'File', keys: ['Mod+Shift+N'] },
  { id: 'newLibrary', label: 'New Library…', section: 'File' },
  { id: 'openLibrary', label: 'Open Library…', section: 'File', keys: ['Mod+O'] },
  { id: 'import', label: 'Import…', section: 'File', keys: ['Mod+I'] },
  { id: 'export', label: 'Export…', section: 'File', keys: ['Mod+Shift+E'] },
  { id: 'bake', label: 'Bake…', section: 'File', keys: ['Mod+Shift+B'], hint: 'Bakes mesh maps' },
  {
    id: 'quickBake',
    label: 'Quick Bake',
    section: 'File',
    hint: 'Bakes with the project’s bake settings, without the dialog'
  },
  { id: 'reloadMesh', label: 'Reload Mesh', section: 'File', keys: ['Mod+Shift+R'] },
  { id: 'projectSettings', label: 'Project Settings…', section: 'File', keys: ['Mod+Shift+,'] },
  { id: 'save', label: 'Save', section: 'File', keys: ['Mod+S'], typing: true },
  { id: 'saveAs', label: 'Save As…', section: 'File', keys: ['Mod+Shift+S'] },
  { id: 'saveAll', label: 'Save All', section: 'File', keys: ['Mod+Alt+S'], typing: true },
  { id: 'closeDocument', label: 'Close Document', section: 'File', keys: ['Mod+W'] },
  { id: 'closeProject', label: 'Close Project', section: 'File' },

  { id: 'undo', label: 'Undo', section: 'Edit', keys: ['Mod+Z'] },
  { id: 'redo', label: 'Redo', section: 'Edit', keys: ['Mod+Shift+Z', 'Mod+Y'] },
  { id: 'cut', label: 'Cut', section: 'Edit', keys: ['Mod+X'] },
  { id: 'copy', label: 'Copy', section: 'Edit', keys: ['Mod+C'] },
  { id: 'paste', label: 'Paste', section: 'Edit', keys: ['Mod+V'] },
  { id: 'duplicate', label: 'Duplicate', section: 'Edit', keys: ['Mod+D'] },
  { id: 'delete', label: 'Delete', section: 'Edit', keys: ['Backspace', 'Delete'] },
  { id: 'selectAll', label: 'Select All', section: 'Edit', keys: ['Mod+A'] },

  { id: 'brushTool', label: 'Brush', section: 'Painting', keys: ['B'], area: 'stack', hint: 'Paints' },
  { id: 'eraserTool', label: 'Eraser', section: 'Painting', keys: ['E'], area: 'stack', hint: 'Takes paint away' },
  {
    id: 'pickerTool',
    label: 'Colour Picker',
    section: 'Painting',
    keys: ['C'],
    area: 'stack',
    hint: 'Takes the brush colour from the picture'
  },
  {
    id: 'lineTool',
    label: 'Line',
    section: 'Painting',
    keys: ['L'],
    area: 'stack',
    hint: 'Paints a straight line between two clicks'
  },
  {
    id: 'pathTool',
    label: 'Path',
    section: 'Painting',
    keys: ['P'],
    area: 'stack',
    hint: 'Paints through each click; double-click or Finish Path ends it'
  },
  { id: 'finishPath', label: 'Finish Path', section: 'Painting', keys: ['Enter'], area: 'stack' },
  { id: 'cancelStroke', label: 'Cancel Line or Path', section: 'Painting', keys: ['Escape'], area: 'stack' },
  { id: 'pickIdColour', label: 'Pick a Colour from the ID Map', section: 'Painting', keys: ['I'], area: 'stack' },
  { id: 'smallerBrush', label: 'Smaller Brush', section: 'Painting', keys: ['['], area: 'stack' },
  { id: 'largerBrush', label: 'Larger Brush', section: 'Painting', keys: [']'], area: 'stack' },

  { id: 'addPaintLayer', label: 'New Paint Layer', section: 'Layers', area: 'stack' },
  { id: 'addFillLayer', label: 'New Fill Layer', section: 'Layers', area: 'stack' },
  { id: 'addFilterLayer', label: 'New Filter Layer', section: 'Layers', area: 'stack' },
  { id: 'addLayerFolder', label: 'New Layer Folder', section: 'Layers', area: 'stack' },
  { id: 'groupLayers', label: 'Group into Folder', section: 'Layers', keys: ['Mod+G'], area: 'stack' },
  { id: 'renameLayer', label: 'Rename Layer', section: 'Layers', area: 'stack' },
  {
    id: 'flattenFolder',
    label: 'Flatten Folder',
    section: 'Layers',
    area: 'stack',
    hint: 'Makes the folder one canvas of the picture it made, keeping its layers as the way back'
  },
  { id: 'unflattenFolder', label: 'Unflatten Folder', section: 'Layers', area: 'stack' },
  { id: 'flattenStack', label: 'Flatten Stack', section: 'Layers', area: 'stack' },

  { id: 'addNode', label: 'Add Node…', section: 'Graph', keys: ['Space'], area: 'graph' },
  { id: 'groupNodes', label: 'Group Selection', section: 'Graph', keys: ['Mod+G'], area: 'graph' },
  {
    id: 'bypassNodes',
    label: 'Bypass',
    section: 'Graph',
    keys: ['Mod+B'],
    area: 'graph',
    hint: 'What feeds a node passes straight through'
  },
  {
    id: 'deleteRelink',
    label: 'Delete and Relink',
    section: 'Graph',
    keys: ['Mod+Backspace', 'Mod+Delete'],
    area: 'graph',
    hint: 'Joins what fed the nodes to what they fed'
  },
  { id: 'arrangeGraph', label: 'Lay Out', section: 'Graph', area: 'graph' },
  { id: 'alignLeft', label: 'Align Left', section: 'Graph', area: 'graph' },
  { id: 'alignCentres', label: 'Align Centres', section: 'Graph', area: 'graph' },
  { id: 'alignRight', label: 'Align Right', section: 'Graph', area: 'graph' },
  { id: 'alignTop', label: 'Align Top', section: 'Graph', area: 'graph' },
  { id: 'alignMiddles', label: 'Align Middles', section: 'Graph', area: 'graph' },
  { id: 'alignBottom', label: 'Align Bottom', section: 'Graph', area: 'graph' },
  { id: 'distributeHorizontally', label: 'Distribute Horizontally', section: 'Graph', area: 'graph' },
  { id: 'distributeVertically', label: 'Distribute Vertically', section: 'Graph', area: 'graph' },

  { id: 'frameModel', label: 'Frame the Model', section: 'Viewport', keys: ['F'] },
  { id: 'nextView', label: 'Next View', section: 'Viewport', keys: ['V'], area: 'viewport' },
  { id: 'previousView', label: 'Previous View', section: 'Viewport', keys: ['Shift+V'], area: 'viewport' },
  { id: 'viewFront', label: 'Front', section: 'Viewport', keys: ['Num1'], area: 'viewport' },
  { id: 'viewBack', label: 'Back', section: 'Viewport', keys: ['Ctrl+Num1'], area: 'viewport' },
  { id: 'viewRight', label: 'Right', section: 'Viewport', keys: ['Num3'], area: 'viewport' },
  { id: 'viewLeft', label: 'Left', section: 'Viewport', keys: ['Ctrl+Num3'], area: 'viewport' },
  { id: 'viewTop', label: 'Top', section: 'Viewport', keys: ['Num7'], area: 'viewport' },
  { id: 'viewBottom', label: 'Bottom', section: 'Viewport', keys: ['Ctrl+Num7'], area: 'viewport' },
  { id: 'orbitLeft', label: 'Orbit Left', section: 'Viewport', keys: ['Left'], area: 'viewport' },
  { id: 'orbitRight', label: 'Orbit Right', section: 'Viewport', keys: ['Right'], area: 'viewport' },
  { id: 'orbitUp', label: 'Orbit Up', section: 'Viewport', keys: ['Up'], area: 'viewport' },
  { id: 'orbitDown', label: 'Orbit Down', section: 'Viewport', keys: ['Down'], area: 'viewport' },
  { id: 'panLeft', label: 'Pan Left', section: 'Viewport', keys: ['Shift+Left'], area: 'viewport' },
  { id: 'panRight', label: 'Pan Right', section: 'Viewport', keys: ['Shift+Right'], area: 'viewport' },
  { id: 'panUp', label: 'Pan Up', section: 'Viewport', keys: ['Shift+Up'], area: 'viewport' },
  { id: 'panDown', label: 'Pan Down', section: 'Viewport', keys: ['Shift+Down'], area: 'viewport' },
  { id: 'zoomIn', label: 'Zoom In', section: 'Viewport', keys: ['Plus', '='], area: 'viewport' },
  { id: 'zoomOut', label: 'Zoom Out', section: 'Viewport', keys: ['-'], area: 'viewport' },

  { id: 'toggleStats', label: 'Show Statistics', section: 'View' },

  { id: 'layout1', label: 'First Layout', section: 'Window', keys: ['F1'] },
  { id: 'layout2', label: 'Second Layout', section: 'Window', keys: ['F2'] },
  { id: 'layout3', label: 'Third Layout', section: 'Window', keys: ['F3'] },
  { id: 'resetLayout', label: 'Reset Layout', section: 'Window' },
  { id: 'showHistory', label: 'History', section: 'Window', hint: 'Shows every undo step' },

  { id: 'shortcuts', label: 'Keyboard Shortcuts…', section: 'Help', keys: ['Mod+/'] },
  { id: 'diagnostics', label: 'Diagnostics…', section: 'Help' },
  { id: 'reportProblem', label: 'Report a Problem…', section: 'Help' }
];

/** Whether two areas can be live at once, which is when one key cannot mean both. */
export function overlaps(a: CommandArea, b: CommandArea): boolean {
  return a === b || a === 'app' || b === 'app' || a === 'viewport' || b === 'viewport';
}

/** A native menu item: a command's id, `-` for a line, a submenu, or something only the desktop app has. */
export type MenuEntry =
  | string
  | { readonly submenu: string; readonly items: readonly MenuEntry[] }
  | { readonly native: NativeEntry };

/**
 * What only the desktop app can put in a menu: the libraries it last opened, the window's own items, and in a
 * development build Reload and the developer tools.
 */
export type NativeEntry = 'recent' | 'closeWindow' | 'fullScreen' | 'windowItems' | 'development';

/**
 * The native menus, each shown when its `when` holds: Layer and Tools with a stack in front, Graph with a graph, and
 * Viewport where a document shows one. Items turn off when nothing would answer them.
 */
export const MenuBar: readonly {
  readonly label: string;
  readonly when?: 'stack' | 'graph' | 'viewport';
  readonly items: readonly MenuEntry[];
}[] = [
  {
    label: 'File',
    items: [
      'newProject',
      'newFolder',
      '-',
      'newLibrary',
      'openLibrary',
      { native: 'recent' },
      '-',
      'import',
      'export',
      'bake',
      'quickBake',
      'reloadMesh',
      'projectSettings',
      '-',
      'save',
      'saveAs',
      'saveAll',
      '-',
      'closeDocument',
      'closeProject',
      { native: 'closeWindow' }
    ]
  },
  {
    label: 'Edit',
    items: ['undo', 'redo', '-', 'cut', 'copy', 'paste', 'duplicate', 'delete', '-', 'selectAll']
  },
  {
    label: 'View',
    items: [{ native: 'development' }, 'toggleStats', '-', { native: 'fullScreen' }]
  },
  {
    label: 'Layer',
    when: 'stack',
    items: [
      'addPaintLayer',
      'addFillLayer',
      'addFilterLayer',
      'addLayerFolder',
      '-',
      'groupLayers',
      'renameLayer',
      '-',
      'flattenFolder',
      'unflattenFolder',
      'flattenStack'
    ]
  },
  {
    label: 'Tools',
    when: 'stack',
    items: [
      'brushTool',
      'eraserTool',
      'pickerTool',
      'lineTool',
      'pathTool',
      '-',
      'finishPath',
      'pickIdColour',
      '-',
      'smallerBrush',
      'largerBrush'
    ]
  },
  {
    label: 'Graph',
    when: 'graph',
    items: [
      'addNode',
      'groupNodes',
      '-',
      'bypassNodes',
      'deleteRelink',
      '-',
      {
        submenu: 'Arrange',
        items: [
          'arrangeGraph',
          '-',
          'alignLeft',
          'alignCentres',
          'alignRight',
          'alignTop',
          'alignMiddles',
          'alignBottom',
          '-',
          'distributeHorizontally',
          'distributeVertically'
        ]
      },
      '-',
      'showHistory'
    ]
  },
  {
    label: 'Viewport',
    when: 'viewport',
    items: [
      'frameModel',
      '-',
      'nextView',
      'previousView',
      {
        submenu: 'Camera',
        items: ['viewFront', 'viewBack', 'viewRight', 'viewLeft', 'viewTop', 'viewBottom']
      },
      '-',
      'zoomIn',
      'zoomOut'
    ]
  },
  {
    label: 'Window',
    items: [
      { submenu: 'Layout', items: ['layout1', 'layout2', 'layout3', '-', 'resetLayout'] },
      'showHistory',
      '-',
      { native: 'windowItems' }
    ]
  },
  { label: 'Help', items: ['shortcuts', 'diagnostics', '-', 'reportProblem'] }
];

/** Keys a control answers itself, as its kind of control does everywhere. Listed, but not rebound. */
export const FixedKeys: readonly { readonly section: string; readonly label: string; readonly keys: string }[] = [
  { section: 'Lists and trees', label: 'Move', keys: '↑ ↓, Home, End, Page Up, Page Down' },
  { section: 'Lists and trees', label: 'Open or close a folder', keys: '→ ←' },
  { section: 'Lists and trees', label: 'Open or choose', keys: 'Enter' },
  { section: 'Lists and trees', label: 'Rename in the Library', keys: 'F2' },
  { section: 'Lists and trees', label: 'Delete a Library folder', keys: 'Delete' },
  { section: 'Lists and trees', label: 'Context menu', keys: 'Shift+F10, Menu key' },
  { section: 'Graph canvas', label: 'Select all nodes', keys: 'Mod+A' },
  { section: 'Graph canvas', label: 'Delete the selection', keys: 'Delete' },
  { section: 'Graph canvas', label: 'Move the selection', keys: 'Arrows, Shift for a grid step' },
  { section: 'Graph canvas', label: 'Clear the selection', keys: 'Escape' },
  { section: 'Graph canvas', label: 'Bend a wire through a reroute dot', keys: 'Alt+click the wire' },
  { section: 'Text', label: 'Commit or cancel', keys: 'Enter, Escape' }
];

/** What a drag in a 3D or 2D view does, by button and modifier, in one preset. */
export interface Navigation {
  /** Words for the shortcuts dialog. */
  readonly orbit: string;
  readonly pan: string;
  readonly zoom: string;
  /** Which way a drag goes. `painting` is whether a left drag would paint. */
  drag(event: { button: number; altKey: boolean; shiftKey: boolean; ctrlKey: boolean; metaKey: boolean }, painting: boolean):
    | 'stroke'
    | 'orbit'
    | 'pan'
    | 'zoom';
  /** Whether a drag in a flat 2D view pans it. */
  pans2d(event: { button: number; altKey: boolean }): boolean;
}

const Standard: Navigation = {
  orbit: 'Drag, Alt+drag while painting, or right-drag',
  pan: 'Middle-drag or Shift+drag',
  zoom: 'Scroll',
  drag: (event, painting) => {
    if (event.button === 0 && painting && !event.altKey && !event.shiftKey) {
      return 'stroke';
    }

    return event.button === 1 || event.shiftKey ? 'pan' : 'orbit';
  },
  pans2d: event => event.button === 1 || event.altKey
};

/** Navigation as sculpting apps have it: the right button moves the view, and the left one stays the brush's. */
const RightDrag: Navigation = {
  orbit: 'Right-drag, or drag while not painting',
  pan: 'Alt+right-drag, middle-drag or Shift+drag',
  zoom: 'Ctrl+right-drag or scroll',
  drag: (event, painting) => {
    if (event.button === 2) {
      return event.altKey ? 'pan' : event.ctrlKey || event.metaKey ? 'zoom' : 'orbit';
    }

    if (event.button === 0 && painting && !event.altKey && !event.shiftKey) {
      return 'stroke';
    }

    return event.button === 1 || event.shiftKey || event.altKey ? 'pan' : 'orbit';
  },
  pans2d: event => event.button === 1 || event.button === 2 || event.altKey
};

/**
 * Key presets. A preset is a starting point: a person's own keys are kept on top of it. `keys` replaces the table's
 * keys for the commands it names.
 */
export const KeyPresets: Readonly<
  Record<
    string,
    { readonly label: string; readonly navigation: Navigation; readonly keys: Readonly<Record<string, readonly Chord[]>> }
  >
> = {
  standard: { label: 'Standard', navigation: Standard, keys: {} },
  rightDrag: {
    label: 'Right-drag navigation',
    navigation: RightDrag,
    // As sculpting apps have them: the brush grows and shrinks on S and Shift+S, and the eraser is X.
    keys: { eraserTool: ['X'], smallerBrush: ['[', 'Shift+S'], largerBrush: [']', 'S'] }
  }
};

export const DefaultPreset = 'standard';

/** A preset by name, or the standard one for a name no preset has. */
export function presetOf(name: string): (typeof KeyPresets)[string] & object {
  return KeyPresets[name] ?? { label: 'Standard', navigation: Standard, keys: {} };
}
