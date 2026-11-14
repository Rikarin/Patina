/** A size a node can be computed at: a level counted in halvings of its graph's size, or null to follow its inputs. */
export interface NodeSize {
  level: number | null;
  label: string;
}

const FollowsInputs: NodeSize = { level: null, label: 'As its inputs' };

/** Every size a node may be computed at, as the engine allows them: four times the graph's size to a sixteenth. */
export const NodeSizes: readonly NodeSize[] = [
  FollowsInputs,
  { level: -2, label: "Four times the graph's" },
  { level: -1, label: "Twice the graph's" },
  { level: 0, label: "The graph's" },
  { level: 1, label: "Half the graph's" },
  { level: 2, label: "A quarter of the graph's" },
  { level: 3, label: "An eighth of the graph's" },
  { level: 4, label: "A sixteenth of the graph's" }
];

/** The size a node is set to; a level the list does not hold reads as following the inputs. */
export function sizeOf(level: number | null | undefined): NodeSize {
  return NodeSizes.find(size => size.level === (level ?? null)) ?? FollowsInputs;
}

/** Whether a node has a size of its own: an Output writes the graph's maps, and a boundary or a dot computes nothing. */
export function sizeable(type: string): boolean {
  return !type.startsWith('Output/') && !type.startsWith('Sub-graph/') && type !== 'Reroute';
}
