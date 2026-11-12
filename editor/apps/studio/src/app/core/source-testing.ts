/** What {@link sources} needs of Node's file system. */
interface Fs {
  readdirSync(path: string, options: { recursive: true }): string[];
  readFileSync(path: string, encoding: 'utf8'): string;
}

/** Every source file of the studio and its UI library, but tests, for checks that read what the code says. */
export async function sources(): Promise<{ path: string; text: string }[]> {
  // Read at run time, so that the bundle the tests run from does not try to hold the file system.
  const module = 'node:fs';
  const fs = (await import(/* @vite-ignore */ module)) as Fs;
  const roots = ['apps/studio/src/app', 'libs/studio-ui/src'];
  return roots.flatMap(root =>
    fs
      .readdirSync(root, { recursive: true })
      .filter(path => path.endsWith('.ts') && !path.endsWith('.spec.ts') && !path.endsWith('-testing.ts'))
      .map(path => ({ path: `${root}/${path}`, text: fs.readFileSync(`${root}/${path}`, 'utf8') }))
  );
}

/** Each element a template opens with a tag, with its attributes, and the line it starts on. */
export function elementsOf(text: string, tag: RegExp): { tag: string; attributes: string; line: number }[] {
  const elements = new RegExp(`<(${tag.source})\\b((?:[^>"]|"[^"]*")*)>`, 'gu');
  return [...text.matchAll(elements)].map(match => ({
    tag: match[1] ?? '',
    attributes: match[2] ?? '',
    line: text.slice(0, match.index).split('\n').length
  }));
}
