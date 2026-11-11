import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { type GraphNodeType, type GraphState, createPatinaClient } from '@patina/api-client';
import { Preferences } from '../core/preferences';
import { API_CLIENT_FACTORY, SERVER_CONNECTION, type ServerState, SystemStatus } from '../core/system-status';
import { GraphPane } from './graph-pane';
import { GRAPH_SOCKET, GraphStore } from './graph-store';
import { FakeSocket, evaluated, node, noiseType, state } from './graph-testing';

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
    host: { os: 'macOS', architecture: 'Arm64', runtime: '.NET 11' },
    worker: { connected },
    formats: { pictures: ['.png'], environments: ['.hdr'], meshes: ['.obj'] },
    textureSides: [256, 512, 1024, 2048],
    defaultTextureSide: 2048
  }
});

/** The Server's ranking, for these tests: a title that starts with what was typed first, then one that contains it. */
function ranked(types: readonly GraphNodeType[], query: string): string[] {
  const typed = query.toLowerCase();
  const starts = types.filter(type => type.title.toLowerCase().startsWith(typed));
  const contains = types.filter(type => !starts.includes(type) && type.title.toLowerCase().includes(typed));
  return [...starts, ...contains].map(type => type.path);
}

/** The rows of the open palette, which sits in an overlay outside the fixture. */
function paletteRows(): string[] {
  const list = document.querySelector('[role=dialog] [role=listbox]');
  return [...(list?.querySelectorAll('[role=option]') ?? [])].map(option => option.textContent.trim());
}

const typeCalled = (path: string, title: string): GraphNodeType => ({ ...noiseType, path, title, category: 'Filters' });

