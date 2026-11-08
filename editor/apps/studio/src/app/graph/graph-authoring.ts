import type { GraphSettingCondition, GraphSettingDefinition } from '@patina/api-client';

/** A node's box on the canvas, in graph units. */
export interface NodeBox {
  id: number;
  x: number;
  y: number;
  width: number;
  height: number;
}

export type Alignment = 'left' | 'centre' | 'right' | 'top' | 'middle' | 'bottom';

/**
 * Where each box goes when the boxes line up on one edge or centre of the box round them all. Only the axis that lines
 * up moves. Fewer than two boxes have nothing to line up with.
 */
export function aligned(boxes: readonly NodeBox[], alignment: Alignment): Map<number, { x: number; y: number }> {
  const moved = new Map<number, { x: number; y: number }>();
  if (boxes.length < 2) {
    return moved;
  }

  const left = Math.min(...boxes.map(box => box.x));
  const right = Math.max(...boxes.map(box => box.x + box.width));
  const top = Math.min(...boxes.map(box => box.y));
  const bottom = Math.max(...boxes.map(box => box.y + box.height));
  for (const box of boxes) {
    const at = { x: box.x, y: box.y };
    switch (alignment) {
      case 'left':
        at.x = left;
        break;
      case 'centre':
        at.x = (left + right) / 2 - box.width / 2;
        break;
      case 'right':
        at.x = right - box.width;
        break;
      case 'top':
        at.y = top;
        break;
      case 'middle':
        at.y = (top + bottom) / 2 - box.height / 2;
        break;
      case 'bottom':
        at.y = bottom - box.height;
        break;
    }

    if (at.x !== box.x || at.y !== box.y) {
      moved.set(box.id, at);
    }
  }

  return moved;
}

/**
 * Where each box goes when the gaps between them along one axis are made equal. The first and last boxes in that order
 * stay, and the ones between share what room is left. Fewer than three boxes have no gap to share.
 */
export function distributed(
  boxes: readonly NodeBox[],
  axis: 'horizontal' | 'vertical'
): Map<number, { x: number; y: number }> {
  const moved = new Map<number, { x: number; y: number }>();
  if (boxes.length < 3) {
    return moved;
  }

  const across = axis === 'horizontal';
  const start = (box: NodeBox): number => (across ? box.x : box.y);
  const size = (box: NodeBox): number => (across ? box.width : box.height);
  const sorted = [...boxes].sort((a, b) => start(a) - start(b) || a.id - b.id);
  const first = sorted[0];
  const last = sorted[sorted.length - 1];
  if (first === undefined || last === undefined) {
    return moved;
  }

  const span = start(last) + size(last) - start(first);
  const filled = sorted.reduce((sum, box) => sum + size(box), 0);
  const gap = (span - filled) / (sorted.length - 1);
  let cursor = start(first);
  for (const box of sorted) {
    if (box !== first && box !== last && cursor !== start(box)) {
      moved.set(box.id, across ? { x: cursor, y: box.y } : { x: box.x, y: cursor });
    }

    cursor += size(box) + gap;
  }

  return moved;
}

/**
 * Whether a parameter or setting with this condition is shown, given what the one it names holds. The rule Vixen's
 * `SettingCondition.Holds` states, so that every client agrees: numbers compare as numbers, anything else as a name
 * ignoring case.
 */
export function holds(condition: GraphSettingCondition | null | undefined, current: string | undefined): boolean {
  if (!condition) {
    return true;
  }

  const held = current ?? '';
  const a = Number(held);
  const b = Number(condition.value);
  const numbers = held.trim() !== '' && condition.value.trim() !== '' && Number.isFinite(a) && Number.isFinite(b);
  const equal = numbers ? a === b : held.toLowerCase() === condition.value.toLowerCase();
  return equal !== condition.negated;
}

/** What a person calls a parameter: its label, or its name when it has none. */
export function titleOf(parameter: Pick<GraphSettingDefinition, 'name' | 'label'>): string {
  return parameter.label?.trim() || parameter.name;
}

/** The control a parameter is drawn with. */
export type ParameterControl = 'slider' | 'number' | 'angle' | 'switch' | 'toggle' | 'choice' | 'text';

export function controlOf(parameter: GraphSettingDefinition): ParameterControl {
  if (parameter.accepted.length > 0) {
    return 'choice';
  }

  const ranged =
    parameter.minimum !== null &&
    parameter.minimum !== undefined &&
    parameter.maximum !== null &&
    parameter.maximum !== undefined;
  switch (parameter.kind) {
    case 'Bool':
      return parameter.widget === 'Toggle' ? 'toggle' : 'switch';
    case 'Float':
    case 'Int':
      if (parameter.widget === 'Angle') {
        return 'angle';
      }

      return ranged ? 'slider' : 'number';
    default:
      return 'text';
  }
}

/** The widgets a parameter of a kind can be given, as the Server accepts them. */
export function widgetsFor(parameter: GraphSettingDefinition): readonly string[] {
  switch (parameter.kind) {
    case 'Bool':
      return ['Default', 'Toggle'];
    case 'Float':
    case 'Int':
      return parameter.minimum !== null &&
        parameter.minimum !== undefined &&
        parameter.maximum !== null &&
        parameter.maximum !== undefined
        ? ['Default', 'Slider', 'Angle']
        : ['Default', 'Angle'];
    default:
      return ['Default'];
  }
}

/** Parameters in their groups, the ungrouped first, each group in the order its first member comes. */
export function grouped<T extends Pick<GraphSettingDefinition, 'group'>>(
  parameters: readonly T[]
): { group: string; members: T[] }[] {
  const groups: { group: string; members: T[] }[] = [];
  for (const parameter of parameters) {
    const name = parameter.group.trim();
    let held = groups.find(group => group.group === name);
    if (!held) {
      held = { group: name, members: [] };
      if (name === '') {
        groups.unshift(held);
      } else {
        groups.push(held);
      }
    }

    held.members.push(parameter);
  }

  return groups;
}

/** The first preset name of the form the graph does not use yet. */
export function freePresetName(taken: readonly { name: string }[]): string {
  const names = new Set(taken.map(preset => preset.name));
  let number = names.size + 1;
  while (names.has(`Preset ${String(number)}`)) {
    number++;
  }

  return `Preset ${String(number)}`;
}
