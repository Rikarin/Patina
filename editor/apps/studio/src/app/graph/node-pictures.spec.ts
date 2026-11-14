import { NodePictureBatch, type NodePictureHash, NodePictures } from './node-pictures';

/** A Server that names each type's picture after it, has none for Output/Output, and counts what it is asked. */
function server(answers = true) {
  const asked: string[][] = [];
  const fetched: string[] = [];
  const revoked: string[] = [];
  return {
    asked,
    fetched,
    revoked,
    draw: (types: readonly string[]): Promise<readonly NodePictureHash[] | null> => {
      asked.push([...types]);
      return Promise.resolve(
        answers ? types.map(source => ({ source, hash: source === 'Output/Output' ? null : `h-${source}` })) : null
      );
    },
    fetch: (hash: string) => {
      fetched.push(hash);
      return Promise.resolve(new Blob([hash]));
    },
    url: (blob: Blob) => `blob:${String(blob.size)}`,
    revoke: (url: string) => revoked.push(url)
  };
}

const settle = () => new Promise(done => setTimeout(done, 60));

describe('NodePictures', () => {
  it('asks once for what a list wants, in batches, and keeps none for a type with no picture', async () => {
    const source = server();
    const pictures = new NodePictures(source);
    const types = Array.from({ length: NodePictureBatch + 3 }, (_, at) => `Source/T${String(at)}`);

    pictures.want(['Source/Noise', 'Output/Output']);
    pictures.want(['Source/Noise', ...types]);
    expect(pictures.url('Source/Noise')).toBeUndefined();
    await settle();

    expect(source.asked.flat()).toEqual(['Source/Noise', 'Output/Output', ...types]);
    expect(source.asked[0]).toHaveLength(NodePictureBatch);
    expect(pictures.url('Source/Noise')).toBe('blob:14');
    expect(pictures.url('Output/Output')).toBeNull();

    pictures.want(['Source/Noise']);
    await settle();
    expect(source.asked).toHaveLength(2);
  });

  it('asks again after a forget, keeping what is shown meanwhile, and lets the pictures go when done', async () => {
    const source = server();
    const pictures = new NodePictures(source);
    pictures.want(['Source/Noise']);
    await settle();

    pictures.forget();
    expect(pictures.url('Source/Noise')).toBe('blob:14');
    await settle();
    expect(source.asked).toEqual([['Source/Noise'], ['Source/Noise']]);
    // The same hash is the same picture, fetched once.
    expect(source.fetched).toEqual(['h-Source/Noise']);

    pictures.dispose();
    expect(source.revoked).toEqual(['blob:14']);
  });

  it('keeps names alone with no Worker, and asks again when the list next wants them', async () => {
    const source = server(false);
    const pictures = new NodePictures(source);
    pictures.want(['Source/Noise']);
    await settle();
    expect(pictures.url('Source/Noise')).toBeUndefined();
    expect(source.asked).toHaveLength(1);

    pictures.want(['Source/Noise']);
    await settle();
    expect(source.asked).toHaveLength(2);
  });
});