async function render(status: ServerState = online(true), graph: () => GraphState = state, types = [noiseType]) {
  const sockets: FakeSocket[] = [];
  const sent: unknown[] = [];
  const answer = async (input: RequestInfo | URL): Promise<Response> => {
    const request = input as Request;
    const { method, url } = request;
    const path = new URL(url).pathname.replace('/api/v1/', '');
    if (path === 'graphs' && method === 'POST') {
      return Response.json(graph());
    }

    if (path.endsWith('node-types/search')) {
      return Response.json({ paths: ranked(types, new URL(url).searchParams.get('query') ?? '') });
    }

    if (path.endsWith('/commands')) {
      sent.push(await request.json());
      return Response.json({ revision: 2, sequence: 1, history: state().history, created: [] });
    }

    if (path.endsWith('compounds')) {
      return Promise.resolve(Response.json([]));
    }

    return Response.json(path.endsWith('node-types') ? types : graph());
  };
  TestBed.configureTestingModule({
    providers: [
      {
        provide: SystemStatus,
        useValue: {
          state: signal(status),
          textureSides: signal([256, 512, 1024, 2048]),
          defaultTextureSide: signal(2048)
        }
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
  return { fixture, store, sockets, element, found, open, sent };
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

  it('adds what was typed when Enter comes before the ranking does', async () => {
    const types = [typeCalled('Filters/Auto Levels', 'Auto Levels'), typeCalled('Filters/Levels', 'Levels')];
    const { open, found, fixture, sent } = await render(online(true), state, types);
    await open();

    (found('graph-add') as HTMLButtonElement).click();
    await fixture.whenStable();
    const search = document.querySelector<HTMLInputElement>('input[role=combobox]');
    if (!search) {
      throw new Error('The palette did not open.');
    }

    // Everything is on screen, Auto Levels first, as it is when somebody starts typing.
    await vi.waitFor(() => {
      expect(paletteRows()[0]).toBe('Auto Levels');
    });
    search.value = 'Levels';
    search.dispatchEvent(new Event('input'));
    search.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    await vi.waitFor(() => {
      expect(sent).toHaveLength(1);
    });

    expect(sent[0]).toMatchObject({ commands: [{ op: 'addNode', type: 'Filters/Levels' }] });
  });

  it('adds what was typed when Enter comes before any list at all', async () => {
    const types = [typeCalled('Filters/Auto Levels', 'Auto Levels'), typeCalled('Filters/Levels', 'Levels')];
    const { open, found, fixture, sent } = await render(online(true), state, types);
    await open();

    (found('graph-add') as HTMLButtonElement).click();
    await fixture.whenStable();
    const search = document.querySelector<HTMLInputElement>('input[role=combobox]');
    if (!search) {
      throw new Error('The palette did not open.');
    }

    search.focus();
    search.value = 'Levels';
    search.dispatchEvent(new Event('input'));
    search.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    await vi.waitFor(() => {
      expect(sent).toHaveLength(1);
    });

    expect(sent[0]).toMatchObject({ commands: [{ op: 'addNode', type: 'Filters/Levels' }] });
  });

  it('shows the ranking as the Server gave it, not regrouped by category', async () => {
    const types = [
      { ...typeCalled('Adjust/Auto Levels', 'Auto Levels'), category: 'Adjust' },
      typeCalled('Filters/Levels', 'Levels')
    ];
    const { open, found, fixture } = await render(online(true), state, types);
    await open();

    (found('graph-add') as HTMLButtonElement).click();
    await fixture.whenStable();
    const search = document.querySelector<HTMLInputElement>('input[role=combobox]');
    if (search) {
      search.value = 'levels';
      search.dispatchEvent(new Event('input'));
    }

    await vi.waitFor(() => {
      expect(paletteRows()).toEqual(['Levels', 'Auto Levels']);
    });
  });

  it('offers the starred and the last added first with nothing typed, and remembers what was added', async () => {
    const types = [
      { ...typeCalled('Colour/Blend', 'Blend'), category: 'Colour' },
      typeCalled('Filters/Blur', 'Blur'),
      typeCalled('Filters/Levels', 'Levels')
    ];
    const { open, found, fixture, sent } = await render(online(true), state, types);
    const preferences = TestBed.inject(Preferences);
    preferences.set({ favouriteNodes: ['Filters/Levels'], recentNodes: ['Colour/Blend'] });
    await open();

    (found('graph-add') as HTMLButtonElement).click();
    await fixture.whenStable();
    await vi.waitFor(() => {
      expect(paletteRows().slice(0, 3)).toEqual(['Levels', 'Blend', 'Blend']);
    });
    const headings = [...document.querySelectorAll('[role=dialog] [role=listbox] [role=presentation]')].map(heading =>
      heading.textContent.trim()
    );
    expect(headings.slice(0, 3)).toEqual(['Favourites', 'Recent', 'Colour']);

    document.querySelector<HTMLElement>('[role=dialog] [role=option]')?.click();
    await vi.waitFor(() => {
      expect(sent).toHaveLength(1);
    });
    expect(preferences.recentNodes()).toEqual(['Filters/Levels', 'Colour/Blend']);
  });

  it('wires every selected node into a node added from the selection, top first', async () => {
    const graph = () => state({ nodes: [{ ...node(1), y: 300 }, node(2)], edges: [] });
    const { open, found, fixture, sent, store } = await render(online(true), graph, [
      typeCalled('Colour/Blend', 'Blend')
    ]);
    await open();
    store.select([1, 2]);
    await fixture.whenStable();

    (found('graph-add') as HTMLButtonElement).click();
    await fixture.whenStable();
    await vi.waitFor(() => {
      expect(paletteRows()).toContain('Blend');
    });
    document.querySelector<HTMLElement>('[role=dialog] [role=option]')?.click();
    await vi.waitFor(() => {
      expect(sent).toHaveLength(1);
    });

    expect(sent[0]).toMatchObject({
      commands: [
        { op: 'addNode', type: 'Colour/Blend' },
        { op: 'wire', fromNode: 2, toNode: -1 },
        { op: 'wire', fromNode: 1, toNode: -1 }
      ]
    });
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

  it('draws a preview box only for a picture a node has', async () => {
    const { open, sockets, element, fixture } = await render();
    await open();

    expect(element.querySelectorAll('xui-graph-node-preview')).toHaveLength(0);

    sockets[0]?.send(evaluated(1, { 1: 'a'.repeat(64) }));
    await fixture.whenStable();

    expect(element.querySelectorAll('xui-graph-node-preview')).toHaveLength(1);
  });

  it('draws no preview box for a type that has no picture worth one', async () => {
    const { open, sockets, element, fixture } = await render(online(true), state, [
      { ...noiseType, hasPreview: false }
    ]);
    await open();

    sockets[0]?.send(evaluated(1, { 1: 'a'.repeat(64) }));
    await fixture.whenStable();

    expect(element.querySelectorAll('xui-graph-node-preview')).toHaveLength(0);
  });

  it('leaves unsaved changes, Save and Close to the app', async () => {
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

    // The tab says so, and Save is the app's: the pane has neither of its own (#200).
    expect(found('graph-unsaved')).toBeNull();
    expect(found('graph-save')).toBeNull();
    expect(found('graph-close')).toBeNull();
  });

  it('lists diagnostics, such as a bitmap the Worker cannot read yet', async () => {
    const { open, sockets, found, fixture } = await render();
    await open();
    const event = evaluated(1, {});
    if (event.evaluation) {
      event.evaluation.diagnostics = [
        {
          id: 'TG0002',
          message:
            "'Input' has no image connected. There is no literal image an author could type into a port instead.",
          severity: 'Error',
          node: 1
        }
      ];
    }

    sockets[0]?.send(event);
    await fixture.whenStable();

    // A person reads what is wrong; the code and the compiler's reasoning wait behind Details (#198).
    const list = found('graph-diagnostics')?.textContent ?? '';
    expect(list).toContain("'Input' has no image connected.");
    expect(list).not.toContain('TG0002');
    expect(list).not.toContain('literal image');

    (found('graph-diagnostics-details') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(found('graph-diagnostics')?.textContent).toContain('TG0002');
    expect(found('graph-diagnostics')?.textContent).toContain('literal image');
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
