import { PreviewLoader } from './preview-loader';

const preview = (node: number, hash: string) => ({ node, port: 'Out', hash, width: 256, height: 256 });

/** A Server whose previews each wait to be released. */
function source() {
  const waiting = new Map<string, { resolve: (blob: Blob) => void; reject: (reason: unknown) => void }>();
  const revoked: string[] = [];
  const fetched: string[] = [];
  return {
    waiting,
    revoked,
    fetched,
    fetch: (hash: string) => {
      fetched.push(hash);
      return new Promise<Blob>((resolve, reject) => {
        waiting.set(hash, { resolve, reject });
      });
    },
    url: (blob: Blob) => `blob:${String(blob.size)}/${String(fetched.length)}`,
    revoke: (url: string) => revoked.push(url)
  };
}

const settle = () => new Promise(done => setTimeout(done));

describe('PreviewLoader', () => {
  it('keeps the old picture until the new one has arrived', async () => {
    const server = source();
    const loader = new PreviewLoader(server, () => undefined);
    loader.want(preview(1, 'a'));
    server.waiting.get('a')?.resolve(new Blob(['a']));
    await settle();
    expect(loader.shown(1)()?.hash).toBe('a');

    loader.want(preview(1, 'b'));
    await settle();
    expect(loader.shown(1)()?.hash).toBe('a');

    server.waiting.get('b')?.resolve(new Blob(['bb']));
    await settle();
    expect(loader.shown(1)()?.hash).toBe('b');
  });

  it('fetches only the newest of many hashes for a node', async () => {
    const server = source();
    const loader = new PreviewLoader(server, () => undefined);
    loader.want(preview(1, 'a'));
    loader.want(preview(1, 'b'));
    loader.want(preview(1, 'c'));
    loader.want(preview(1, 'd'));
    expect(server.fetched).toEqual(['a']);

    server.waiting.get('a')?.resolve(new Blob(['a']));
    await settle();

    expect(server.fetched).toEqual(['a', 'd']);
  });

  it('holds back past a handful of requests', async () => {
    const server = source();
    const loader = new PreviewLoader(server, () => undefined);
    for (let node = 0; node < 20; node++) {
      loader.want(preview(node, `hash-${String(node)}`));
    }

    expect(server.fetched).toHaveLength(6);

    server.waiting.get('hash-0')?.resolve(new Blob(['x']));
    await settle();

    expect(server.fetched).toHaveLength(7);
  });

  it('shares a picture between nodes, and revokes it with its last use', async () => {
    const server = source();
    const loader = new PreviewLoader(server, () => undefined);
    loader.want(preview(1, 'a'));
    loader.want(preview(2, 'a'));
    server.waiting.get('a')?.resolve(new Blob(['a']));
    await settle();
    expect(server.fetched).toEqual(['a']);
    expect(loader.shown(1)()?.url).toBe(loader.shown(2)()?.url);

    loader.forget(1);
    expect(server.revoked).toEqual([]);
    loader.forget(2);
    expect(server.revoked).toHaveLength(1);
  });

  it('does not fetch what is on screen', async () => {
    const server = source();
    const loader = new PreviewLoader(server, () => undefined);
    loader.want(preview(1, 'a'));
    server.waiting.get('a')?.resolve(new Blob(['a']));
    await settle();

    loader.want(preview(1, 'a'));

    expect(server.fetched).toEqual(['a']);
  });

  it('reports a lost file and fetches it again after the next evaluation', async () => {
    const server = source();
    let lost = 0;
    const loader = new PreviewLoader(server, () => {
      lost++;
    });
    loader.want(preview(1, 'a'));
    server.waiting.get('a')?.reject(new Error('404'));
    await settle();
    expect(lost).toBe(1);

    loader.retryLost();
    server.waiting.get('a')?.resolve(new Blob(['a']));
    await settle();

    expect(server.fetched).toEqual(['a', 'a']);
    expect(loader.shown(1)()?.hash).toBe('a');
  });
});
