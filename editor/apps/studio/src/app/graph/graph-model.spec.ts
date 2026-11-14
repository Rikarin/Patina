import { applyEvent, classify, diagnosticsOf, documentOf, edgeKey, evaluationSummary } from './graph-model';
import { clean, edited, evaluated, node, noDelta, state } from './graph-testing';

describe('graph events', () => {
  it('applies the next event, and treats a replay as seen', () => {
    const document = documentOf(state({ sequence: 4 }));

    expect(classify(document, edited(5, []))).toBe('apply');
    expect(classify(document, edited(4, []))).toBe('seen');
    expect(classify(document, edited(2, []))).toBe('seen');
  });

  it('reads the state again after a gap or a resync', () => {
    const document = documentOf(state({ sequence: 4 }));

    expect(classify(document, edited(6, []))).toBe('reload');
    expect(classify(document, { sequence: 9, kind: 'resync', revision: 3, history: clean })).toBe('reload');
  });

  it('says a session has ended, in order', () => {
    const document = documentOf(state({ sequence: 4 }));

    expect(classify(document, { sequence: 5, kind: 'closed', revision: 3, history: clean })).toBe('closed');
  });

  it('replaces changed nodes whole and adds new ones', () => {
    const next = applyEvent(documentOf(state()), edited(1, [node(2, { Scale: [3] }), node(3)]));

    expect(next.nodes.get(2)?.values).toEqual({ Scale: [3] });
    expect(next.nodes.size).toBe(3);
    expect(next.sequence).toBe(1);
    expect(next.history.dirty).toBe(true);
  });

  it('keeps the GPU time of a node the result cache answers for, and drops it with the node', () => {
    const before = documentOf(
      state({
        times: [
          { node: 1, milliseconds: 2.5 },
          { node: 2, milliseconds: 0.5 }
        ]
      })
    );
    const event = evaluated(1, {});
    const times = [{ node: 2, milliseconds: 0.75 }];
    const ran = applyEvent(before, {
      ...event,
      ...(event.evaluation ? { evaluation: { ...event.evaluation, times } } : {})
    });
    const removed = applyEvent(ran, { ...edited(2, []), delta: { ...noDelta, removedNodes: [1] } });

    expect([...ran.times]).toEqual([
      [1, 2.5],
      [2, 0.75]
    ]);
    expect([...removed.times]).toEqual([[2, 0.75]]);
  });

  it('leaves untouched nodes as they were, so that a view can keep them', () => {
    const before = documentOf(state());
    const next = applyEvent(before, edited(1, [node(2, { Scale: [3] })]));

    expect(next.nodes.get(1)).toBe(before.nodes.get(1));
  });

  it('removes nodes and their previews, and swaps wires by the input they arrive at', () => {
    const start = documentOf(state({ previews: [{ node: 2, port: 'Out', hash: 'a', width: 1, height: 1 }] }));
    const wire = { fromNode: 3, fromPort: 'Out', toNode: 2, toPort: 'Input' };
    const next = applyEvent(start, {
      sequence: 1,
      kind: 'undone',
      revision: 2,
      history: clean,
      delta: {
        ...noDelta,
        removedNodes: [2],
        edges: [wire],
        removedEdges: [{ fromNode: 1, fromPort: 'Out', toNode: 2, toPort: 'Input' }]
      }
    });

    expect(next.nodes.has(2)).toBe(false);
    expect(next.previews.has(2)).toBe(false);
    expect(next.edges.get(edgeKey(wire))).toEqual(wire);
    expect(next.edges.size).toBe(1);
  });

  it('applies settings', () => {
    const next = applyEvent(documentOf(state({ settings: { seed: '7', baseWidth: '64' } })), {
      sequence: 1,
      kind: 'edited',
      revision: 2,
      history: clean,
      delta: { ...noDelta, settings: { seed: '9' }, removedSettings: ['baseWidth'] }
    });

    expect(next.settings).toEqual({ seed: '9' });
  });

  it('keeps the previews an evaluation did not list', () => {
    const start = documentOf(
      state({
        previews: [
          { node: 1, port: 'Out', hash: 'old-1', width: 1, height: 1 },
          { node: 2, port: 'Out', hash: 'old-2', width: 1, height: 1 }
        ]
      })
    );
    const next = applyEvent(start, evaluated(1, { 2: 'new-2' }));

    expect(next.previews.get(1)?.hash).toBe('old-1');
    expect(next.previews.get(2)?.hash).toBe('new-2');
  });

  it('ignores a preview of a node that has gone since the evaluation began', () => {
    const next = applyEvent(documentOf(state()), evaluated(1, { 99: 'late' }));

    expect(next.previews.has(99)).toBe(false);
  });

  it('lists what the compiler said with what the file needed', () => {
    const repair = { id: 'TG0005', message: 'dropped', severity: 'Warning' };
    const problem = { id: 'PT0001', message: 'no bitmaps yet', severity: 'Error' };
    const event = evaluated(1, {});
    if (event.evaluation) {
      event.evaluation.diagnostics = [problem];
    }

    const next = applyEvent(documentOf(state({ repairs: [repair] })), event);

    expect(diagnosticsOf(next)).toEqual([problem, repair]);
  });
});

describe('evaluationSummary', () => {
  const evaluation = {
    revision: 3,
    compiled: false,
    ran: 4,
    reused: 1,
    milliseconds: 12.4,
    previews: [],
    diagnostics: [],
    times: []
  };
  const summary = (overrides: Parameters<typeof state>[0]) => evaluationSummary(documentOf(state(overrides)));

  it('says what the last evaluation did', () => {
    expect(summary({ revision: 3, settled: 3, evaluation })).toBe('Updated in 12 ms');
  });

  it('never leaves an old count showing while an edit is on its way', () => {
    expect(summary({ revision: 4, settled: 3, evaluation })).toBe('Evaluating…');
  });

  it('says a graph is up to date rather than that nothing ran in no time', () => {
    expect(summary({ revision: 3, settled: 3, evaluation: { ...evaluation, ran: 0, milliseconds: 0.2 } })).toBe(
      'Up to date'
    );
  });

  it('does not call a graph that did not compile evaluated', () => {
    const diagnostics = [{ id: 'TG0001', message: 'No such type.', severity: 'Error' }];
    expect(summary({ revision: 3, settled: 3, evaluation: { ...evaluation, ran: 0, diagnostics } })).toBe(
      'Not evaluated'
    );
  });

  it('keeps an evaluation current past a revision that needed none', () => {
    const document = documentOf(state({ revision: 3, settled: 3, evaluation, sequence: 5 }));
    const moved = applyEvent(document, {
      sequence: 6,
      kind: 'edited',
      revision: 4,
      history: clean,
      delta: noDelta
    });
    expect(evaluationSummary(moved)).toBe('Evaluating…');
    const settled = applyEvent(moved, { sequence: 7, kind: 'settled', revision: 4, history: clean });
    expect(evaluationSummary(settled)).toBe('Updated in 12 ms');
  });
});
