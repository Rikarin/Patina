import { Directive, input, output } from '@angular/core';
import { desktop } from './desktop';

/** How a menu writes the keys beside its items, as the platform does. */
export function keyLabels(): { mod: string; delete: string } {
  const platform = desktop()?.platform ?? globalThis.navigator.platform;
  const mac = platform === 'darwin' || /mac/iu.test(platform);
  return mac ? { mod: '⌘', delete: '⌫' } : { mod: 'Ctrl+', delete: 'Del' };
}

/** Whether a key asks for the context menu: the Menu key, or Shift+F10, which is the one a Mac keyboard has. */
export function asksForMenu(event: KeyboardEvent): boolean {
  return event.key === 'ContextMenu' || (event.shiftKey && event.key === 'F10');
}

/**
 * Opens an `xuiContextMenuTriggerFor` menu from the keyboard, at the focused row, with its first item focused.
 *
 * xUI's context menu opens only on a `contextmenu` event, which a Mac keyboard never sends, so this sends one from the
 * keys every platform has. It belongs in `@xui/context-menu`, and goes when the trigger handles the keys itself.
 */
@Directive({
  selector: '[ptnMenuKeys]',
  host: { '(keydown)': 'press($event)' }
})
export class MenuKeys {
  /** Where the menu is about, inside the host: by default whatever has focus. */
  readonly ptnMenuKeys = input<string>('');

  /**
   * Says what the menu will be about before it opens, so that it draws the right items the first time: an item that
   * changed under focus would take the focus with it.
   */
  readonly aiming = output<{ target: EventTarget }>();

  protected press(event: KeyboardEvent): void {
    if (!asksForMenu(event)) {
      return;
    }

    const focused = event.target as HTMLElement;
    const host = event.currentTarget as HTMLElement;
    const selector = this.ptnMenuKeys();
    const target = (selector ? host.querySelector<HTMLElement>(selector) : null) ?? focused;
    event.preventDefault();
    event.stopPropagation();
    this.aiming.emit({ target });
    const box = target.getBoundingClientRect();
    // Button 0 is how the menu tells a keyboard open from a right-click, and so focuses its first item.
    target.dispatchEvent(
      new MouseEvent('contextmenu', {
        bubbles: true,
        cancelable: true,
        button: 0,
        clientX: box.left + Math.min(24, box.width / 2),
        clientY: box.bottom
      })
    );
  }
}
