import { describe, expect, it, vi } from 'vitest';

vi.mock('electron', () => ({ Menu: {} }));

import { readMenuModel, template, type MenuActions } from './menu';

const actions = (): MenuActions & { sent: [string, boolean][] } => {
  const sent: [string, boolean][] = [];
  return {
    sent,
    recent: ['/libs/Game.ptlib'],
    development: false,
    newLibrary: vi.fn(() => Promise.resolve()),
    openLibrary: vi.fn(() => Promise.resolve()),
    openRecent: vi.fn(() => Promise.resolve()),
    command: (name, accelerator) => sent.push([name, accelerator])
  };
};

describe('menus from the studio', () => {
  it('keeps commands, lines, submenus and the app’s own items', () => {
    const model = [
      {
        label: 'Edit',
        items: [
          { type: 'command', id: 'undo', label: 'Undo Paint', accelerator: 'CmdOrCtrl+Z', registered: true, enabled: true },
          { type: 'separator' },
          { type: 'submenu', label: 'More', items: [{ type: 'command', id: 'cut', label: 'Cut', enabled: false }] },
          { type: 'native', native: 'recent' }
        ]
      }
    ];

    expect(readMenuModel(model)).toEqual(model);
  });

  it('refuses anything that is not a menu: roles, odd ids, odd keys, endless nesting', () => {
    const one = (item: unknown) => readMenuModel([{ label: 'File', items: [item] }]);
    expect(one({ type: 'role', role: 'quit' })).toBeNull();
    expect(one({ type: 'native', native: 'toggleDevTools' })).toBeNull();
    expect(one({ type: 'command', id: 'a b', label: 'A', enabled: true })).toBeNull();
    expect(one({ type: 'command', id: 'save', label: 'Save', enabled: true, accelerator: 'Cmd+<script>' })).toEqual([
      { label: 'File', items: [{ type: 'command', id: 'save', label: 'Save', enabled: true }] }
    ]);
    let deep: unknown = { type: 'command', id: 'x', label: 'X', enabled: true };
    for (let at = 0; at < 5; at++) {
      deep = { type: 'submenu', label: 'S', items: [deep] };
    }

    expect(one(deep)).toBeNull();
    expect(readMenuModel('menus')).toBeNull();
  });

  it('shows a key without the command key but does not take it, and says when a key sent a command', () => {
    const sent = actions();
    const built = template(sent, [
      {
        label: 'Tools',
        items: [
          { type: 'command', id: 'brushTool', label: 'Brush', accelerator: 'B', registered: false, enabled: true },
          { type: 'command', id: 'save', label: 'Save', accelerator: 'CmdOrCtrl+S', registered: true, enabled: true }
        ]
      }
    ]);
    const tools = built.find(menu => menu.label === 'Tools')?.submenu as {
      id: string;
      registerAccelerator: boolean;
      click: (item: unknown, window: unknown, event: { triggeredByAccelerator?: boolean }) => void;
    }[];

    expect(tools.map(item => [item.id, item.registerAccelerator])).toEqual([
      ['brushTool', false],
      ['save', true]
    ]);
    tools[1]?.click(null, null, { triggeredByAccelerator: true });
    tools[0]?.click(null, null, {});
    expect(sent.sent).toEqual([
      ['save', true],
      ['brushTool', false]
    ]);
  });

  it('opens libraries itself before the studio has said what its menus are', () => {
    const sent = actions();
    const file = template(
      sent,
      [{ label: 'File', items: [{ type: 'command', id: 'openLibrary', label: 'Open Library…', enabled: true }] }],
      true
    ).find(menu => menu.label === 'File')?.submenu as { click: (a: unknown, b: unknown, c: object) => void }[];
    file[0]?.click(null, null, {});

    expect(sent.openLibrary).toHaveBeenCalled();
    expect(sent.sent).toEqual([]);
  });
});
