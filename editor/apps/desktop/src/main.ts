import {
  app,
  BrowserWindow,
  dialog,
  ipcMain,
  net,
  protocol,
  session,
  shell,
  type IpcMainEvent,
  type IpcMainInvokeEvent,
  type OpenDialogOptions
} from 'electron';
import { existsSync, statSync } from 'node:fs';
import { extname, join, normalize, resolve, sep } from 'node:path';
import { pathToFileURL } from 'node:url';
import { contentSecurityPolicy } from './csp';
import { LibraryPlace, type LibraryChoice } from './libraries';
import { installMenu } from './menu';
import { Sidecars } from './sidecars';

// Patina desktop: the studio in a sandboxed window, and the backend (patina-server + patina-worker) as supervised
// sidecars on loopback. The app is one client of the Server's API: nothing in that API knows about Electron (plan §8).

app.setName('Patina');
if (process.env['PATINA_USER_DATA']) {
  app.setPath('userData', process.env['PATINA_USER_DATA']); // tests run with throwaway settings
}

/** One window per settings folder. Launching the app again brings the running one to the front instead. */
const primary = app.requestSingleInstanceLock();
if (!primary) {
  app.quit();
}

/** The renderer comes from the built studio through this scheme; nothing else is served from disk. */
const Scheme = 'app';
/** The app icon (apps/desktop/assets/icon.png), copied next to main.js by the build. */
const Icon = join(__dirname, 'icon.png');
const studioRoot = resolve(
  (app.isPackaged ? undefined : process.env['PATINA_STUDIO_DIR']) ??
    join(__dirname, app.isPackaged ? 'studio' : '../studio/browser')
);
const devServer = app.isPackaged ? undefined : process.env['PATINA_STUDIO_URL']; // `nx serve studio` during development

/** Whether a page is the studio itself, compared by origin: a prefix would also pass `http://localhost:42000.evil`. */
function isStudio(url: string): boolean {
  if (url.startsWith(`${Scheme}://`)) {
    return true;
  }

  try {
    return !!devServer && new URL(url).origin === new URL(devServer).origin;
  } catch {
    return false;
  }
}

/** Only the studio's own top frame may use the bridge. */
function fromStudio(event: IpcMainEvent | IpcMainInvokeEvent): boolean {
  return event.senderFrame?.parent === null && isStudio(event.senderFrame.url);
}

function handle(channel: string, answer: (event: IpcMainInvokeEvent, ...args: unknown[]) => unknown): void {
  ipcMain.handle(channel, (event, ...args: unknown[]) => {
    if (!fromStudio(event)) {
      throw new Error(`${channel} is only for the studio.`);
    }

    return answer(event, ...args);
  });
}

function listen(channel: string, heard: (event: IpcMainEvent, ...args: unknown[]) => void): void {
  ipcMain.on(channel, (event, ...args: unknown[]) => {
    if (fromStudio(event)) {
      heard(event, ...args);
    }
  });
}

protocol.registerSchemesAsPrivileged([
  { scheme: Scheme, privileges: { standard: true, secure: true, supportFetchAPI: true, corsEnabled: true } }
]);

let sidecars: Sidecars | null = null;
let window: BrowserWindow | null = null;
const QuitDeadlineMs = 8_000;
let quitting = false;
/** What the renderer last said: whether quitting now would lose edits. */
let unsaved = false;
/** The user has answered, so the next quit goes through. */
let quitConfirmed = false;

function backend(): Sidecars {
  // PATINA_LIBRARY is for scripts and tests. People get the library they last had open.
  sidecars ??= new Sidecars(startingLibrary(), statuses => window?.webContents.send('patina:sidecars', statuses));
  return sidecars;
}

/** The library to start in. One that has gone is said so once, and the app starts with none. */
function startingLibrary(): string | null {
  const asked = process.env['PATINA_LIBRARY'];
  if (asked && !LibraryPlace.isLibrary(asked)) {
    dialog.showErrorBox('This is not a library', `${asked} has no library.yaml.`);
    return null;
  }

  return asked || askedToOpen || places().last();
}

let libraryPlaces: LibraryPlace | null = null;

