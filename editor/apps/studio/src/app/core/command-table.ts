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
  {
    id: 'importMaterials',
    label: 'Import Materials…',
    section: 'File',
    hint: 'Brings the materials of a glTF, MaterialX or USD file into the project, each as a graph'
  },
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
  { id: 'showBin', label: 'Bin…', section: 'File', hint: 'What was deleted, to restore or delete for good' },

  { id: 'undo', label: 'Undo', section: 'Edit', keys: ['Mod+Z'] },
  { id: 'redo', label: 'Redo', section: 'Edit', keys: ['Mod+Shift+Z', 'Mod+Y'] },
  { id: 'cut', label: 'Cut', section: 'Edit', keys: ['Mod+X'] },
  { id: 'copy', label: 'Copy', section: 'Edit', keys: ['Mod+C'] },
  { id: 'paste', label: 'Paste', section: 'Edit', keys: ['Mod+V'] },
  { id: 'duplicate', label: 'Duplicate', section: 'Edit', keys: ['Mod+D'] },
  { id: 'delete', label: 'Delete', section: 'Edit', keys: ['Backspace', 'Delete'] },
  { id: 'selectAll', label: 'Select All', section: 'Edit', keys: ['Mod+A'] },
  // In the Edit menu, and on a Mac in the app menu, where the desktop app moves it (apps/desktop/src/menu.ts).
  { id: 'preferences', label: 'Preferences…', section: 'Edit', keys: ['Mod+,'] },

  { id: 'brushTool', label: 'Brush', section: 'Painting', keys: ['B'], area: 'stack', hint: 'Paints' },
  { id: 'eraserTool', label: 'Eraser', section: 'Painting', keys: ['E'], area: 'stack', hint: 'Takes paint away' },
  {
    id: 'smudgeTool',
    label: 'Smudge',
    section: 'Painting',
    keys: ['R'],
    area: 'stack',
    hint: 'Drags the paint under the brush along the stroke'
  },
  {
    id: 'cloneTool',
    label: 'Clone',
    section: 'Painting',
    area: 'stack',
    hint: 'Paints the picture as it lies at the clone source; Set Clone Source, or a Cmd- or Ctrl-click, chooses it'
  },
  {
    id: 'setCloneSource',
    label: 'Set Clone Source',
    section: 'Painting',
    area: 'stack',
    hint: 'The next click on the mesh or the map is where the clone reads from'
  },
  {
    id: 'projectionTool',
    label: 'Projection',
    section: 'Painting',
    area: 'stack',
    hint: 'Paints the picture on the viewport through the brush'
  },
  {
    id: 'fillTool',
    label: 'Polygon Fill',
    section: 'Painting',
    keys: ['G'],
    area: 'stack',
    hint: 'Fills the triangle, polygon, mesh part or UV shell under a click'
  },
  {
    id: 'toggleStencil',
    label: 'Stencil',
    section: 'Painting',
    area: 'stack',
    hint: 'The picture on the viewport masks the brush by its brightness'
  },
  {
    id: 'moveStencilLeft',
    label: 'Move Stencil Left',
    section: 'Painting',
    keys: ['Alt+Left'],
    area: 'viewport',
    hint: 'Moves the picture on the viewport, which a stencil or the projection tool reads'
  },
  { id: 'moveStencilRight', label: 'Move Stencil Right', section: 'Painting', keys: ['Alt+Right'], area: 'viewport' },
  { id: 'moveStencilUp', label: 'Move Stencil Up', section: 'Painting', keys: ['Alt+Up'], area: 'viewport' },
  { id: 'moveStencilDown', label: 'Move Stencil Down', section: 'Painting', keys: ['Alt+Down'], area: 'viewport' },
  {
    id: 'growStencil',
    label: 'Enlarge Stencil',
    section: 'Painting',
    keys: ['Alt+Plus', 'Alt+='],
    area: 'viewport'
  },
  { id: 'shrinkStencil', label: 'Shrink Stencil', section: 'Painting', keys: ['Alt+-'], area: 'viewport' },
  { id: 'turnStencilLeft', label: 'Turn Stencil Left', section: 'Painting', keys: ['Alt+['], area: 'viewport' },
  { id: 'turnStencilRight', label: 'Turn Stencil Right', section: 'Painting', keys: ['Alt+]'], area: 'viewport' },
  {
    id: 'pickerTool',
    label: 'Color Picker',
    section: 'Painting',
    keys: ['C'],
    area: 'stack',
    hint: 'Takes the brush color from the picture'
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
  { id: 'pickIdColour', label: 'Pick a Color from the ID Map', section: 'Painting', keys: ['I'], area: 'stack' },
  { id: 'smallerBrush', label: 'Smaller Brush', section: 'Painting', keys: ['['], area: 'stack' },
  { id: 'largerBrush', label: 'Larger Brush', section: 'Painting', keys: [']'], area: 'stack' },

  { id: 'addPaintLayer', label: 'New Paint Layer', section: 'Layers', area: 'stack' },
  { id: 'addFillLayer', label: 'New Fill Layer', section: 'Layers', area: 'stack' },
  { id: 'addFilterLayer', label: 'New Filter Layer', section: 'Layers', area: 'stack' },
  { id: 'addLayerFolder', label: 'New Layer Folder', section: 'Layers', area: 'stack' },
  { id: 'groupLayers', label: 'Group into Folder', section: 'Layers', keys: ['Mod+G'], area: 'stack' },
  { id: 'moveLayerUp', label: 'Move Layer Up', section: 'Layers', keys: ['Mod+]'], area: 'stack' },
  { id: 'moveLayerDown', label: 'Move Layer Down', section: 'Layers', keys: ['Mod+['], area: 'stack' },
  { id: 'renameLayer', label: 'Rename Layer', section: 'Layers', area: 'stack' },
  {
    id: 'flattenFolder',
    label: 'Flatten Folder',
    section: 'Layers',
    area: 'stack',
    hint: 'Makes the folder one canvas of the picture it made, keeping its layers as the way back'
  },
  { id: 'unflattenFolder', label: 'Unflatten Folder', section: 'Layers', area: 'stack' },
  { id: 'flattenStack', label: 'Flatten All Layers', section: 'Layers', area: 'stack' },
  {
    id: 'exportLayer',
    label: 'Export Layer Alone…',
    section: 'Layers',
    area: 'stack',
    hint: 'Writes the layer’s maps on their own, over the channels’ defaults, into a folder you choose'
  },
  {
    id: 'exportMask',
    label: 'Export Mask Alone…',
    section: 'Layers',
    area: 'stack',
    hint: 'Writes the layer’s mask as one gray map, white where the layer shows, into a folder you choose'
  },
  {
    id: 'createInstance',
    label: 'Create Instance',
    section: 'Layers',
    area: 'stack',
    hint: 'A layer that stands for the chosen one and follows every edit to it'
  },
  { id: 'pasteAsInstance', label: 'Paste as Instance', section: 'Layers', area: 'stack' },
  {
    id: 'findLayer',
    label: 'Find Layer Under Cursor',
    section: 'Layers',
    area: 'stack',
    hint: 'The next click on the mesh or the map chooses the layer on top there'
  },
  {
    id: 'openMaskStack',
    label: 'Open Mask in Layers',
    section: 'Layers',
    area: 'stack',
    hint: 'Shows the chosen layer’s mask in Layers, with its own layers and effects'
  },

  { id: 'addNode', label: 'Add Node…', section: 'Graph', keys: ['Space'], area: 'graph' },
  {
    id: 'findNode',
    label: 'Find Node…',
    section: 'Graph',
    keys: ['Mod+F'],
    area: 'graph',
    hint: 'Selects a node of this graph by its name and shows it'
  },
  { id: 'groupNodes', label: 'Frame Selection', section: 'Graph', keys: ['Mod+G'], area: 'graph' },
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
  {
    id: 'swapInputs',
    label: 'Swap Inputs',
    section: 'Graph',
    area: 'graph',
    hint: 'Trades the wires into a node’s first two inputs of one kind'
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
  {
    id: 'toggleGraphSnap',
    label: 'Snap to Grid',
    section: 'Graph',
    area: 'graph',
    hint: 'A node dragged on the graph lands on the grid'
  },
  {
    id: 'toggleGraphParameters',
    label: 'Parameters on Cards',
    section: 'Graph',
    area: 'graph',
    hint: 'Each node lists what was changed on it, under its title'
  },
  { id: 'curvedWires', label: 'Curved Wires', section: 'Graph', area: 'graph' },
  { id: 'angledWires', label: 'Angled Wires', section: 'Graph', area: 'graph' },
  { id: 'straightWires', label: 'Straight Wires', section: 'Graph', area: 'graph' },
  {
    id: 'publishPackage',
    label: 'Publish as Package…',
    section: 'Graph',
    area: 'graph',
    hint: 'Saves the graph with what it reads into the library’s packages, where every project’s shelf offers it'
  },

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
  {
    id: 'toggleWireframe',
    label: 'Wireframe',
    section: 'Viewport',
    keys: ['W'],
    area: 'viewport',
    hint: 'Draws the triangles’ edges over the model'
  },
  {
    id: 'toggleGrid',
    label: 'Grid',
    section: 'Viewport',
    area: 'viewport',
    hint: 'Draws a grid on the floor the model stands on, with the world’s axes'
  },
  {
    id: 'saveScreenshot',
    label: 'Save Screenshot…',
    section: 'Viewport',
    hint: 'Writes the picture as it is now to a PNG file'
  },

  { id: 'toggleStats', label: 'Show Statistics', section: 'View' },

  // As Finder's views are: F2 is Rename in every list, so the layouts are not on F keys (#224).
  { id: 'layout1', label: 'First Layout', section: 'Window', keys: ['Mod+1'] },
  { id: 'layout2', label: 'Second Layout', section: 'Window', keys: ['Mod+2'] },
  { id: 'layout3', label: 'Third Layout', section: 'Window', keys: ['Mod+3'] },
  { id: 'layout4', label: 'Fourth Layout', section: 'Window', keys: ['Mod+4'] },
  { id: 'resetLayout', label: 'Reset Layout', section: 'Window' },
  { id: 'showHistory', label: 'History', section: 'Window', hint: 'Shows every undo step' },
  {
    id: 'showChecks',
    label: 'Checks',
    section: 'Window',
    hint: 'Lists what the texture set in front would get wrong in an export'
  },
  {
    id: 'showActivity',
    label: 'Activity',
    section: 'Window',
    hint: 'Bakes and exports with how far each is, and everything the app has said'
  },

  {
    id: 'documentation',
    label: 'Documentation',
    section: 'Help',
    hint: 'A guide to Patina, from a library to an export, in the browser'
  },
  { id: 'shortcuts', label: 'Keyboard Shortcuts…', section: 'Help', keys: ['Mod+/'] },
  { id: 'diagnostics', label: 'Diagnostics…', section: 'Help' },
  { id: 'reportProblem', label: 'Report a Problem…', section: 'Help' },
  { id: 'about', label: 'About Patina', section: 'Help', hint: 'The version, the licences, and whose your work is' }
];

/** Whether two areas can be live at once, which is when one key cannot mean both. */
export function overlaps(a: CommandArea, b: CommandArea): boolean {
  return a === b || a === 'app' || b === 'app' || a === 'viewport' || b === 'viewport';
}

/** A native menu item: a command's id, `-` for a line, a submenu, or something only the desktop app has. */
export type MenuEntry =
  string | { readonly submenu: string; readonly items: readonly MenuEntry[] } | { readonly native: NativeEntry };

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
      'importMaterials',
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
      'showBin',
      { native: 'closeWindow' }
    ]
  },
  {
    label: 'Edit',
    items: ['undo', 'redo', '-', 'cut', 'copy', 'paste', 'duplicate', 'delete', '-', 'selectAll', '-', 'preferences']
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
      'moveLayerUp',
      'moveLayerDown',
      '-',
      'flattenFolder',
      'unflattenFolder',
      'flattenStack',
      'exportLayer',
      'exportMask',
      '-',
      'createInstance',
      'pasteAsInstance',
      '-',
      'findLayer',
      'openMaskStack'
    ]
  },
  {
    label: 'Tools',
    when: 'stack',
    items: [
      'brushTool',
      'eraserTool',
      'smudgeTool',
      'cloneTool',
      'projectionTool',
      'fillTool',
      'pickerTool',
      'lineTool',
      'pathTool',
      '-',
      'setCloneSource',
      'toggleStencil',
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
      'findNode',
      'groupNodes',
      '-',
      'bypassNodes',
      'deleteRelink',
      'swapInputs',
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
      { submenu: 'Wires', items: ['curvedWires', 'angledWires', 'straightWires'] },
      'toggleGraphSnap',
      'toggleGraphParameters',
      '-',
      'publishPackage',
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
      'zoomOut',
      '-',
      'toggleWireframe',
      'toggleGrid',
      '-',
      'saveScreenshot'
    ]
  },
  {
    label: 'Window',
    items: [
      { submenu: 'Layout', items: ['layout1', 'layout2', 'layout3', 'layout4', '-', 'resetLayout'] },
      'showHistory',
      'showChecks',
      'showActivity',
      '-',
      { native: 'windowItems' }
    ]
  },
  { label: 'Help', items: ['documentation', 'shortcuts', '-', 'diagnostics', 'reportProblem', '-', 'about'] }
];

