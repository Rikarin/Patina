import { Menu, type MenuItemConstructorOptions } from 'electron';
import { basename } from 'node:path';

/** What the menus do in the main process. Everything else is a command the studio carries out. */
export interface MenuActions {
  recent: string[];
  /** Keeps Reload and the developer tools out of a packaged app, where they would throw away open work. */
  development: boolean;
  newLibrary(): Promise<void>;
  openLibrary(): Promise<void>;
  openRecent(path: string): Promise<void>;
  /** A command the studio carries out, and whether its key sent it rather than a click. */
  command(name: string, accelerator: boolean): void;
}

/** Items only the desktop app has, which the studio places by name. */
export type NativeEntry = 'recent' | 'closeWindow' | 'fullScreen' | 'windowItems' | 'development';

/**
 * One item of the studio's menus (typed again in apps/studio/src/app/core/desktop.ts, across the bridge). The studio
 * holds the command table, so it says what each item is called, which key it shows and whether it is on (#188, #191).
 */
export type MenuModelItem =
  | {
      type: 'command';
      id: string;
      label: string;
      accelerator?: string;
      registered?: boolean;
      enabled: boolean;
      checked?: boolean;
    }
  | { type: 'separator' }
  | { type: 'submenu'; label: string; items: MenuModelItem[] }
  | { type: 'native'; native: NativeEntry };

export type MenuModel = { label: string; items: MenuModelItem[] }[];

const Natives = new Set<string>(['recent', 'closeWindow', 'fullScreen', 'windowItems', 'development']);
const MaxItems = 400;
const MaxDepth = 3;

/**
 * The studio's menus, kept to what a menu can hold, or null if they are not menus. The renderer is the studio's, but a
 * menu is the main process's, so nothing it sends becomes a role or a click of anything else.
 */
