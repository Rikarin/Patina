import { TestBed } from '@angular/core/testing';
import { type GraphState, createPatinaClient } from '@patina/api-client';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';
import { LibrarySelection } from '../library/library-selection';
import { GRAPH_SOCKET, GraphStore, socketUrl } from './graph-store';
import { FakeSocket, clean, dirty, edited, evaluated, noDelta, node, noiseType, state } from './graph-testing';

const editReply = (sequence: number) =>
  Response.json({ revision: sequence + 1, sequence, history: dirty, created: [] });

const missing = () => Response.json({ detail: 'There is no open graph.' }, { status: 404 });

interface Harness {
  store: GraphStore;
  requests: { key: string; body: unknown }[];
  sockets: FakeSocket[];
  routes: Record<string, (request: Request) => Promise<Response> | Response>;
  socket: () => FakeSocket;
  commands: () => { commands: { op: string; value?: unknown }[]; merge: boolean }[];
}

function harness(opening: GraphState = state()): Harness {
  const requests: Harness['requests'] = [];
  const sockets: FakeSocket[] = [];
  const routes: Harness['routes'] = {
    'POST graphs': () => Response.json(opening),
    'GET graphs/g1': () => Response.json(opening),
    'GET graphs/g1/node-types': () => Response.json([noiseType]),
    'GET graphs/g1/compounds': () => Response.json([]),
    'POST graphs/g1/commands': () => editReply(1),
    'POST graphs/g1/undo': () => editReply(2),
    'POST graphs/g1/save': () => editReply(2),
    'POST graphs/g1/evaluate': () => new Response(null, { status: 202 }),
    'DELETE graphs/g1': () => new Response(null, { status: 204 })
  };
  const answer = async (input: RequestInfo | URL): Promise<Response> => {
    const request = input as Request;
    const path = new URL(request.url).pathname.replace('/api/v1/', '');
    const key = `${request.method} ${path}`;
    const text = await request.clone().text();
    requests.push({ key, body: text ? (JSON.parse(text) as unknown) : null });
    const route = routes[key];
    return route ? route(request) : Response.json({ detail: `no route for ${key}` }, { status: 500 });
  };
  TestBed.configureTestingModule({
    providers: [
      { provide: SERVER_CONNECTION, useValue: () => Promise.resolve({ baseUrl: 'http://server:1', token: 'secret' }) },
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
  return {
    store: TestBed.inject(GraphStore),
    requests,
    sockets,
    routes,
    socket: () => {
      const socket = sockets.at(-1);
      if (!socket) {
        throw new Error('No socket was opened.');
      }

      return socket;
    },
    commands: () =>
      requests.filter(request => request.key === 'POST graphs/g1/commands').map(request => request.body) as ReturnType<
        Harness['commands']
      >
  };
}

const settle = async () => {
  for (let turn = 0; turn < 20; turn++) {
    await new Promise(done => setTimeout(done));
  }
};

describe('GraphStore', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  async function opened(start?: GraphState): Promise<Harness> {
    const test = harness(start);
    await test.store.open('shared/graphs/a.vxtexgraph');
    test.socket().opened();
    return test;
  }

  it('builds the socket address, with the token the browser cannot put in a header', () => {
    expect(socketUrl({ baseUrl: 'http://127.0.0.1:5190', token: 't' }, 'g1', 7)).toBe(
      'ws://127.0.0.1:5190/ws/graphs/g1?since=7&access_token=t'
    );
    expect(socketUrl({ baseUrl: 'http://127.0.0.1:5190', token: null }, 'g1', 0)).toBe(
      'ws://127.0.0.1:5190/ws/graphs/g1?since=0'
    );
  });

  it('opens a graph and follows it from the number the state ended at', async () => {
    const { store, socket, requests } = await opened(state({ sequence: 12 }));

    expect(store.session().kind).toBe('open');
    expect(store.document()?.nodes.size).toBe(2);
    expect(store.types().get('Source/Noise')?.title).toBe('Noise');
    expect(socket().url).toContain('since=12');
    expect(requests[0]).toEqual({ key: 'POST graphs', body: { path: 'shared/graphs/a.vxtexgraph' } });
  });

  it('says why a file did not open, and can try again', async () => {
    const test = harness();
    test.routes['POST graphs'] = () => Response.json({ detail: 'There is no file.' }, { status: 404 });

    await test.store.open('nope');
    expect(test.store.session()).toEqual({ kind: 'failed', path: 'nope', reason: 'There is no file.' });

    test.routes['POST graphs'] = () => Response.json(state());
    await test.store.reopen();
    expect(test.store.session().kind).toBe('open');
  });

  it("applies a second client's edit from the socket, without a reload", async () => {
    const { store, socket, requests } = await opened();
    const reads = requests.length;

    socket().send(edited(1, [node(3)]));

    expect(store.document()?.nodes.has(3)).toBe(true);
    expect(requests).toHaveLength(reads);
  });

  it('changes nothing on its own: an edit shows when its event comes back', async () => {
    const { store, socket } = await opened();

    store.remove([2], []);
    await settle();
    expect(store.document()?.nodes.has(2)).toBe(true);

    socket().send({
      sequence: 1,
      kind: 'edited',
      revision: 2,
      history: dirty,
      delta: { nodes: [], removedNodes: [2], edges: [], removedEdges: [], settings: {}, removedSettings: [] }
    });
    expect(store.document()?.nodes.has(2)).toBe(false);
    expect(store.document()?.history.dirty).toBe(true);
  });

  it("shows a slider's own value until the event for it, and then the document", async () => {
    const { store, socket } = await opened();

    store.setValue(1, 'Scale', [5]);
    await settle();
    expect(store.valueOf(1, 'Scale', [8])).toEqual([5]);

    socket().send(edited(1, [node(1, { Scale: [5] })]));
    expect(store.valueOf(1, 'Scale', [8])).toEqual([5]);

    socket().send(edited(2, [node(1, { Scale: [9] })]));
    expect(store.valueOf(1, 'Scale', [8])).toEqual([9]);
  });

  it('coalesces a slider to one request out, the newest next, as one undo step', async () => {
    const test = await opened();
    let finish: (response: Response) => void = () => undefined;
    test.routes['POST graphs/g1/commands'] = () =>
      new Promise<Response>(resolve => {
        finish = resolve;
      });

    for (const value of [1, 2, 3, 4, 5]) {
      test.store.setValue(1, 'Scale', [value]);
    }

    await settle();
    expect(test.commands()).toHaveLength(1);
    expect(test.commands()[0]).toMatchObject({ merge: false, commands: [{ op: 'setValue', value: [1] }] });

    finish(editReply(1));
    await settle();
    expect(test.commands()).toHaveLength(2);
    expect(test.commands()[1]).toMatchObject({ merge: true, commands: [{ op: 'setValue', value: [5] }] });
    finish(editReply(2));
    await settle();

    test.store.endGesture();
    test.store.setValue(1, 'Scale', [6]);
    await settle();
    expect(test.commands()[2]).toMatchObject({ merge: false });
  });

  it('sends a drag as moveNodes, folded into one step, with every node in every batch', async () => {
    const test = await opened();

    test.store.moveNode(1, { x: 10, y: 0 });
    test.store.moveNode(2, { x: 20, y: 0 });
    await settle();
    test.store.moveNode(1, { x: 30, y: 0 });
    test.store.moveNode(2, { x: 40, y: 0 });
    await settle();
    test.store.endMove();
    await settle();
    test.store.moveNode(1, { x: 50, y: 0 });
    await settle();

    expect(test.commands().map(batch => batch.merge)).toEqual([false, true, false]);
    expect(test.commands()[1]?.commands).toEqual([
      {
        op: 'moveNodes',
        positions: [
          { node: 1, x: 30, y: 0 },
          { node: 2, x: 40, y: 0 }
        ]
      }
    ]);
    expect(test.commands()[2]?.commands).toEqual([{ op: 'moveNodes', positions: [{ node: 1, x: 50, y: 0 }] }]);
  });

  it('holds a dragged node where the hand put it, whatever older echoes say', async () => {
    const { store, socket } = await opened();

    store.moveNode(1, { x: 500, y: 0 });
    socket().send(edited(1, [{ ...node(1), x: 200 }]));

    expect(store.positions().get('1')).toEqual({ x: 500, y: 0 });
    await settle();
    socket().send(edited(2, [{ ...node(1), x: 500 }]));
    expect(store.positions().size).toBe(0);
  });

  it('shows a refusal and goes back to the document', async () => {
    const test = await opened();
    test.routes['POST graphs/g1/commands'] = () => Response.json({ detail: 'Not that.' }, { status: 422 });

    test.store.setValue(1, 'Scale', [5]);
    await settle();

    expect(test.store.problem()).toBe('Not that.');
    expect(test.store.valueOf(1, 'Scale', [8])).toEqual([8]);
  });

  it('adds a node with a wire, in one batch, and selects what it made', async () => {
    const test = await opened();
    test.routes['POST graphs/g1/commands'] = () =>
      Response.json({ revision: 2, sequence: 1, history: dirty, created: [{ ref: -1, node: 9 }] });

    await test.store.addNode(
      { ...noiseType, ports: [{ name: 'Input', direction: 'Input', kind: 'Image', default: [], summary: '' }] },
      { x: 1, y: 2 },
      { node: 1, port: 'Out', incoming: false }
    );

    expect(test.commands()[0]?.commands).toEqual([
      { op: 'addNode', type: 'Source/Noise', x: 1, y: 2, ref: -1 },
      { op: 'connect', fromNode: 1, fromPort: 'Out', toNode: -1, toPort: 'Input' }
    ]);
    expect([...test.store.selection()]).toEqual([9]);
  });

  it('turns a selected wire into a disconnect', async () => {
    const test = await opened();

    test.store.remove([], ['2/Input']);
    await settle();

    expect(test.commands()[0]?.commands).toEqual([{ op: 'disconnect', toNode: 2, toPort: 'Input' }]);
  });

  it('after a dropped socket, asks for what came after the last event it applied', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    const { store, socket, sockets } = await opened();
    socket().send(edited(1, [node(3)]));
    socket().send(evaluated(2, {}));

    socket().dropped();
    expect(store.link()).toBe('reconnecting');
    expect(store.editable()).toBe(false);

    await vi.advanceTimersByTimeAsync(1_000);
    expect(sockets).toHaveLength(2);
    expect(socket().url).toContain('since=2');

    socket().opened();
    expect(store.link()).toBe('live');
    socket().send(edited(1, [node(9)]));
    expect(store.document()?.nodes.has(9)).toBe(false);
    socket().send(edited(3, [node(9)]));
    expect(store.document()?.nodes.has(9)).toBe(true);
  });

  it('keeps trying while the Server is down', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    const test = await opened();
    test.routes['GET graphs/g1'] = () => {
      throw new TypeError('fetch failed');
    };

    test.socket().dropped();
    await vi.advanceTimersByTimeAsync(2_000);
    expect(test.sockets).toHaveLength(1);
    expect(test.store.session().kind).toBe('open');

    test.routes['GET graphs/g1'] = () => Response.json(state());
    await vi.advanceTimersByTimeAsync(5_000);
    expect(test.sockets).toHaveLength(2);
  });

  it('reads the state again on a resync, and on a gap', async () => {
    const test = await opened();
    test.routes['GET graphs/g1'] = () => Response.json(state({ sequence: 40, nodes: [node(7)], edges: [] }));

    test.socket().send({ sequence: 40, kind: 'resync', revision: 9, history: clean });
    await settle();
    expect([...(test.store.document()?.nodes.keys() ?? [])]).toEqual([7]);
    expect(test.store.document()?.sequence).toBe(40);

    test.routes['GET graphs/g1'] = () => Response.json(state({ sequence: 50, nodes: [node(8)], edges: [] }));
    test.socket().send(edited(45, [node(1)]));
    await settle();
    expect([...(test.store.document()?.nodes.keys() ?? [])]).toEqual([8]);
  });

  it('applies events that came during a reload after it', async () => {
    const test = await opened();
    let finish: (response: Response) => void = () => undefined;
    test.routes['GET graphs/g1'] = () =>
      new Promise<Response>(resolve => {
        finish = resolve;
      });

    test.socket().send(edited(5, [node(1)]));
    await settle();
    test.socket().send(edited(6, [node(4)]));
    test.socket().send(edited(7, [node(5)]));
    finish(Response.json(state({ sequence: 6, nodes: [node(1)], edges: [] })));
    await settle();

    expect([...(test.store.document()?.nodes.keys() ?? [])]).toEqual([1, 5]);
  });

  it('opens the file again after a Server restart, and says edits are gone', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    const test = await opened();
    test.socket().send(edited(1, [node(3)]));
    test.routes['GET graphs/g1'] = missing;

    test.socket().dropped();
    await vi.advanceTimersByTimeAsync(1_000);

    expect(test.requests.at(-3)?.key).toBe('POST graphs');
    expect(test.requests.at(-3)?.body).toEqual({ path: '/lib/shared/graphs/a.vxtexgraph' });
    expect(test.store.notice()).toContain('Edits since the last save are gone');
    expect(test.store.document()?.nodes.has(3)).toBe(false);
    expect(test.sockets).toHaveLength(2);
  });

  it('says so when another client closes the graph', async () => {
    const { store, socket } = await opened();

    socket().send({ sequence: 1, kind: 'closed', revision: 1, history: clean });

    expect(store.session()).toEqual({
      kind: 'ended',
      path: '/lib/shared/graphs/a.vxtexgraph',
      name: 'A'
    });
    expect(store.document()).toBeNull();
  });

  it('asks before leaving unsaved edits, and discards when told to', async () => {
    const test = await opened(state({ history: dirty }));

    await test.store.close();
    expect(test.store.leaving()).toEqual({ kind: 'close' });
    expect(test.store.session().kind).toBe('open');

    await test.store.resolveLeaving('discard');
    expect(test.store.session().kind).toBe('none');
    expect(test.requests.at(-1)?.key).toBe('DELETE graphs/g1');
  });

  it('saves first when asked to, and stays if the save fails', async () => {
    const test = await opened(state({ history: dirty }));
    test.routes['POST graphs/g1/save'] = () => Response.json({ detail: 'Changed.' }, { status: 409 });

    await test.store.open('shared/graphs/b.vxtexgraph');
    expect(test.store.leaving()).toEqual({ kind: 'open', path: 'shared/graphs/b.vxtexgraph' });
    await test.store.resolveLeaving('save');

    expect(test.store.saveConflict()).toBe(true);
    expect(test.store.session().kind).toBe('open');
  });

  it('forces a save only when asked', async () => {
    const test = await opened();
    let query = '';
    test.routes['POST graphs/g1/save'] = request => {
      query = new URL(request.url).search;
      return editReply(2);
    };

    await test.store.save(true);

    expect(query).toBe('?force=true');
  });

  it('does not open what is already open', async () => {
    const test = await opened();
    const reads = test.requests.length;

    await test.store.open('shared/graphs/a.vxtexgraph');

    expect(test.requests).toHaveLength(reads);
  });

  it('gives the Inspector back to the Library when it selects something', async () => {
    const { store, socket } = await opened();
    store.select([1]);
    socket().opened();
    expect(store.selectedNode()?.id).toBe(1);

    TestBed.inject(LibrarySelection).clear();
    TestBed.tick();
    expect(store.selectedNode()?.id).toBe(1); // nothing changed: nothing was selected there

    TestBed.inject(LibrarySelection).select({
      kind: 'project',
      project: { id: 'p', name: 'P', path: 'p', tags: [], referenceCount: 0 }
    });
    TestBed.tick();
    expect(store.selectedNode()).toBeNull();
  });

  it('drops a removed node from the selection', async () => {
    const { store, socket } = await opened();
    store.select([2]);

    socket().send({
      sequence: 1,
      kind: 'edited',
      revision: 2,
      history: dirty,
      delta: { nodes: [], removedNodes: [2], edges: [], removedEdges: [], settings: {}, removedSettings: [] }
    });

    expect(store.selection().size).toBe(0);
  });
});

