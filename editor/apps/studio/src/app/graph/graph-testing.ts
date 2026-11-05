import type { GraphEvent, GraphHistoryState, GraphNodeState, GraphNodeType, GraphState } from '@patina/api-client';
import type { GraphSocket } from './graph-store';

export const clean: GraphHistoryState = { canUndo: false, canRedo: false, dirty: false };
export const dirty: GraphHistoryState = { canUndo: true, canRedo: false, undoLabel: 'Set Scale', dirty: true };

export function node(id: number, values: Record<string, number[]> = {}): GraphNodeState {
  return { id, type: 'Source/Noise', x: id * 100, y: 0, values, texts: {} };
}

export const noiseType: GraphNodeType = {
  path: 'Source/Noise',
  title: 'Noise',
  category: 'Source',
  summary: 'Noise.',
  hasPreview: true,
  ports: [
    { name: 'Scale', direction: 'Input', kind: 'Float', default: [8], summary: '' },
    { name: 'Out', direction: 'Output', kind: 'Image', default: [], summary: '' }
  ],
  settings: []
};

export function state(overrides: Partial<GraphState> = {}): GraphState {
  return {
    id: 'g1',
    path: '/lib/shared/graphs/a.vxtexgraph',
    name: 'A',
    revision: 1,
    sequence: 0,
    history: clean,
    nodes: [node(1), node(2)],
    edges: [{ fromNode: 1, fromPort: 'Out', toNode: 2, toPort: 'Input' }],
    settings: { seed: '7' },
    parameters: [],
    previews: [],
    repairs: [],
    evaluation: null,
    ...overrides
  };
}

export const noDelta = {
  nodes: [],
  removedNodes: [],
  edges: [],
  removedEdges: [],
  settings: {},
  removedSettings: []
};

/** An `edited` event that replaces some nodes. */
export function edited(sequence: number, nodes: GraphNodeState[], history = dirty): GraphEvent {
  return { sequence, kind: 'edited', revision: sequence + 1, history, delta: { ...noDelta, nodes } };
}

/** An `evaluated` event carrying preview hashes. */
export function evaluated(sequence: number, hashes: Record<number, string>): GraphEvent {
  return {
    sequence,
    kind: 'evaluated',
    revision: sequence + 1,
    history: clean,
    evaluation: {
      revision: sequence + 1,
      compiled: false,
      ran: 1,
      reused: 0,
      milliseconds: 1,
      previews: Object.entries(hashes).map(([id, hash]) => ({
        node: Number(id),
        port: 'Out',
        hash,
        width: 256,
        height: 256
      })),
      diagnostics: []
    }
  };
}

/** A socket a test drives by hand. */
export class FakeSocket implements GraphSocket {
  onopen: GraphSocket['onopen'] = null;
  onmessage: GraphSocket['onmessage'] = null;
  onclose: GraphSocket['onclose'] = null;
  closed = false;

  constructor(readonly url: string) {}

  close(): void {
    this.closed = true;
  }

  opened(): void {
    this.onopen?.call(this as unknown as WebSocket, new Event('open'));
  }

  send(event: GraphEvent): void {
    this.onmessage?.call(this as unknown as WebSocket, new MessageEvent('message', { data: JSON.stringify(event) }));
  }

  dropped(): void {
    this.onclose?.call(this as unknown as WebSocket, new CloseEvent('close'));
  }
}