export function readMenuModel(value: unknown): MenuModel | null {
  let count = 0;
  const text = (held: unknown, limit = 80): string | null =>
    typeof held === 'string' && held.length > 0 && held.length <= limit ? held : null;
  const item = (held: unknown, depth: number): MenuModelItem | null => {
    if (++count > MaxItems || typeof held !== 'object' || held === null) {
      return null;
    }

    const entry = held as Record<string, unknown>;
    switch (entry['type']) {
      case 'separator':
        return { type: 'separator' };
      case 'native':
        return typeof entry['native'] === 'string' && Natives.has(entry['native'])
          ? { type: 'native', native: entry['native'] as NativeEntry }
          : null;
      case 'submenu': {
        const label = text(entry['label']);
        const items = depth < MaxDepth ? list(entry['items'], depth + 1) : null;
        return label && items ? { type: 'submenu', label, items } : null;
      }
      case 'command': {
        const id = text(entry['id'], 64);
        const label = text(entry['label'], 120);
        const accelerator = text(entry['accelerator'], 40);
        if (!id || !/^[A-Za-z0-9]+$/u.test(id) || !label) {
          return null;
        }

        return {
          type: 'command',
          id,
          label,
          enabled: entry['enabled'] === true,
          ...(accelerator && /^[A-Za-z0-9+,./;'`[\]\\=-]+$/u.test(accelerator)
            ? { accelerator, registered: entry['registered'] === true }
            : {}),
          ...(typeof entry['checked'] === 'boolean' ? { checked: entry['checked'] } : {})
        };
      }
      default:
        return null;
    }
  };
  const list = (held: unknown, depth: number): MenuModelItem[] | null => {
    if (!Array.isArray(held)) {
      return null;
    }

    const items = held.map(entry => item(entry, depth));
    return items.every(entry => entry !== null) ? (items) : null;
  };

  if (!Array.isArray(value)) {
    return null;
  }

  const menus = value.map(menu => {
    const held = (typeof menu === 'object' && menu !== null ? menu : {}) as Record<string, unknown>;
    const label = text(held['label']);
    const items = list(held['items'], 1);
    return label && items ? { label, items } : null;
  });
  return menus.every(menu => menu !== null) ? (menus) : null;
}

/** The menus before the studio has said what they are, or after it has gone: the app's own items, nothing to act on. */
export const StartingMenus: MenuModel = [
  {
    label: 'File',
    items: [
      { type: 'command', id: 'newLibrary', label: 'New Library…', enabled: true },
      { type: 'command', id: 'openLibrary', label: 'Open Library…', enabled: true },
      { type: 'native', native: 'recent' },
      { type: 'separator' },
      { type: 'native', native: 'closeWindow' }
    ]
  },
  { label: 'View', items: [{ type: 'native', native: 'development' }, { type: 'native', native: 'fullScreen' }] },
  { label: 'Window', items: [{ type: 'native', native: 'windowItems' }] }
];

export function installMenu(actions: MenuActions, menus: MenuModel | null): void {
  Menu.setApplicationMenu(Menu.buildFromTemplate(template(actions, menus ?? StartingMenus, menus === null)));
}

/**
 * The menu bar. `starting` is before the studio has said what its menus are: then New Library and Open Library are the
 * main process's own, since there may be no studio to send them to.
 */
export function template(actions: MenuActions, menus: MenuModel, starting = false): MenuItemConstructorOptions[] {
  const mac = process.platform === 'darwin';
  const native = (entry: NativeEntry): MenuItemConstructorOptions[] => {
    switch (entry) {
      case 'recent':
        return [
          {
            label: 'Open Recent',
            submenu: actions.recent.length
              ? actions.recent.map(path => ({
                  label: basename(path, '.ptlib'),
                  toolTip: path,
                  click: () => void actions.openRecent(path)
                }))
              : [{ label: 'No recent libraries', enabled: false }]
          }
        ];
      case 'closeWindow':
        // Cmd+W closes the document in front, as in other editors, so the window takes Shift.
        return [mac ? { role: 'close', accelerator: 'Shift+CmdOrCtrl+W' } : { role: 'quit' }];
      case 'fullScreen':
        return [{ role: 'togglefullscreen' }];
      case 'windowItems':
        return mac ? [{ role: 'minimize' }, { role: 'zoom' }, { type: 'separator' }, { role: 'front' }] : [{ role: 'minimize' }];
      case 'development':
        return actions.development ? [{ role: 'reload' }, { role: 'toggleDevTools' }, { type: 'separator' }] : [];
    }
  };
  const build = (items: readonly MenuModelItem[]): MenuItemConstructorOptions[] =>
    items.flatMap((item): MenuItemConstructorOptions[] => {
      switch (item.type) {
        case 'separator':
          return [{ type: 'separator' }];
        case 'native':
          return native(item.native);
        case 'submenu':
          return [{ label: item.label, submenu: build(item.items) }];
        case 'command':
          return [
            {
              id: item.id,
              label: item.label,
              enabled: item.enabled,
              ...(item.checked === undefined ? {} : { type: 'checkbox' as const, checked: item.checked }),
              ...(item.accelerator
                ? // A key without the command key is shown, not taken: a text field has to keep typing it.
                  { accelerator: item.accelerator, registerAccelerator: item.registered === true }
                : {}),
              click: (_menuItem, _window, event) => {
                if (starting && item.id === 'newLibrary') {
                  void actions.newLibrary();
                } else if (starting && item.id === 'openLibrary') {
                  void actions.openLibrary();
                } else {
                  actions.command(item.id, event.triggeredByAccelerator === true);
                }
              }
            }
          ];
      }
    });

  return [
    ...(mac ? [{ role: 'appMenu' as const }] : []),
    ...menus.map(
      (menu): MenuItemConstructorOptions => ({
        label: menu.label,
        ...(menu.label === 'Help' ? { role: 'help' as const } : {}),
        submenu: build(menu.items)
      })
    )
  ];
}
