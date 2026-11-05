import { contextBridge, ipcRenderer } from 'electron';

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
  openMesh: () => ipcRenderer.invoke('patina:openMesh'),
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