describe('what a graph holds besides its nodes', () => {
  async function opened(opening: GraphState = state()): Promise<Harness> {
    const test = harness(opening);
    await test.store.open('a.vxtexgraph');
    test.socket().opened();
    return test;
  }

  const sent = (test: Harness) => test.commands().flatMap(batch => batch.commands);

  it('asks the Server to create a graph, then opens the path it answers with', async () => {
    const test = harness();
    test.routes['POST library/graphs'] = () =>
      Response.json({ path: 'shared/graphs/rust.vxtexgraph' }, { status: 201 });
    await test.store.create('Rust', null);
    expect(test.requests.find(request => request.key === 'POST library/graphs')?.body).toEqual({
      name: 'Rust',
      project: null
    });
    expect(test.requests.find(request => request.key === 'POST graphs')?.body).toEqual({
      path: 'shared/graphs/rust.vxtexgraph'
    });
    expect(test.store.session().kind).toBe('open');
  });

  it('lets a refused name reach whoever asked', async () => {
    const test = harness();
    test.routes['POST library/graphs'] = () => Response.json({ detail: 'It exists already.' }, { status: 409 });
    await expect(test.store.create('Rust', null)).rejects.toThrow('It exists already.');
  });

  it('sends graph settings, and shows them only once the delta arrives', async () => {
    const test = await opened();
    test.store.setSetting('seed', '9');
    await vi.waitFor(() => {
      expect(sent(test)).toEqual([{ op: 'setSetting', key: 'seed', value: '9' }]);
    });
    expect(test.store.document()?.settings['seed']).toBe('7');
    test.socket().send({
      sequence: 1,
      kind: 'edited',
      revision: 2,
      history: dirty,
      delta: { ...noDelta, settings: { seed: '9' } }
    });
    expect(test.store.document()?.settings['seed']).toBe('9');
  });

  const wear = {
    name: 'Wear',
    default: '0.5',
    summary: '',
    kind: 'Float',
    minimum: 0,
    maximum: 1,
    group: '',
    accepted: []
  };

  it('folds a parameter slider into one step, and keeps the hand ahead of the delta', async () => {
    const test = await opened(state({ parameters: [wear] }));
    test.store.setParameterDefault(wear, '0.6');
    expect(test.store.parameterDefault(wear)).toBe('0.6');
    await vi.waitFor(() => {
      expect(test.commands().length).toBe(1);
    });
    test.store.setParameterDefault(wear, '0.7');
    await vi.waitFor(() => {
      expect(test.commands().length).toBe(2);
    });
    expect(test.commands().map(batch => batch.merge)).toEqual([false, true]);
    test.store.endGesture();
  });

  it('takes the whole parameter, group and note lists from a delta', async () => {
    const test = await opened();
    test.socket().send({
      sequence: 1,
      kind: 'edited',
      revision: 2,
      history: dirty,
      delta: {
        ...noDelta,
        parameters: [wear],
        groups: [{ id: 1, title: 'Base', nodes: [1] }],
        comments: [{ id: 1, text: 'Hello', x: 1, y: 2, width: 200, height: 100 }]
      }
    });
    const document = test.store.document();
    expect(document?.parameters).toEqual([wear]);
    expect(document?.groups[0]?.title).toBe('Base');
    expect(document?.comments[0]?.text).toBe('Hello');
  });

  it('groups the selection, taking its nodes out of the frames they were in', async () => {
    const test = await opened(state({ groups: [{ id: 7, title: 'Old', nodes: [1, 2] }] }));
    test.store.select([1]);
    test.store.groupSelection();
    await vi.waitFor(() => {
      expect(sent(test)).toEqual([
        { op: 'setGroup', id: 7, nodes: [2] },
        { op: 'addGroup', title: 'Group', nodes: [1] }
      ]);
    });
  });

  it('moves a node between frames when it comes to rest, and leaves the rest alone', async () => {
    const test = await opened(
      state({
        groups: [
          { id: 4, title: 'A', nodes: [1] },
          { id: 9, title: 'B', nodes: [] }
        ]
      })
    );
    test.store.regroup([{ node: 1, group: 'group-4' }]);
    expect(test.commands()).toEqual([]);
    test.store.regroup([{ node: 1, group: 'group-9' }]);
    await vi.waitFor(() => {
      expect(sent(test)).toEqual([
        { op: 'setGroup', id: 4, nodes: [] },
        { op: 'setGroup', id: 9, nodes: [1] }
      ]);
    });
  });

  it('selects a new note, and forgets a selected note that has gone', async () => {
    const test = await opened();
    test.routes['POST graphs/g1/commands'] = () =>
      Response.json({ revision: 2, sequence: 1, history: dirty, created: [{ ref: -1, node: 12 }] });
    await test.store.addComment({ x: 1, y: 2 });
    expect(sent(test)).toEqual([{ op: 'addComment', text: 'Note', x: 1, y: 2, ref: -1 }]);
    expect(test.store.selectedNote()).toBe(12);
    test.socket().send({
      sequence: 1,
      kind: 'edited',
      revision: 2,
      history: dirty,
      delta: { ...noDelta, comments: [] }
    });
    expect(test.store.selectedNote()).toBeNull();
  });

  it('edits a note by its identity, wherever it stands in the list', async () => {
    const note = { id: 12, text: 'Two', x: 0, y: 0, width: 200, height: 100 };
    const test = await opened(state({ comments: [{ ...note, id: 3, text: 'One' }, note] }));
    test.store.selectNote(12);
    test.store.setCommentText(12, 'Second');
    await vi.waitFor(() => {
      expect(sent(test)).toEqual([{ op: 'setComment', id: 12, text: 'Second' }]);
    });

    // Another client took the first note away, and the selected one moved up.
    test.socket().send({
      sequence: 1,
      kind: 'edited',
      revision: 2,
      history: dirty,
      delta: { ...noDelta, comments: [note] }
    });
    expect(test.store.selectedNote()).toBe(12);
  });

  it('sends ports as commands, and reads the node types again when the interface changes', async () => {
    const test = await opened();
    const port = { name: 'Mask', direction: 'Input', kind: 'Image', default: [], summary: '' };
    test.store.addPort(port);
    await vi.waitFor(() => {
      expect(sent(test).at(-1)).toEqual({ op: 'addPort', port });
    });
    test.store.editPort('Mask', 'Input', { ...port, name: 'Wear' });
    await vi.waitFor(() => {
      expect(sent(test).at(-1)).toEqual({
        op: 'setPort',
        name: 'Mask',
        direction: 'Input',
        port: { ...port, name: 'Wear' }
      });
    });
    test.store.removePort('Wear', 'Input');
    await vi.waitFor(() => {
      expect(sent(test).at(-1)).toEqual({ op: 'removePort', name: 'Wear', direction: 'Input' });
    });

    const types = () => test.requests.filter(request => request.key === 'GET graphs/g1/node-types').length;
    expect(types()).toBe(1);
    test.socket().send({
      sequence: 1,
      kind: 'edited',
      revision: 2,
      history: dirty,
      delta: { ...noDelta, interface: [port] }
    });
    expect(test.store.document()?.interface).toEqual([port]);
    await vi.waitFor(() => {
      expect(types()).toBe(2);
    });
  });

  it('saves the selection as a compound at its centre, and selects the node that holds it', async () => {
    const test = await opened();
    test.routes['POST graphs/g1/compounds'] = () =>
      Response.json({
        compound: { type: 'Wear', path: '/lib/Compounds/Wear.vxtexgraph', readOnly: false },
        node: 9,
        edit: { revision: 2, sequence: 1, history: dirty, created: [] }
      });
    test.store.select([1, 2]);
    await expect(test.store.saveSelectionAsCompound('Wear')).resolves.toBeNull();
    expect(test.requests.at(-1)?.body).toEqual({ name: 'Wear', nodes: [1, 2], x: 150, y: 0 });
    expect([...test.store.selection()]).toEqual([9]);
  });

  it('says why a selection could not be saved', async () => {
    const test = await opened();
    test.routes['POST graphs/g1/compounds'] = () => Response.json({ detail: 'That is taken.' }, { status: 422 });
    test.store.select([1]);
    await expect(test.store.saveSelectionAsCompound('Wear')).resolves.toContain('That is taken.');
  });

  it('copies a shipped compound and opens the copy', async () => {
    const shipped = state({ path: 'shipped:Patterns/Cells', readOnly: true });
    const test = await opened(shipped);
    test.routes['POST library/compounds'] = () =>
      Response.json({ type: 'Patterns/Cells Copy', path: '/lib/c.vxtexgraph', readOnly: false }, { status: 201 });
    test.routes['POST graphs'] = () => Response.json(state({ path: '/lib/c.vxtexgraph' }));
    await test.store.copyToLibrary();
    expect(test.requests.find(request => request.key === 'POST library/compounds')?.body).toEqual({
      type: 'Patterns/Cells',
      graph: null
    });
    expect(test.store.document()?.path).toBe('/lib/c.vxtexgraph');
    expect(test.store.document()?.readOnly).toBe(false);
  });

  it('asks for the node types again when a compound was saved', async () => {
    const test = await opened();
    const types = () => test.requests.filter(request => request.key === 'GET graphs/g1/node-types').length;
    expect(types()).toBe(1);
    test.socket().send({ sequence: 1, kind: 'typesChanged', revision: 1, history: clean });
    await vi.waitFor(() => {
      expect(types()).toBe(2);
    });
  });
});

