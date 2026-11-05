import { BrowserWindow, type OpenDialogOptions, app, dialog, ipcMain, net, protocol, session, shell } from 'electron';
import { existsSync, statSync } from 'node:fs';
import { extname, join, normalize, resolve, sep } from 'node:path';
import { pathToFileURL } from 'node:url';
import { contentSecurityPolicy } from './csp';
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
  process.env['PATINA_STUDIO_DIR'] ?? join(__dirname, app.isPackaged ? 'studio' : '../studio/browser')
);
const devServer = process.env['PATINA_STUDIO_URL']; // `nx serve studio` during development

protocol.registerSchemesAsPrivileged([
  { scheme: Scheme, privileges: { standard: true, secure: true, supportFetchAPI: true, corsEnabled: true } }
]);

let sidecars: Sidecars | null = null;
let window: BrowserWindow | null = null;
let quitting = false;
/** What the renderer last said: whether quitting now would lose edits. */
let unsaved = false;
/** The user has answered, so the next quit goes through. */
let quitConfirmed = false;

function backend(): Sidecars {
  sidecars ??= new Sidecars(process.env['PATINA_LIBRARY'] ?? null, statuses =>
    window?.webContents.send('patina:sidecars', statuses)
  );
  return sidecars;
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
    if (!url.startsWith(`${Scheme}://`) && !(devServer && url.startsWith(devServer))) {
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
  ipcMain.on('patina:unsaved', (_event, value: unknown) => {
    unsaved = value === true;
  });
  ipcMain.on('patina:quit', () => {
    quitConfirmed = true;
    app.quit();
  });
  ipcMain.handle('patina:connection', () => backend().start());
  ipcMain.handle('patina:sidecars', () => sidecars?.statuses() ?? []);
  ipcMain.handle('patina:sidecars:logs', () => sidecars?.logs() ?? { worker: [], server: [] });
  ipcMain.handle('patina:openMesh', async () => {
    const options: OpenDialogOptions = {
      properties: ['openFile'],
      filters: [{ name: 'Meshes', extensions: ['glb', 'gltf', 'fbx', 'obj'] }]
    };
    const picked = await (window ? dialog.showOpenDialog(window, options) : dialog.showOpenDialog(options));
    return picked.canceled ? null : (picked.filePaths[0] ?? null);
  });
}

app.on('second-instance', () => {
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
  void sidecars.stop().finally(() => app.quit());
});
