/** A size a node can be computed at: a level counted in halvings of its graph's size, or null to follow its inputs. */
export interface NodeSize {
  level: number | null;
  label: string;
  /** What a card on the canvas says of it, short: the graph's size times this (#298). Empty for following the inputs. */
  badge: string;
}

const FollowsInputs: NodeSize = { level: null, label: 'As its inputs', badge: '' };

/** Every size a node may be computed at, as the engine allows them: four times the graph's size to a sixteenth. */
export const NodeSizes: readonly NodeSize[] = [
  FollowsInputs,
  { level: -2, label: "Four times the graph's", badge: '×4' },
  { level: -1, label: "Twice the graph's", badge: '×2' },
  { level: 0, label: "The graph's", badge: '×1' },
  { level: 1, label: "Half the graph's", badge: '½' },
  { level: 2, label: "A quarter of the graph's", badge: '¼' },
  { level: 3, label: "An eighth of the graph's", badge: '⅛' },
  { level: 4, label: "A sixteenth of the graph's", badge: '1/16' }
];

/** The size a node is set to; a level the list does not hold reads as following the inputs. */
export function sizeOf(level: number | null | undefined): NodeSize {
  return NodeSizes.find(size => size.level === (level ?? null)) ?? FollowsInputs;
}

/** Whether a node has a size of its own: an Output writes the graph's maps, and a boundary or a dot computes nothing. */
export function sizeable(type: string): boolean {
  return !type.startsWith('Output/') && !type.startsWith('Sub-graph/') && type !== 'Reroute';
}
