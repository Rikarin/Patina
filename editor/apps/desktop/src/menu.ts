import { Menu, type MenuItemConstructorOptions } from 'electron';
import { basename } from 'node:path';

/** What the menus do, which is all in the main process or sent on to the studio. */
export interface MenuActions {
  recent: string[];
  /** Keeps Reload and the developer tools out of a packaged app, where they would throw away open work. */
  development: boolean;
  newLibrary(): Promise<void>;
  openLibrary(): Promise<void>;
  openRecent(path: string): Promise<void>;
  /** A command the studio carries out. */
  command(name: string): void;
}

export function installMenu(actions: MenuActions): void {
  Menu.setApplicationMenu(Menu.buildFromTemplate(template(actions)));
}

function template(actions: MenuActions): MenuItemConstructorOptions[] {
  const mac = process.platform === 'darwin';
  const recent: MenuItemConstructorOptions[] = actions.recent.length
    ? actions.recent.map(path => ({
        label: basename(path, '.ptlib'),
        toolTip: path,
        click: () => void actions.openRecent(path)
      }))
    : [{ label: 'No recent libraries', enabled: false }];
  return [
    ...(mac ? [{ role: 'appMenu' as const }] : []),
    {
      label: 'File',
      submenu: [
        { label: 'New Project…', accelerator: 'CmdOrCtrl+N', click: () => actions.command('newProject') },
        { label: 'New Folder', accelerator: 'CmdOrCtrl+Shift+N', click: () => actions.command('newFolder') },
        { type: 'separator' },
        { label: 'New Library…', click: () => void actions.newLibrary() },
        { label: 'Open Library…', accelerator: 'CmdOrCtrl+O', click: () => void actions.openLibrary() },
        { label: 'Open Recent', submenu: recent },
        { type: 'separator' },
        { label: 'Save', accelerator: 'CmdOrCtrl+S', click: () => actions.command('save') },
        { type: 'separator' },
        mac ? { role: 'close' } : { role: 'quit' }
      ]
    },
    { role: 'editMenu' },
    {
      label: 'View',
      submenu: [
        ...(actions.development
          ? ([{ role: 'reload' }, { role: 'toggleDevTools' }, { type: 'separator' }] as MenuItemConstructorOptions[])
          : []),
        { role: 'togglefullscreen' }
      ]
    },
    { role: 'windowMenu' }
  ];
}
