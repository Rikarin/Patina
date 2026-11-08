import { acceleratorOf, hasCommandKey } from './chords';
import { type MenuEntry, MenuBar } from './command-table';
import type { MenuModel, MenuModelItem } from './desktop';

/** What the menu reads from the commands at work: the same answers the keys and buttons get. */
export interface MenuSource {
  enabled(id: string): boolean;
  label(id: string): string;
  checked(id: string): boolean | undefined;
  keys(): ReadonlyMap<string, readonly string[]>;
}

/** What the document in front has, which decides which menus are shown. */
export interface MenuContext {
  stack: boolean;
  graph: boolean;
  viewport: boolean;
}

/**
 * The menu bar as it should be now, from {@link MenuBar}. A menu for a kind of document shows only with one in front,
 * and an item is on only when something would answer it. Lines never lead, trail or come two at a time.
 */
export function menuModel(source: MenuSource, context: MenuContext): MenuModel {
  return MenuBar.filter(menu => menu.when === undefined || context[menu.when]).map(menu => ({
    label: menu.label,
    items: tidy(menu.items.map(entry => itemOf(entry, source)))
  }));
}

function itemOf(entry: MenuEntry, source: MenuSource): MenuModelItem {
  if (entry === '-') {
    return { type: 'separator' };
  }

  if (typeof entry === 'string') {
    const chord = source.keys().get(entry)?.[0];
    const checked = source.checked(entry);
    return {
      type: 'command',
      id: entry,
      label: source.label(entry),
      enabled: source.enabled(entry),
      ...(chord ? { accelerator: acceleratorOf(chord), registered: hasCommandKey(chord) } : {}),
      ...(checked === undefined ? {} : { checked })
    };
  }

  if ('submenu' in entry) {
    return { type: 'submenu', label: entry.submenu, items: tidy(entry.items.map(item => itemOf(item, source))) };
  }

  return { type: 'native', native: entry.native };
}

function tidy(items: MenuModelItem[]): MenuModelItem[] {
  const kept = items.filter(
    (item, at) => item.type !== 'separator' || (at > 0 && items[at - 1]?.type !== 'separator')
  );
  while (kept.at(-1)?.type === 'separator') {
    kept.pop();
  }

  return kept;
}