function places(): LibraryPlace {
  libraryPlaces ??= new LibraryPlace(join(app.getPath('userData'), 'libraries.json'));
  return libraryPlaces;
}

/** Whether going elsewhere may go ahead: the user decides what becomes of edits that are not saved. */
async function mayLeave(): Promise<boolean> {
  if (!unsaved) {
    return true;
  }

  const options = {
    type: 'warning' as const,
    message: 'Leave without saving?',
    detail: 'Something open has changes that are not saved, and they will be lost.',
    buttons: ['Cancel', 'Leave'],
    defaultId: 0,
    cancelId: 0
  };
  const { response } = await (window ? dialog.showMessageBox(window, options) : dialog.showMessageBox(options));
  return response === 1;
}

/** Moves the app to a library, and reloads the studio, which reads everything from the new Server. */
async function openLibrary(choice: LibraryChoice | null): Promise<string | null> {
  if (choice === null || !(await mayLeave())) {
    return null;
  }

  await backend().useLibrary(choice.path, choice.create);
  places().remember(choice.path);
  unsaved = false;
  rebuildMenu();
  window?.webContents.reload();
  return choice.path;
}

async function askForLibrary(): Promise<LibraryChoice | null> {
  const options: OpenDialogOptions = { title: 'Open Library', properties: ['openDirectory'] };
  const picked = await (window ? dialog.showOpenDialog(window, options) : dialog.showOpenDialog(options));
  const path = picked.canceled ? null : (picked.filePaths[0] ?? null);
  if (path === null) {
    return null;
  }

  if (!LibraryPlace.isLibrary(path)) {
    dialog.showErrorBox('This is not a library', `${path} has no library.yaml.`);
    return null;
  }

  return { path, create: false };
}

async function askForNewLibrary(): Promise<LibraryChoice | null> {
  const options = {
    title: 'New Library',
    buttonLabel: 'Create',
    defaultPath: 'Game.ptlib',
    properties: ['createDirectory' as const]
  };
  const picked = await (window ? dialog.showSaveDialog(window, options) : dialog.showSaveDialog(options));
  if (picked.canceled || !picked.filePath) {
    return null;
  }

  const path = LibraryPlace.named(picked.filePath);
  return { path, create: !LibraryPlace.isLibrary(path) };
}

function openRecent(path: string): Promise<string | null> {
  if (!LibraryPlace.isLibrary(path)) {
    places().forget(path);
    rebuildMenu();
    dialog.showErrorBox('This library is gone', `${path} is no longer there.`);
    return Promise.resolve(null);
  }

  return openLibrary({ path, create: false });
}

function rebuildMenu(): void {
  installMenu({
    recent: places().recent(),
    development: !app.isPackaged,
    newLibrary: async () => {
      await openLibrary(await askForNewLibrary());
    },
    openLibrary: async () => {
      await openLibrary(await askForLibrary());
    },
    openRecent: async path => {
      await openRecent(path);
    },
    command: command => window?.webContents.send('patina:command', command)
  });
}

function serveStudio(): void {
  protocol.handle(Scheme, async request => {
    const { pathname } = new URL(request.url);
    const file = normalize(join(studioRoot, decodeURIComponent(pathname)));
    // Refuse anything outside the studio build. Paths without an extension, like the root a reload asks for, get the
    // app shell; a missing file is a 404, so a page older than the build fails to load a chunk instead of being handed
    // the shell as JavaScript.
    const inside = file.startsWith(studioRoot + sep);
    if (inside && extname(file) && !isFile(file)) {
      return new Response('Not found', { status: 404 });
    }

    const target = inside && isFile(file) ? file : join(studioRoot, 'index.html');
    const response = await net.fetch(pathToFileURL(target).toString());
    const headers = new Headers(response.headers);
    headers.set('Content-Security-Policy', contentSecurityPolicy());
    return new Response(response.body, { status: response.status, headers });
  });
}

function isFile(path: string): boolean {
  return statSync(path, { throwIfNoEntry: false })?.isFile() ?? false;
}