/** What has focus when a control answers a key itself: a list or tree, the graph canvas, or a text field. */
export type FixedFocus = 'list' | 'canvas' | 'text';

/** A key a control answers itself, as its kind of control does everywhere. Listed, but not rebound. */
export interface FixedKey {
  readonly section: string;
  readonly label: string;
  /** The keys as the shortcuts dialog shows them, `Mod` being the platform's command key. */
  readonly keys: string;
  /** The chords it takes, which no command may have where its keys work while such a control has focus. */
  readonly chords: readonly Chord[];
  readonly focus: FixedFocus;
  /** The command it carries out, as Delete in a list deletes: that command may have the key too. */
  readonly command?: string;
}

const Arrows: readonly Chord[] = ['Up', 'Down', 'Left', 'Right'];

/**
 * Keys a control answers itself. Enter and Space have no chords here: they press whatever control has focus, everywhere
 * ({@link Commands.press}), so a command on them is one that runs when nothing that takes them has focus.
 */
export const FixedKeys: readonly FixedKey[] = [
  {
    section: 'Lists and trees',
    label: 'Move',
    keys: '↑ ↓, Home, End, Page Up, Page Down',
    chords: ['Up', 'Down', 'Home', 'End', 'PageUp', 'PageDown'],
    focus: 'list'
  },
  {
    section: 'Lists and trees',
    label: 'Open or close a folder',
    keys: '→ ←',
    chords: ['Right', 'Left'],
    focus: 'list'
  },
  { section: 'Lists and trees', label: 'Open or choose', keys: 'Enter', chords: [], focus: 'list' },
  { section: 'Lists and trees', label: 'Rename in the Library', keys: 'F2', chords: ['F2'], focus: 'list' },
  {
    section: 'Lists and trees',
    label: 'Delete a Library folder',
    keys: 'Delete',
    chords: ['Delete', 'Backspace'],
    focus: 'list',
    command: 'delete'
  },
  {
    section: 'Lists and trees',
    label: 'Context menu',
    keys: 'Shift+F10, Menu key',
    chords: ['Shift+F10', 'ContextMenu'],
    focus: 'list'
  },
  {
    section: 'Graph canvas',
    label: 'Select all nodes',
    keys: 'Mod+A',
    chords: ['Mod+A'],
    focus: 'canvas',
    command: 'selectAll'
  },
  {
    section: 'Graph canvas',
    label: 'Delete the selection',
    keys: 'Delete',
    chords: ['Delete', 'Backspace'],
    focus: 'canvas',
    command: 'delete'
  },
  {
    section: 'Graph canvas',
    label: 'Move the selection',
    keys: 'Arrows, Shift for a grid step',
    chords: [...Arrows, ...Arrows.map(arrow => `Shift+${arrow}`)],
    focus: 'canvas'
  },
  { section: 'Graph canvas', label: 'Clear the selection', keys: 'Escape', chords: ['Escape'], focus: 'canvas' },
  {
    section: 'Graph canvas',
    label: 'Bend a wire through a reroute dot',
    keys: 'Alt+click the wire',
    chords: [],
    focus: 'canvas'
  },
  { section: 'Text', label: 'Commit or cancel', keys: 'Enter, Escape', chords: ['Escape'], focus: 'text' }
];

