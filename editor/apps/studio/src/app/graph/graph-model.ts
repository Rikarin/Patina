import type {
  GraphCommentState,
  GraphDelta,
  GraphDiagnostic,
  GraphEdgeState,
  GraphEvaluation,
  GraphEvent,
  GraphGroupState,
  GraphHistoryState,
  GraphInterfacePort,
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
  /** The ports this graph has when another graph holds it. */
  interface: readonly GraphInterfacePort[];
  /** Frames round some nodes. */
  groups: readonly GraphGroupState[];
  /** Notes on the canvas. */
  comments: readonly GraphCommentState[];
  /** The newest preview of each node. A node has one image output, so a node is enough of a key. */
  previews: ReadonlyMap<number, GraphPreview>;
  repairs: readonly GraphDiagnostic[];
  evaluation: GraphEvaluation | null;
  /** The newest revision with nothing left to evaluate. Below {@link revision}, evaluation is behind. */
  settled: number;
  /** True for a compound that ships with Patina. */
  readOnly: boolean;
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
    interface: state.interface,
    groups: state.groups,
    comments: state.comments,
    previews: new Map(state.previews.map(preview => [preview.node, preview])),
    repairs: state.repairs,
    evaluation: state.evaluation ?? null,
    settled: state.settled,
    readOnly: state.readOnly
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

  if (event.kind === 'settled') {
    next.settled = event.revision;
  }

  if (event.evaluation) {
    next.evaluation = event.evaluation;
    next.settled = event.evaluation.revision;
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
  document.parameters = delta.parameters ?? document.parameters;
  document.interface = delta.interface ?? document.interface;
  document.groups = delta.groups ?? document.groups;
  document.comments = delta.comments ?? document.comments;
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

/** What the toolbar says about the last evaluation, or nothing when there is nothing to say. */
export function evaluationSummary(document: GraphDocument): string {
  if (document.settled < document.revision) {
    return 'Evaluating…';
  }

  const evaluation = document.evaluation;
  if (!evaluation || evaluation.error) {
    return '';
  }

  if (evaluation.diagnostics.some(item => item.severity === 'Error')) {
    return 'Not evaluated';
  }

  return evaluation.ran === 0
    ? 'Up to date'
    : `${String(evaluation.ran)} ran, ${evaluation.milliseconds.toFixed(0)} ms`;
}
