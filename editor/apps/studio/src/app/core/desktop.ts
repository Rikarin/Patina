import type { Connection } from '@patina/api-client';

/** One supervised process of the desktop app (apps/desktop/src/sidecars.ts). */
export interface SidecarStatus {
  name: 'worker' | 'server';
  state: 'starting' | 'running' | 'restarting' | 'stopped' | 'failed';
  detail: string | null;
  pid: number | null;
}

/** What the desktop app's preload exposes. Undefined in a plain browser, where the studio talks to a dev Server. */
export interface DesktopBridge {
  platform: string;
  connection(): Promise<Connection>;
  onFullScreen(listener: (fullScreen: boolean) => void): void;
  sidecars(): Promise<SidecarStatus[]>;
  onSidecars(listener: (statuses: SidecarStatus[]) => void): void;
  logs(): Promise<Record<SidecarStatus['name'], string[]>>;
}

/** The desktop bridge, or undefined outside Electron. */
export function desktop(): DesktopBridge | undefined {
  return (globalThis as { patina?: DesktopBridge }).patina;
}