function createWindow(): void {
  window = new BrowserWindow({
    width: 1440,
    height: 900,
    minWidth: 960,
    minHeight: 600,
    title: 'Patina',
    backgroundColor: '#111113',
    show: false,
    icon: Icon,
    // On macOS the studio's top bar is the title bar: no system bar, just the traffic lights, centred in its 40 px.
    ...(process.platform === 'darwin'
      ? { titleBarStyle: 'hidden' as const, trafficLightPosition: { x: 12, y: 13 } }
      : {}),
    webPreferences: {
      preload: join(__dirname, 'preload.js'),
      contextIsolation: true,
      sandbox: true,
      nodeIntegration: false,
      spellcheck: false
    }
  });

  window.once('ready-to-show', () => window?.show());
  // Full screen hides the traffic lights, so the top bar can give their space back.
  window.on('enter-full-screen', () => window?.webContents.send('patina:fullScreen', true));
  window.on('leave-full-screen', () => window?.webContents.send('patina:fullScreen', false));
  // The window never navigates away from the studio; links to the web open in the browser.
  window.webContents.on('will-navigate', (event, url) => {
    if (!isStudio(url)) {
      event.preventDefault();
    }
  });
  window.webContents.setWindowOpenHandler(({ url }) => {
    if (url.startsWith('https://')) {
      shell.openExternal(url).catch((error: unknown) => console.error('Patina: could not open', url, error));
    }

    return { action: 'deny' };
  });
  window.on('close', event => {
    if (holdForUnsaved()) {
      event.preventDefault();
    }
  });
  window.on('closed', () => {
    window = null;
    unsaved = false;
  });
  window
    .loadURL(devServer ?? `${Scheme}://studio/index.html`)
    .catch((error: unknown) => console.error('Patina: the studio did not load', error));
}

function handleIpc(): void {
  listen('patina:unsaved', (_event, value: unknown) => {
    unsaved = value === true;
  });
  listen('patina:editText', (event, edit: unknown) => {
    const contents = event.sender;
    if (edit === 'undo') {
      contents.undo();
    } else if (edit === 'redo') {
      contents.redo();
    } else if (edit === 'delete') {
      contents.delete();
    } else if (edit === 'selectAll') {
      contents.selectAll();
    } else if (edit === 'cut') {
      contents.cut();
    } else if (edit === 'copy') {
      contents.copy();
    } else if (edit === 'paste') {
      contents.paste();
    }
  });
  listen('patina:quit', () => {
    quitConfirmed = true;
    app.quit();
  });
  handle('patina:connection', () => backend().start());
  handle('patina:sidecars', () => sidecars?.statuses() ?? []);
  handle('patina:sidecars:logs', () => sidecars?.logs() ?? { worker: [], server: [] });
  handle('patina:sidecars:restart', async () => {
    await backend().restart();
    window?.webContents.reload(); // a new Server has a new address, and autosave offers back what was open
  });
  handle('patina:libraries:recent', () => places().recent());
  handle('patina:libraries:open', async () => openLibrary(await askForLibrary()));
  handle('patina:libraries:new', async () => openLibrary(await askForNewLibrary()));
  handle('patina:libraries:openRecent', (_event, path: unknown) =>
    typeof path === 'string' ? openRecent(path) : null
  );
  listen('patina:title', (_event, title: unknown) => {
    window?.setTitle(typeof title === 'string' && title.length > 0 ? `${title} — Patina` : 'Patina');
  });
  handle('patina:reveal', (_event, path: unknown) => {
    if (typeof path === 'string') {
      shell.showItemInFolder(path);
    }
  });
  handle('patina:openMesh', async () => {
    const options: OpenDialogOptions = {
      properties: ['openFile'],
      filters: [{ name: 'Meshes', extensions: ['glb', 'gltf', 'fbx', 'obj'] }]
    };
    const picked = await (window ? dialog.showOpenDialog(window, options) : dialog.showOpenDialog(options));
    return picked.canceled ? null : (picked.filePaths[0] ?? null);
  });
  handle('patina:openFile', async () => {
    const options: OpenDialogOptions = {
      title: 'Import',
      filters: [
        {
          name: 'Meshes and pictures',
          extensions: ['glb', 'gltf', 'fbx', 'obj', 'png', 'jpg', 'jpeg', 'tga', 'bmp', 'gif']
        }
      ],
      properties: ['openFile']
    };
    const picked = window ? await dialog.showOpenDialog(window, options) : await dialog.showOpenDialog(options);
    return picked.canceled ? null : (picked.filePaths[0] ?? null);
  });
  handle('patina:openEnvironment', async () => {
    const options: OpenDialogOptions = {
      title: 'Choose an Environment',
      properties: ['openFile'],
      filters: [{ name: 'Radiance panoramas', extensions: ['hdr'] }]
    };
    const picked = await (window ? dialog.showOpenDialog(window, options) : dialog.showOpenDialog(options));
    return picked.canceled ? null : (picked.filePaths[0] ?? null);
  });
  handle('patina:openImage', async () => {
    const options: OpenDialogOptions = {
      properties: ['openFile'],
      filters: [{ name: 'Pictures', extensions: ['png', 'jpg', 'jpeg', 'exr', 'tga'] }]
    };
    const picked = await (window ? dialog.showOpenDialog(window, options) : dialog.showOpenDialog(options));
    return picked.canceled ? null : (picked.filePaths[0] ?? null);
  });
  handle('patina:openFolder', async () => {
    const options: OpenDialogOptions = { properties: ['openDirectory', 'createDirectory'] };
    const picked = await (window ? dialog.showOpenDialog(window, options) : dialog.showOpenDialog(options));
    return picked.canceled ? null : (picked.filePaths[0] ?? null);
  });
}

