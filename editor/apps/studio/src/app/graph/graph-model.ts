import type {
  GraphDelta,
  GraphDiagnostic,
  GraphEdgeState,
  GraphEvaluation,
  GraphEvent,
  GraphHistoryState,
  GraphNodeState,
  GraphPreview,
  GraphSettingDefinition,
  GraphState
} from '@patina/api-client';

/** A graph as this client holds it: the Server's state, kept current by nothing but its events. */
export interface GraphDocument {
  id: string;
  path: string;
  name: string;
  revision: number;
  /** The last event applied. A reconnect asks for what came after it. */
  sequence: number;
  history: GraphHistoryState;
  nodes: ReadonlyMap<number, GraphNodeState>;
  /** By the input they arrive at, of which each has at most one. */
  edges: ReadonlyMap<string, GraphEdgeState>;
  settings: Readonly<Record<string, string>>;
  parameters: readonly GraphSettingDefinition[];
  /** The newest preview of each node. A node has one image output, so a node is enough of a key. */
  previews: ReadonlyMap<number, GraphPreview>;
  repairs: readonly GraphDiagnostic[];
  evaluation: GraphEvaluation | null;
}

/** What to do with an event, given what the client holds. */
export type Arrival = 'apply' | 'seen' | 'reload' | 'closed';

export function edgeKey(edge: Pick<GraphEdgeState, 'toNode' | 'toPort'>): string {
  return `${String(edge.toNode)}/${edge.toPort}`;
}

export function documentOf(state: GraphState): GraphDocument {
  return {
    id: state.id,
    path: state.path,
    name: state.name,
    revision: state.revision,
    sequence: state.sequence,
    history: state.history,
    nodes: new Map(state.nodes.map(node => [node.id, node])),
    edges: new Map(state.edges.map(edge => [edgeKey(edge), edge])),
    settings: state.settings,
    parameters: state.parameters,
    previews: new Map(state.previews.map(preview => [preview.node, preview])),
    repairs: state.repairs,
    evaluation: state.evaluation ?? null
  };
}

/**
 * Numbers rise by one, so anything but the next one means either a replay of what is held, as after a reconnect, or
 * something missed. `resync` is the Server saying the same: the events asked for are gone.
 */
export function classify(document: GraphDocument, event: GraphEvent): Arrival {
  if (event.kind === 'resync') {
    return 'reload';
  }

  if (event.sequence <= document.sequence) {
    return 'seen';
  }

  if (event.sequence !== document.sequence + 1) {
    return 'reload';
  }

  return event.kind === 'closed' ? 'closed' : 'apply';
}

/** The document after an event {@link classify} said to apply. */
export function applyEvent(document: GraphDocument, event: GraphEvent): GraphDocument {
  const next: GraphDocument = {
    ...document,
    revision: event.revision,
    sequence: event.sequence,
    history: event.history
  };
  if (event.delta) {
    applyDelta(next, event.delta);
  }

  if (event.evaluation) {
    next.evaluation = event.evaluation;
    if (event.evaluation.previews.length > 0) {
      // Only what differs from the last evaluation is listed, so everything else stays as it is.
      const previews = new Map(next.previews);
      for (const preview of event.evaluation.previews) {
        if (next.nodes.has(preview.node)) {
          previews.set(preview.node, preview);
        }
      }

      next.previews = previews;
    }
  }

  return next;
}

function applyDelta(document: GraphDocument, delta: GraphDelta): void {
  const nodes = new Map(document.nodes);
  for (const node of delta.nodes) {
    nodes.set(node.id, node);
  }

  const edges = new Map(document.edges);
  for (const edge of delta.removedEdges) {
    edges.delete(edgeKey(edge));
  }

  for (const edge of delta.edges) {
    edges.set(edgeKey(edge), edge);
  }

  if (delta.removedNodes.length > 0) {
    const previews = new Map(document.previews);
    for (const id of delta.removedNodes) {
      nodes.delete(id);
      previews.delete(id);
    }

    document.previews = previews;
  }

  document.nodes = nodes;
  document.edges = edges;
  document.settings = withSettings(document.settings, delta);
}

function withSettings(settings: Readonly<Record<string, string>>, delta: GraphDelta): Record<string, string> {
  const next = { ...settings, ...delta.settings };
  for (const key of delta.removedSettings) {
    // eslint-disable-next-line @typescript-eslint/no-dynamic-delete -- settings are a dictionary of the Server's keys
    delete next[key];
  }

  return next;
}

/** What the compiler says now and what reading the file had to fix, the newest evaluation first. */
export function diagnosticsOf(document: GraphDocument): readonly GraphDiagnostic[] {
  return [...(document.evaluation?.diagnostics ?? []), ...document.repairs];
}
