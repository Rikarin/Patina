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
        { label: 'Import…', accelerator: 'CmdOrCtrl+I', click: () => actions.command('import') },
        { label: 'Reload Mesh', accelerator: 'CmdOrCtrl+Shift+R', click: () => actions.command('reloadMesh') },
        { label: 'Project Settings…', accelerator: 'CmdOrCtrl+Shift+,', click: () => actions.command('projectSettings') },
        { type: 'separator' },
        { label: 'Save', accelerator: 'CmdOrCtrl+S', click: () => actions.command('save') },
        { label: 'Save As…', accelerator: 'Shift+CmdOrCtrl+S', click: () => actions.command('saveAs') },
        { type: 'separator' },
        mac ? { role: 'close' } : { role: 'quit' }
      ]
    },
    {
      // Not the stock Edit menu: its Undo only reaches text fields, and a menu takes these keys before any page sees
      // them, so the studio decides what each one means for whatever is in front.
      label: 'Edit',
      submenu: [
        { id: 'edit-undo', label: 'Undo', accelerator: 'CmdOrCtrl+Z', click: () => actions.command('undo') },
        { id: 'edit-redo', label: 'Redo', accelerator: 'Shift+CmdOrCtrl+Z', click: () => actions.command('redo') },
        { type: 'separator' },
        { id: 'edit-cut', label: 'Cut', accelerator: 'CmdOrCtrl+X', click: () => actions.command('cut') },
        { id: 'edit-copy', label: 'Copy', accelerator: 'CmdOrCtrl+C', click: () => actions.command('copy') },
        { id: 'edit-paste', label: 'Paste', accelerator: 'CmdOrCtrl+V', click: () => actions.command('paste') },
        {
          id: 'edit-delete',
          label: 'Delete',
          accelerator: 'CmdOrCtrl+Backspace',
          // Shown, not taken: in a text field the key has to go on meaning what it means there.
          registerAccelerator: false,
          click: () => actions.command('delete')
        },
        {
          id: 'edit-select-all',
          label: 'Select All',
          accelerator: 'CmdOrCtrl+A',
          click: () => actions.command('selectAll')
        }
      ]
    },
    {
      label: 'View',
      submenu: [
        ...(actions.development
          ? ([{ role: 'reload' }, { role: 'toggleDevTools' }, { type: 'separator' }] as MenuItemConstructorOptions[])
          : []),
        { label: 'Show Statistics', click: () => actions.command('toggleStats') },
        { label: 'Reset Layout', click: () => actions.command('resetLayout') },
        { label: 'Keyboard Shortcuts…', click: () => actions.command('shortcuts') },
        { label: 'Diagnostics…', click: () => actions.command('diagnostics') },
        { type: 'separator' },
        { role: 'togglefullscreen' }
      ]
    },
    { role: 'windowMenu' }
  ];
}
