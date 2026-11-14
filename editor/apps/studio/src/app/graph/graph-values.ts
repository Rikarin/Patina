import type { GraphCommand, GraphNodeState, GraphNodeType } from '@patina/api-client';

/** What Copy Values takes from a node: its type and every number and text set on it. */
export type CopiedValues = Pick<GraphNodeState, 'type' | 'values' | 'texts'>;

function sameLanes(a: readonly number[] | undefined, b: readonly number[]): boolean {
  return a?.length === b.length && a.every((lane, index) => lane === b[index]);
}

/**
 * The edits that make a node of the same type hold what was copied (#181): each number and text the copy set, and the
 * type's default for each one the copy left alone that the node set, so the node ends as the copy was. An expression is
 * a text, so it comes along, and one the copy did not have is cleared. Nothing that already matches is sent, so a
 * paste onto an equal node is no edit.
 */
export function pastedValues(type: GraphNodeType, copied: CopiedValues, target: GraphNodeState): GraphCommand[] {
  if (copied.type !== type.path || target.type !== type.path) {
    return [];
  }

  const node = target.id;
  const commands: GraphCommand[] = [];
  for (const port of type.ports) {
    if (port.direction !== 'Input' || port.default.length === 0) {
      continue;
    }

    const value = copied.values[port.name] ?? port.default;
    if (!sameLanes(target.values[port.name] ?? port.default, value)) {
      commands.push({ op: 'setValue', node, port: port.name, value: [...value] });
    }
  }

  const defaults = new Map(type.settings.map(setting => [setting.name, setting.default]));
  const names = new Set([...Object.keys(copied.texts), ...Object.keys(target.texts)]);
  for (const name of names) {
    const fallback = defaults.get(name) ?? '';
    const value = copied.texts[name] ?? fallback;
    if ((target.texts[name] ?? fallback) !== value) {
      commands.push({ op: 'setText', node, port: name, value });
    }
  }

  return commands;
}