describe('going into a compound', () => {
  const compound = state({ id: 'g2', path: '/lib/Compounds/Mine.vxtexgraph', name: 'Mine', nodes: [], edges: [] });

  async function inside(outer: GraphState = state(), inner: GraphState = compound): Promise<Harness> {
    const test = harness(outer);
    test.routes['GET graphs/g1/compounds'] = () => Response.json([{ type: 'Mine', path: inner.path, readOnly: false }]);
    await test.store.open('a.vxtexgraph');
    test.routes['POST graphs'] = () => Response.json(inner);
    test.routes['GET graphs/g2/node-types'] = () => Response.json([noiseType]);
    test.routes['GET graphs/g2/compounds'] = () => Response.json([]);
    test.routes['DELETE graphs/g2'] = () => new Response(null, { status: 204 });
    await test.store.enterCompound('Mine');
    test.routes['POST graphs'] = () => Response.json(outer);
    return test;
  }

  it('opens it and keeps the way back, leaving the outer session open', async () => {
    const test = await inside();
    expect(test.store.document()?.id).toBe('g2');
    expect(test.store.trail().map(crumb => crumb.name)).toEqual(['A']);
    expect(test.requests.some(request => request.key === 'DELETE graphs/g1')).toBe(false);
  });

  it('comes back by closing the compound and joining the outer session', async () => {
    const test = await inside();
    await test.store.back(0);
    expect(test.requests.some(request => request.key === 'DELETE graphs/g2')).toBe(true);
    expect(test.store.document()?.id).toBe('g1');
    expect(test.store.trail()).toEqual([]);
  });

  it('asks before leaving a compound with unsaved edits', async () => {
    const test = await inside(state(), { ...compound, history: dirty });
    await test.store.back(0);
    expect(test.store.leaving()).toEqual({ kind: 'back', index: 0 });
    expect(test.store.document()?.id).toBe('g2');
    await test.store.resolveLeaving('discard');
    expect(test.store.document()?.id).toBe('g1');
  });

  it('counts unsaved edits on the way in, and saves them when asked to leave everything', async () => {
    const test = await inside(state({ history: dirty }));
    test.routes['POST graphs/g2/save'] = () => editReply(2);
    expect(test.store.unsaved()).toBe(true);
    await test.store.close();
    expect(test.store.leaving()).toEqual({ kind: 'close' });
    await test.store.resolveLeaving('save');
    expect(test.requests.some(request => request.key === 'POST graphs/g1/save')).toBe(true);
    expect(test.requests.some(request => request.key === 'DELETE graphs/g1')).toBe(true);
    expect(test.store.session().kind).toBe('none');
  });
});

