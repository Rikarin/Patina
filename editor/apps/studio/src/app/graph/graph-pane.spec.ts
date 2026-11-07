import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { type GraphState, createPatinaClient } from '@patina/api-client';
import { API_CLIENT_FACTORY, SERVER_CONNECTION, type ServerState, SystemStatus } from '../core/system-status';
import { GraphPane } from './graph-pane';
import { GRAPH_SOCKET, GraphStore } from './graph-store';
import { FakeSocket, evaluated, noiseType, state } from './graph-testing';

vi.stubGlobal(
  'ResizeObserver',
  class {
    observe(): void {
      // jsdom lays nothing out.
    }

    disconnect(): void {
      // Nothing to stop.
    }
  }
);

const online = (connected: boolean): ServerState => ({
  kind: 'online',
  system: {
    version: '1',
    apiVersion: 1,
    library: null,
    host: { os: 'macOS', architecture: 'Arm64', runtime: '.NET 10' },
    worker: { connected },
    formats: { pictures: ['.png'], environments: ['.hdr'] },
    textureSides: [256, 512, 1024, 2048],
    defaultTextureSide: 2048
  }
});

async function render(status: ServerState = online(true), graph: () => GraphState = state) {
  const sockets: FakeSocket[] = [];
  const answer = (input: RequestInfo | URL): Promise<Response> => {
    const { method, url } = input as Request;
    const path = new URL(url).pathname.replace('/api/v1/', '');
    if (path === 'graphs' && method === 'POST') {
      return Promise.resolve(Response.json(graph()));
    }

    if (path.endsWith('compounds')) {
      return Promise.resolve(Response.json([]));
    }

    return Promise.resolve(Response.json(path.endsWith('node-types') ? [noiseType] : graph()));
  };
  TestBed.configureTestingModule({
    providers: [
      {
        provide: SystemStatus,
        useValue: { state: signal(status), textureSides: signal([256, 512, 1024, 2048]), defaultTextureSide: signal(2048) }
      },
      { provide: SERVER_CONNECTION, useValue: () => Promise.resolve({ baseUrl: 'http://server:1', token: null }) },
      {
        provide: API_CLIENT_FACTORY,
        useValue: (connection: Parameters<typeof createPatinaClient>[0]) => createPatinaClient(connection, answer)
      },
      {
        provide: GRAPH_SOCKET,
        useValue: (url: string) => {
          const socket = new FakeSocket(url);
          sockets.push(socket);
          return socket;
        }
      }
    ]
  });
  const fixture = TestBed.createComponent(GraphPane);
  await fixture.whenStable();
  const store = TestBed.inject(GraphStore);
  const element = fixture.nativeElement as HTMLElement;
  const found = (id: string) => element.querySelector(`[data-testid=${id}]`);
  const open = async () => {
    await store.open('projects/a/a.vxtexgraph');
    sockets.at(-1)?.opened();
    await fixture.whenStable();
  };
  return { fixture, store, sockets, element, found, open };
}

describe('GraphPane', () => {
  /** The ports a node draws on the canvas, by name. */
  function portsOn(element: HTMLElement, node: number): (string | undefined)[] {
    const ports = element.querySelectorAll(`[data-node="${String(node)}"] xui-graph-port`);
    return [...ports].map(port => port.getAttribute('title') ?? port.textContent.trim());
  }

  it('draws a number input only when it has a wire or someone asked for it', async () => {
    const wired = () => state({ edges: [{ fromNode: 1, fromPort: 'Out', toNode: 2, toPort: 'Scale' }] });
    const { open, element, fixture, store } = await render(online(true), wired);

    await open();

    expect(portsOn(element, 1)).toHaveLength(1); // Out
    expect(portsOn(element, 2)).toHaveLength(2); // Scale, with its wire, and Out

    store.togglePort(1, 'Scale');
    await fixture.whenStable();
    expect(portsOn(element, 1)).toHaveLength(2);

    store.togglePort(1, 'Scale');
    await fixture.whenStable();
    expect(portsOn(element, 1)).toHaveLength(1);
  });

  it('says when no graph is open', async () => {
    const { found } = await render();

    expect(found('graph-none')?.textContent).toContain('No graph open');
  });

  it('draws a node per node once a graph is open', async () => {
    const { open, element, found } = await render();

    await open();

    expect(element.querySelectorAll('[data-testid=graph-node]')).toHaveLength(2);
    expect(found('graph-name')?.textContent).toContain('A');
    expect(found('graph-unsaved')).toBeNull();
  });

  it('marks unsaved changes', async () => {
    const { open, sockets, found, fixture } = await render();
    await open();

    sockets[0]?.send({
      sequence: 1,
      kind: 'edited',
      revision: 2,
      history: { canUndo: true, canRedo: false, dirty: true },
      delta: { nodes: [], removedNodes: [], edges: [], removedEdges: [], settings: {}, removedSettings: [] }
    });
    await fixture.whenStable();

    expect(found('graph-unsaved')).not.toBeNull();
    expect((found('graph-save') as HTMLButtonElement).disabled).toBe(false);
  });

  it('lists diagnostics, such as a bitmap the Worker cannot read yet', async () => {
    const { open, sockets, found, fixture } = await render();
    await open();
    const event = evaluated(1, {});
    if (event.evaluation) {
      event.evaluation.diagnostics = [
        { id: 'PT0001', message: 'This graph reads the image a.png.', severity: 'Error', node: 1 }
      ];
    }

    sockets[0]?.send(event);
    await fixture.whenStable();

    expect(found('graph-diagnostics')?.textContent).toContain('PT0001');
  });

  it('says when an evaluation failed', async () => {
    const { open, sockets, found, fixture } = await render();
    await open();
    const event = evaluated(1, {});
    if (event.evaluation) {
      event.evaluation.error = 'No Vulkan.';
    }

    sockets[0]?.send(event);
    await fixture.whenStable();

    expect(found('graph-evaluation-error')?.textContent).toContain('No Vulkan');
  });

  it('says when no Worker is connected, with edits still on', async () => {
    const { open, found } = await render(online(false));

    await open();

    expect(found('graph-no-worker')).not.toBeNull();
    expect((found('graph-add') as HTMLButtonElement).disabled).toBe(false);
  });

  it('says when the Server is gone, and pauses editing', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    const { open, sockets, found, fixture } = await render();
    await open();

    sockets[0]?.dropped();
    await fixture.whenStable();

    expect(found('graph-offline')).not.toBeNull();
    expect((found('graph-add') as HTMLButtonElement).disabled).toBe(true);
    vi.useRealTimers();
  });
});
