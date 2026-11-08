import { DraftLayer } from './draft-layer';
import { EditQueue } from './edit-queue';

/** A send that waits for the test to answer it. */
function gate() {
  let open: (value: number) => void = () => undefined;
  let fail: (reason: unknown) => void = () => undefined;
  const answer = new Promise<number>((resolve, reject) => {
    open = resolve;
    fail = reject;
  });
  return {
    answer,
    open: (value: number) => {
      open(value);
    },
    fail: (reason: unknown) => {
      fail(reason);
    }
  };
}

describe('EditQueue', () => {
  it('never has more than one request out', async () => {
    const queue = new EditQueue<number>();
    const first = gate();
    const second = gate();
    const sent: string[] = [];

    void queue.submit(() => {
      sent.push('first');
      return first.answer;
    });
    const later = queue.submit(() => {
      sent.push('second');
      return second.answer;
    });
    expect(sent).toEqual(['first']);

    first.open(1);
    await first.answer;
    await Promise.resolve();
    await Promise.resolve();
    expect(sent).toEqual(['first', 'second']);
    second.open(2);
    await expect(later).resolves.toBe(2);
  });

  it('sends only the newest of a slider while one is out, and everyone gets its answer', async () => {
    const queue = new EditQueue<number>();
    const first = gate();
    const sent: number[] = [];
    const slider = (value: number) => (): Promise<number> => {
      sent.push(value);
      return value === 1 ? first.answer : Promise.resolve(value);
    };

    void queue.submit(slider(1), { key: 'scale' });
    const replies = [2, 3, 4, 5].map(value => queue.submit(slider(value), { key: 'scale', merge: true }));
    expect(queue.pending()).toBe(2);

    first.open(1);
    await expect(Promise.all(replies)).resolves.toEqual([5, 5, 5, 5]);
    expect(sent).toEqual([1, 5]);
  });

  it('opens an undo step if any batch it replaces would have', async () => {
    const queue = new EditQueue<number>();
    const first = gate();
    const merges: boolean[] = [];

    void queue.submit(() => first.answer);
    const opening = queue.submit(
      merge => {
        merges.push(merge);
        return Promise.resolve(1);
      },
      { key: 'scale', merge: false }
    );
    void queue.submit(
      merge => {
        merges.push(merge);
        return Promise.resolve(2);
      },
      { key: 'scale', merge: true }
    );

    first.open(0);
    await opening;
    expect(merges).toEqual([false]);
  });

  it('does not reorder around a different edit', async () => {
    const queue = new EditQueue<string>();
    const first = gate();
    const sent: string[] = [];
    const send = (name: string) => () => {
      sent.push(name);
      return Promise.resolve(name);
    };

    void queue.submit(async () => {
      await first.answer;
      sent.push('out');
      return 'out';
    });
    void queue.submit(send('a1'), { key: 'a' });
    void queue.submit(send('add'));
    const last = queue.submit(send('a2'), { key: 'a' });

    first.open(0);
    await last;
    expect(sent).toEqual(['out', 'a1', 'add', 'a2']);
  });

  it('carries on after a refused edit', async () => {
    const queue = new EditQueue<number>();
    const refused = queue.submit(() => Promise.reject(new Error('no')));
    const next = queue.submit(() => Promise.resolve(2));

    await expect(refused).rejects.toThrow('no');
    await expect(next).resolves.toBe(2);
  });

  it('rejects what never went out when the session ends', async () => {
    const queue = new EditQueue<number>();
    const first = gate();
    void queue.submit(() => first.answer);
    const waiting = queue.submit(() => Promise.resolve(1));

    queue.clear(new Error('gone'));

    await expect(waiting).rejects.toThrow('gone');
  });
});

describe('DraftLayer', () => {
  it('keeps a draft until the event that carries it has been applied', () => {
    const drafts = new DraftLayer<number>();
    drafts.set('a', 1);

    drafts.settle('a', 1, 5, 3);
    expect(drafts.values().get('a')).toBe(1);

    drafts.reached(4);
    expect(drafts.values().get('a')).toBe(1);
    drafts.reached(5);
    expect(drafts.values().has('a')).toBe(false);
  });

  it('goes at once when the event came before the reply', () => {
    const drafts = new DraftLayer<number>();
    drafts.set('a', 1);

    drafts.settle('a', 1, 5, 5);

    expect(drafts.values().has('a')).toBe(false);
  });

  it('ignores an answer to a value the hand has already left', () => {
    const drafts = new DraftLayer<number>();
    drafts.set('a', 1);
    drafts.set('a', 2);

    drafts.settle('a', 1, 5, 9);

    expect(drafts.values().get('a')).toBe(2);
  });
});