/** A library the system asked to open before there was a window to open it in. */
let askedToOpen: string | null = process.argv.find(argument => argument.endsWith('.ptlib')) ?? null;

function openFromSystem(path: string): void {
  if (!LibraryPlace.isLibrary(path)) {
    dialog.showErrorBox('This is not a library', `${path} has no library.yaml.`);
  } else if (window) {
    void openLibrary({ path, create: false });
  } else {
    askedToOpen = path;
  }
}

// Finder double-clicks arrive here, and may do so before the app is ready.
app.on('open-file', (event, path) => {
  event.preventDefault();
  openFromSystem(path);
});

app.on('second-instance', (_event, argv) => {
  const library = argv.find(argument => argument.endsWith('.ptlib'));
  if (library) {
    openFromSystem(library);
  }

  if (window) {
    if (window.isMinimized()) {
      window.restore();
    }

    window.show();
    window.focus();
  }
});

app
  .whenReady()
  .then(() => {
    if (!primary) {
      return;
    }

    rebuildMenu();
    // Unpackaged, macOS would show Electron's own icon in the Dock.
    if (existsSync(Icon)) {
      app.dock?.setIcon(Icon);
    }

    // No camera, microphone, notifications or anything else: the studio needs none of it.
    session.defaultSession.setPermissionRequestHandler((_contents, _permission, callback) => callback(false));
    serveStudio();
    handleIpc();
    createWindow();
    app.on('activate', () => {
      if (BrowserWindow.getAllWindows().length === 0) {
        createWindow();
      }
    });
  })
  .catch((error: unknown) => console.error('Patina: startup failed', error));

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

/** With edits that would be lost, asks the renderer to ask the user, and says that it has. */
function holdForUnsaved(): boolean {
  // A renderer that has crashed cannot answer, and its edits are safe on the Server only until it stops anyway.
  if (!unsaved || quitConfirmed || quitting || !window || window.webContents.isCrashed()) {
    return false;
  }

  window.webContents.send('patina:quit-requested');
  return true;
}

app.on('before-quit', event => {
  if (holdForUnsaved()) {
    event.preventDefault();
    return;
  }

  if (quitting || !sidecars) {
    return;
  }

  // Stop the backend cleanly (the Server leaves the discovery file and deletes its socket) and really quit.
  event.preventDefault();
  quitting = true;
  // A process that will not die must not keep the app on screen: each is killed at 3 s, so this is already wrong.
  setTimeout(() => {
    app.exit(0);
  }, QuitDeadlineMs).unref();
  void sidecars.stop().finally(() => app.quit());
});
