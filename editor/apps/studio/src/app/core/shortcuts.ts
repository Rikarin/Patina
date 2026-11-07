import { Injectable, inject, signal } from '@angular/core';
import { PaintStore, type PaintTool } from '../paint/paint-store';

const StorageKey = 'patina.shortcuts.1';
const RadiusStep = 1.25;

/** What a key can do. The Edit commands are the menu's, since a menu takes those keys first. */
export interface Shortcut {
  id: string;
  label: string;
  key: string;
}

const Defaults: readonly Shortcut[] = [
  { id: 'smaller', label: 'Smaller brush', key: '[' },
  { id: 'larger', label: 'Larger brush', key: ']' },
  { id: 'tool-brush', label: 'Brush tool', key: 'b' },
  { id: 'tool-line', label: 'Line tool', key: 'l' },
  { id: 'tool-path', label: 'Path tool', key: 'p' },
  { id: 'colour-pick', label: 'Pick a colour from the id map', key: 'i' }
];

const Tools: Readonly<Record<string, PaintTool>> = { 'tool-brush': 'brush', 'tool-line': 'line', 'tool-path': 'path' };

function typing(target: EventTarget | null): boolean {
  return target instanceof Element && target.closest('input, textarea, select, [contenteditable=true]') !== null;
}

/** Keys for painting. Rebinding is kept on this machine, since it is a person's and not a document's. */
@Injectable({ providedIn: 'root' })
export class Shortcuts {
  private readonly paint = inject(PaintStore);

  readonly all = signal<readonly Shortcut[]>(this.read());

  /**
   * Runs what a key is bound to. ⚠ It returns nothing on purpose: a listener that returns false makes Angular prevent
   * the default, which swallowed Tab and Delete everywhere.
   */
  press(event: KeyboardEvent): void {
    if (typing(event.target) || event.metaKey || event.ctrlKey || event.altKey || !this.paint.state()) {
      return;
    }

    const found = this.all().find(shortcut => shortcut.key === event.key.toLowerCase());
    if (found) {
      this.run(found.id);
      event.preventDefault();
    }
  }

  rebind(id: string, key: string): void {
    const next = this.all().map(shortcut => (shortcut.id === id ? { ...shortcut, key: key.toLowerCase() } : shortcut));
    this.all.set(next);
    globalThis.localStorage.setItem(StorageKey, JSON.stringify(next));
  }

  reset(): void {
    globalThis.localStorage.removeItem(StorageKey);
    this.all.set(Defaults);
  }

  private run(id: string): void {
    const tool = Tools[id];
    if (tool) {
      this.paint.tool.set(tool);
    } else if (id === 'smaller' || id === 'larger') {
      const radius = this.paint.radius() * (id === 'larger' ? RadiusStep : 1 / RadiusStep);
      this.paint.radius.set(Math.max(1, Math.min(256, Math.round(radius))));
    } else if (id === 'colour-pick') {
      this.paint.picking.set(this.paint.layer());
    }
  }

  private read(): readonly Shortcut[] {
    try {
      const saved = JSON.parse(globalThis.localStorage.getItem(StorageKey) ?? '[]') as Shortcut[];
      return Defaults.map(shortcut => saved.find(held => held.id === shortcut.id) ?? shortcut);
    } catch {
      return Defaults;
    }
  }
}
