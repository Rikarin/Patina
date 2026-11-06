import type { StackCommand } from '@patina/api-client';

/** A row of the stack as dragging needs it: top of the stack first, a folder above what it holds. */
export interface DragRow {
  id: string;
  parent: string | null;
  folder: boolean;
}

/** Where on a row something was dropped. */
export type DropZone = 'above' | 'below' | 'into';

function siblings(rows: readonly DragRow[], parent: string | null): readonly string[] {
  return rows.filter(row => row.parent === parent).map(row => row.id);
}

/** Whether a layer is inside a folder, at any depth, or is that folder. */
function isWithin(rows: readonly DragRow[], layer: string, folder: string): boolean {
  let at: string | null = layer;
  while (at !== null) {
    if (at === folder) {
      return true;
    }

    at = rows.find(row => row.id === at)?.parent ?? null;
  }

  return false;
}

/** Steps one layer takes through its siblings. Positive is towards the top of the stack. */
function steps(layer: string, delta: number): StackCommand[] {
  const op = { op: 'moveLayer' as const, layer, delta: Math.sign(delta) };
  return Array.from({ length: Math.abs(delta) }, () => op);
}

/**
 * The commands that drop a layer on a row, or an empty list when the drop means nothing. A move is steps, which the
 * Server undoes as one, and going to another folder lands at its top and steps down from there.
 */
export function dropCommands(rows: readonly DragRow[], dragged: string, target: string, zone: DropZone): StackCommand[] {
  const from = rows.find(row => row.id === dragged);
  const onto = rows.find(row => row.id === target);
  if (!from || !onto || dragged === target || isWithin(rows, target, dragged)) {
    return [];
  }

  if (zone === 'into') {
    return onto.folder && from.parent !== target ? [{ op: 'nestLayer', layer: dragged, parent: target }] : [];
  }

  const parent = onto.parent;
  if (from.parent === parent) {
    const held = siblings(rows, parent);
    const start = held.indexOf(dragged);
    const others = held.filter(id => id !== dragged);
    const place = others.indexOf(target) + (zone === 'below' ? 1 : 0);
    return steps(dragged, start - place);
  }

  const place = siblings(rows, parent).indexOf(target) + (zone === 'below' ? 1 : 0);
  return [{ op: 'nestLayer', layer: dragged, parent }, ...steps(dragged, -place)];
}