/**
 * The keys the desktop app's own menu items take before the window sees them, as `apps/desktop/src/menu.ts` and
 * Electron's menu roles give them. No command can have one: on a Mac, ⌘Q would quit whatever it was bound to. Reload
 * and the developer tools are only in a development build, but are kept from every build so a keys file works in both.
 */
export function reservedKeys(mac: boolean): readonly { readonly chord: Chord; readonly label: string }[] {
  return mac
    ? [
        { chord: 'Mod+Q', label: 'Quit Patina' },
        { chord: 'Mod+H', label: 'Hide Patina' },
        { chord: 'Mod+Alt+H', label: 'Hide Others' },
        { chord: 'Mod+M', label: 'Minimise' },
        { chord: 'Mod+Shift+W', label: 'Close Window' },
        { chord: 'Mod+Ctrl+F', label: 'Full Screen' },
        { chord: 'Mod+R', label: 'Reload' },
        { chord: 'Mod+Alt+I', label: 'Developer Tools' }
      ]
    : [
        { chord: 'Mod+Q', label: 'Quit Patina' },
        { chord: 'Mod+M', label: 'Minimise' },
        { chord: 'F11', label: 'Full Screen' },
        { chord: 'Mod+R', label: 'Reload' },
        { chord: 'Mod+Shift+I', label: 'Developer Tools' }
      ];
}

/** Whether a command's keys work while a control of this kind has focus, which is when a fixed key would clash. */
export function reachesFocus(spec: CommandSpec, focus: FixedFocus): boolean {
  const area = spec.area ?? 'app';
  switch (focus) {
    // A text field keeps its keys, except a command's that works while typing.
    case 'text':
      return spec.typing === true;
    // The canvas shows only with a graph in front.
    case 'canvas':
      return area === 'app' || area === 'graph';
    // Lists are in every kind of document, but the viewport's keys need the viewport focused.
    case 'list':
      return area !== 'viewport';
  }
}

/** What a drag in a 3D or 2D view does, by button and modifier, in one preset. */
export interface Navigation {
  /** Words for the shortcuts dialog. */
  readonly orbit: string;
  readonly pan: string;
  readonly zoom: string;
  /** Which way a drag goes. `painting` is whether a left drag would paint. */
  drag(
    event: { button: number; altKey: boolean; shiftKey: boolean; ctrlKey: boolean; metaKey: boolean },
    painting: boolean
  ): 'stroke' | 'orbit' | 'pan' | 'zoom';
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
    {
      readonly label: string;
      readonly navigation: Navigation;
      readonly keys: Readonly<Record<string, readonly Chord[]>>;
    }
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
