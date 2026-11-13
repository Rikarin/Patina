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
export type ParameterControl = 'slider' | 'number' | 'angle' | 'switch' | 'toggle' | 'choice' | 'colour' | 'text';

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
    case 'Color':
      return 'colour';
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

/** A wire as {@link flowOf} reads it. */
export interface FlowWire {
  fromNode: number;
  toNode: number;
}

/**
 * The wires a picture takes on its way into the selected nodes and out of them, as far as it goes either way: what the
 * graph lights up to show where a node's picture comes from and where it ends up.
 */
export function flowOf<T extends FlowWire>(wires: readonly T[], selected: ReadonlySet<number>): Set<T> {
  const flowing = new Set<T>();
  const walk = (along: (wire: T) => number, back: (wire: T) => number): void => {
    const seen = new Set(selected);
    const waiting = [...seen];
    for (let node = waiting.pop(); node !== undefined; node = waiting.pop()) {
      for (const wire of wires) {
        if (back(wire) === node) {
          flowing.add(wire);
          const next = along(wire);
          if (!seen.has(next)) {
            seen.add(next);
            waiting.push(next);
          }
        }
      }
    }
  };
  walk(
    wire => wire.fromNode,
    wire => wire.toNode
  );
  walk(
    wire => wire.toNode,
    wire => wire.fromNode
  );
  return flowing;
}

/** A line of the add-node palette: a node type, and the heading it is under. */
export interface PaletteLine<T> {
  type: T;
  group: string;
}

/**
 * The palette with nothing typed: the starred node types, then the ones added last, then every one under its category.
 * A type the list was not given, such as one a dropped wire cannot use or one that has gone, is left out of the first
 * two, so a favourite never offers what cannot be added there.
 */
export function paletteOf<T extends { path: string; category: string }>(
  types: readonly T[],
  favourites: readonly string[],
  recents: readonly string[]
): PaletteLine<T>[] {
  const byPath = new Map(types.map(type => [type.path, type]));
  const pick = (paths: readonly string[], group: string): PaletteLine<T>[] =>
    paths.flatMap(path => {
      const type = byPath.get(path);
      return type ? [{ type, group }] : [];
    });
  return [
    ...pick(favourites, 'Favourites'),
    ...pick(recents, 'Recent'),
    ...types.map(type => ({ type, group: type.category || 'Compounds' }))
  ];
}

/** The nodes whose title holds what was typed, those that start with it first, for Find Node. */
export function findMatches<T extends { title: string }>(nodes: readonly T[], query: string): T[] {
  const typed = query.trim().toLocaleLowerCase();
  if (typed === '') {
    return [...nodes];
  }

  const starts = (node: T) => (node.title.toLocaleLowerCase().startsWith(typed) ? 0 : 1);
  return nodes
    .filter(node => node.title.toLocaleLowerCase().includes(typed))
    .sort((a, b) => starts(a) - starts(b) || a.title.localeCompare(b.title));
}

/** A wire as the Server keeps it: from an output to an input. */
export interface Wire {
  fromNode: number;
  fromPort: string;
  toNode: number;
  toPort: string;
}

/** One end of a wire: a port of a node. */
export interface WireEnd {
  node: number;
  port: string;
}

/** What rewiring asks of the Server. A connect into a wired input replaces the wire there, as the canvas does. */
export type WireCommand =
  | { op: 'connect'; fromNode: number; fromPort: string; toNode: number; toPort: string }
  | { op: 'disconnect'; toNode: number; toPort: string };

const isEnd = (end: WireEnd, node: number, port: string) => end.node === node && end.port === port;

/**
 * Every wire leaving one output, moved to leave another instead. A wire into the node it would now come from is left
 * where it is, since it would make a loop.
 */
export function movedWires(wires: readonly Wire[], from: WireEnd, to: WireEnd): WireCommand[] {
  if (isEnd(from, to.node, to.port)) {
    return [];
  }

  return wires
    .filter(wire => isEnd(from, wire.fromNode, wire.fromPort) && wire.toNode !== to.node)
    .map(wire => ({ op: 'connect', fromNode: to.node, fromPort: to.port, toNode: wire.toNode, toPort: wire.toPort }));
}

/** What feeds one input, wired into another input as well, or moved there when `move` is set. */
export function copiedWire(wires: readonly Wire[], from: WireEnd, to: WireEnd, move: boolean): WireCommand[] {
  const wire = wires.find(candidate => isEnd(from, candidate.toNode, candidate.toPort));
  if (!wire || isEnd(from, to.node, to.port) || wire.fromNode === to.node) {
    return [];
  }

  const connect: WireCommand = {
    op: 'connect',
    fromNode: wire.fromNode,
    fromPort: wire.fromPort,
    toNode: to.node,
    toPort: to.port
  };
  return move ? [connect, { op: 'disconnect', toNode: from.node, toPort: from.port }] : [connect];
}

/**
 * The wires into two inputs of one node, traded: what fed the first feeds the second and the other way round. An input
 * with no wire trades its emptiness. Nothing to trade is nothing to do.
 */
export function swappedWires(wires: readonly Wire[], node: number, first: string, second: string): WireCommand[] {
  const into = (port: string) => wires.find(wire => wire.toNode === node && wire.toPort === port);
  const [a, b] = [into(first), into(second)];
  if (!a && !b) {
    return [];
  }

  // Both were found before anything changes, since the first is replaced before the second is given what it held.
  const give = (wire: Wire | undefined, port: string): WireCommand =>
    wire
      ? { op: 'connect', fromNode: wire.fromNode, fromPort: wire.fromPort, toNode: node, toPort: port }
      : { op: 'disconnect', toNode: node, toPort: port };
  return [give(b, first), give(a, second)];
}

/**
 * The two inputs Swap Inputs trades: the first two that carry one kind of wire, when either is wired. A blend's
 * foreground and background are the case this is for, and its mask, the third picture, is never one of them.
 */
export function swapPairOf(
  inputs: readonly { name: string; family: string }[],
  wired: ReadonlySet<string>
): [string, string] | null {
  const families = new Map<string, string[]>();
  for (const input of inputs) {
    families.set(input.family, [...(families.get(input.family) ?? []), input.name]);
  }

  for (const [first, second] of families.values()) {
    if (first !== undefined && second !== undefined && (wired.has(first) || wired.has(second))) {
      return [first, second];
    }
  }

  return null;
}