describe('leaving the app', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('says whether there is anything to lose, and quits only after the answer', async () => {
    const said: boolean[] = [];
    let requested: () => void = () => undefined;
    const quit = vi.fn();
    vi.stubGlobal('patina', {
      setUnsaved: (unsaved: boolean) => said.push(unsaved),
      onQuitRequested: (listener: () => void) => {
        requested = listener;
      },
      quit
    });
    const test = harness(state({ history: dirty }));
    await test.store.open('a.vxtexgraph');
    TestBed.tick();
    expect(said.at(-1)).toBe(true);

    requested();
    expect(test.store.leaving()).toEqual({ kind: 'quit' });
    await test.store.resolveLeaving('cancel');
    expect(quit).not.toHaveBeenCalled();

    requested();
    await test.store.resolveLeaving('discard');
    expect(quit).toHaveBeenCalledOnce();
  });
});

describe('the shortcuts', () => {
  const press = (key: string, modifiers: KeyboardEventInit = { ctrlKey: true }) =>
    new KeyboardEvent('keydown', { key, cancelable: true, ...modifiers });

  it('undoes, redoes and saves, and claims nothing else', async () => {
    const test = harness();
    test.routes['POST graphs/g1/redo'] = () => editReply(2);
    await test.store.open('a.vxtexgraph');

    expect(test.store.shortcut(press('a'))).toBe(false);
    expect(test.store.shortcut(press('z', {}))).toBe(false);

    const undo = press('z');
    expect(test.store.shortcut(undo)).toBe(true);
    expect(undo.defaultPrevented).toBe(true);
    expect(test.store.shortcut(press('z', { metaKey: true, shiftKey: true }))).toBe(true);
    expect(test.store.shortcut(press('s'))).toBe(true);
    await vi.waitFor(() => {
      const keys = test.requests.map(request => request.key);
      expect(keys).toContain('POST graphs/g1/undo');
      expect(keys).toContain('POST graphs/g1/redo');
      expect(keys).toContain('POST graphs/g1/save');
    });
  });
});
