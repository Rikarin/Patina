import { contextBridge, ipcRenderer, webUtils } from 'electron';

// The only surface the sandboxed renderer gets (typed in apps/studio/src/app/core/desktop.ts).
contextBridge.exposeInMainWorld('patina', {
  platform: process.platform,
  onFullScreen: (listener: (fullScreen: boolean) => void) => {
    ipcRenderer.on('patina:fullScreen', (_event, fullScreen: boolean) => listener(fullScreen));
  },
  connection: () => ipcRenderer.invoke('patina:connection'),
  sidecars: () => ipcRenderer.invoke('patina:sidecars'),
  onSidecars: (listener: (statuses: unknown) => void) => {
    ipcRenderer.on('patina:sidecars', (_event, statuses: unknown) => listener(statuses));
  },
  logs: () => ipcRenderer.invoke('patina:sidecars:logs'),
  restartEngine: () => ipcRenderer.invoke('patina:sidecars:restart'),
  recentLibraries: () => ipcRenderer.invoke('patina:libraries:recent'),
  openLibrary: () => ipcRenderer.invoke('patina:libraries:open'),
  newLibrary: () => ipcRenderer.invoke('patina:libraries:new'),
  openRecentLibrary: (path: string) => ipcRenderer.invoke('patina:libraries:openRecent', path),
  setTitle: (title: string) => {
    ipcRenderer.send('patina:title', title);
  },
  reveal: (path: string) => ipcRenderer.invoke('patina:reveal', path),
  openFile: (pictures?: readonly string[], meshes?: readonly string[]) =>
    ipcRenderer.invoke('patina:openFile', pictures, meshes),
  openEnvironment: (formats?: readonly string[]) => ipcRenderer.invoke('patina:openEnvironment', formats),
  openLook: () => ipcRenderer.invoke('patina:openLook'),
  openPreset: () => ipcRenderer.invoke('patina:openPreset'),
  pathOf: (file: File) => webUtils.getPathForFile(file),
  editText: (edit: string) => ipcRenderer.send('patina:editText', edit),
  onCommand: (listener: (command: string, accelerator: boolean) => void) => {
    ipcRenderer.on('patina:command', (_event, command: string, accelerator?: boolean) =>
      listener(command, accelerator === true)
    );
  },
  setMenu: (menus: unknown) => {
    ipcRenderer.send('patina:menu', menus);
  },
  readKeys: () => ipcRenderer.invoke('patina:keys:read'),
  writeKeys: (settings: unknown) => ipcRenderer.invoke('patina:keys:write', settings),
  readPreferences: () => ipcRenderer.invoke('patina:preferences:read'),
  writePreferences: (settings: unknown) => ipcRenderer.invoke('patina:preferences:write', settings),
  openMesh: (formats?: readonly string[]) => ipcRenderer.invoke('patina:openMesh', formats),
  openImage: (formats?: readonly string[]) => ipcRenderer.invoke('patina:openImage', formats),
  openFolder: () => ipcRenderer.invoke('patina:openFolder'),
  saveScreenshot: () => ipcRenderer.invoke('patina:saveScreenshot'),
  setUnsaved: (unsaved: boolean) => {
    ipcRenderer.send('patina:unsaved', unsaved);
  },
  onQuitRequested: (listener: () => void) => {
    ipcRenderer.on('patina:quit-requested', () => listener());
  },
  quit: () => {
    ipcRenderer.send('patina:quit');
  }
});
