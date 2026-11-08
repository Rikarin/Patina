/** What a text field can be told to do. */
export type TextEdit = 'undo' | 'redo' | 'delete' | 'selectAll' | 'cut' | 'copy' | 'paste';

import type { Connection } from '@patina/api-client';

/** One supervised process of the desktop app (apps/desktop/src/sidecars.ts). */
export interface SidecarStatus {
  name: 'worker' | 'server';
  state: 'starting' | 'running' | 'restarting' | 'stopped' | 'failed';
  detail: string | null;
  pid: number | null;
}

/** One native menu item, as the studio describes it (apps/desktop/src/menu.ts builds it). */
export type MenuModelItem =
  | {
      type: 'command';
      id: string;
      label: string;
      /** Electron's accelerator, shown beside the item. */
      accelerator?: string;
      /** Whether the menu takes the key itself, which only a key with the command key does. */
      registered?: boolean;
      enabled: boolean;
      checked?: boolean;
    }
  | { type: 'separator' }
  | { type: 'submenu'; label: string; items: MenuModelItem[] }
  | { type: 'native'; native: 'recent' | 'closeWindow' | 'fullScreen' | 'windowItems' | 'development' };

/** The menu bar, left to right, after the app menu a Mac has. */
export type MenuModel = { label: string; items: MenuModelItem[] }[];

/** What the desktop app's preload exposes. Undefined in a plain browser, where the studio talks to a dev Server. */
export interface DesktopBridge {
  platform: string;
  connection(): Promise<Connection>;
  onFullScreen(listener: (fullScreen: boolean) => void): void;
  sidecars(): Promise<SidecarStatus[]>;
  onSidecars(listener: (statuses: SidecarStatus[]) => void): void;
  logs(): Promise<Record<SidecarStatus['name'], string[]>>;
  /** Stops what runs behind the app and starts it again. */
  restartEngine(): Promise<void>;
  /** The libraries opened lately, newest first. */
  recentLibraries(): Promise<string[]>;
  /** Asks for a library and moves the app to it, reloading this page. Null if they cancelled. */
  openLibrary(): Promise<string | null>;
  /** Asks where a new library goes, lays it out and moves the app to it. Null if they cancelled. */
  newLibrary(): Promise<string | null>;
  /** Moves the app to a library opened before. Null if it has gone. */
  openRecentLibrary(path: string): Promise<string | null>;
  /** Says what is open, for the window's title. */
  setTitle(title: string): void;
  /** Shows a file in the system's file browser. */
  reveal(path: string): Promise<void>;
  /** Asks for a mesh or a picture of these formats: its path, or null if they cancelled. */
  openFile(pictures?: readonly string[]): Promise<string | null>;
  /** Asks for a panorama of these formats. */
  openEnvironment(formats?: readonly string[]): Promise<string | null>;
  /** Asks for a <code>.cube</code> table to view the viewport through. */
  openLook(): Promise<string | null>;
  /** Where a file dropped on the window is. */
  pathOf(file: File): string;

  /** Has the focused text field carry out an edit, which only the browser's own editor can do. */
  editText(edit: TextEdit): void;

  /**
   * A menu command for the studio, such as `newProject`, and whether its key sent it rather than a click: a key the
   * menu took first still keeps to the rules a key has in a text field.
   */
  onCommand(listener: (command: string, accelerator: boolean) => void): void;
  /** Replaces the native menus with these, which say what is on and what each key is (#188). */
  setMenu(menus: MenuModel): void;
  /** The person's key settings (`keys.json` in the app's settings folder), or null when there are none. */
  readKeys(): Promise<unknown>;
  /** Writes the person's key settings. */
  writeKeys(settings: unknown): Promise<void>;
  /** Asks the user for a mesh file: its path, or null if they cancelled. */
  openMesh(): Promise<string | null>;
  /** Asks the user for a picture of these formats: its path, or null if they cancelled. */
  openImage(formats?: readonly string[]): Promise<string | null>;
  /** Asks the user for a folder: its path, or null if they cancelled. */
  openFolder(): Promise<string | null>;
  /** Tells the app whether quitting would lose anything, which is what decides whether it asks. */
  setUnsaved(unsaved: boolean): void;
  /** The user asked to quit with something unsaved. The app waits for {@link quit}. */
  onQuitRequested(listener: () => void): void;
  quit(): void;
}

/** The desktop bridge, or undefined outside Electron. */
export function desktop(): DesktopBridge | undefined {
  return (globalThis as { patina?: DesktopBridge }).patina;
}
