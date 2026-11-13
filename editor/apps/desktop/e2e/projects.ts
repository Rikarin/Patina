import { expect, type ElectronApplication, type Page } from '@playwright/test';
import { existsSync, readdirSync } from 'node:fs';
import { basename, join } from 'node:path';

/** Answers the system's next save dialog, which a test cannot click. */
export const answerSave = (app: ElectronApplication, path: string) =>
  app.evaluate(({ dialog }, filePath) => {
    Object.assign(dialog, { showSaveDialog: () => Promise.resolve({ canceled: false, filePath }) });
  }, path);

/** Answers the system's next open dialog, and notes when it has. */
export const answerOpen = (app: ElectronApplication, path: string) =>
  app.evaluate(({ dialog }, filePath) => {
    Object.assign(globalThis, { patinaOpened: false });
    Object.assign(dialog, {
      showOpenDialog: () => {
        Object.assign(globalThis, { patinaOpened: true });
        return Promise.resolve({ canceled: false, filePaths: [filePath] });
      }
    });
  }, path);

/**
 * Clicks a Choose… and takes the file `answerOpen` gave the system's dialog. A field that offers the library's files
 * opens Choose a Mesh or Choose a Picture first when the library has some, and From a File… is then the way to the
 * system's dialog; with none, the system's dialog opens at once.
 */
export async function chooseFromComputer(app: ElectronApplication, window: Page, testId: string): Promise<void> {
  await window.getByTestId(testId).click();
  const machine = window.getByTestId('pick-file-machine');
  const opened = () => app.evaluate(() => (globalThis as { patinaOpened?: boolean }).patinaOpened === true);
  await expect.poll(async () => (await machine.isVisible()) || (await opened()), { timeout: 30_000 }).toBe(true);
  if (await machine.isVisible()) {
    await machine.click();
  }

  await expect.poll(opened).toBe(true);
}

/** Clicks a Choose… and picks a file the library holds, by its name, in Choose a Mesh or Choose a Picture. */
export async function chooseInLibrary(window: Page, testId: string, name: string): Promise<void> {
  await window.getByTestId(testId).click();
  const dialog = window.getByTestId('pick-file');
  await dialog.getByTestId('pick-file-item').filter({ hasText: name }).first().click();
  await dialog.getByTestId('pick-file-choose').click();
  await expect(dialog).toHaveCount(0);
}

/**
 * Chooses in one of xUI's selects the way a person does: opens it, then clicks the choice by its name. A name is
 * matched whole, or from its start when it is a pattern.
 */
export async function pick(window: Page, testId: string, name: string | RegExp): Promise<void> {
  await window.getByTestId(testId).click();
  await window
    .getByRole('option', typeof name === 'string' ? { name, exact: true } : { name })
    .locator('visible=true')
    .first()
    .click();
}

/** What New Project calls each kind a project can start as. */
export const ProjectKinds = {
  texturing: /^Texturing/u,
  'layered-material': /^Layered Material/u,
  'image-material': /^Image Material/u,
  graph: /^Graph/u
} as const;

/** Makes a library the way a person does. */
export async function newLibrary(app: ElectronApplication, window: Page, path: string): Promise<void> {
  await answerSave(app, path);
  await window.getByTestId('library-new').click();
  await expect(window.getByRole('status', { name: 'Engine status' })).toContainText(
    basename(path).replace(/\.ptlib$/u, ''), // a Windows path is split at its backslashes too
    {
      timeout: 30_000
    }
  );
  await expect(window.getByRole('status', { name: 'Engine status' })).toContainText('GPU ready');
}

/** Makes a project the way a person does, which also opens it. */
export async function newProject(
  app: ElectronApplication,
  window: Page,
  name: string,
  mesh?: string,
  kind: keyof typeof ProjectKinds = mesh ? 'texturing' : 'layered-material'
): Promise<void> {
  await window.getByTestId('library-new-project').click();
  await window.getByTestId('new-project-name').fill(name);
  await pick(window, 'new-project-kind', ProjectKinds[kind]);
  if (mesh) {
    await answerOpen(app, mesh);
    await chooseFromComputer(app, window, 'new-project-mesh');
    await expect(window.getByTestId('new-project-mesh-name')).toHaveText(basename(mesh));
  }

  await window.getByRole('button', { name: 'Create' }).click();
  await expect(window.getByTestId('document-tab-name').filter({ hasText: name })).toBeVisible({ timeout: 60_000 });
}

/**
 * How many whole autosaves a library holds. Each is a folder of its own, and it counts once its document is written,
 * which comes after its paint: a folder alone can be one still being written, which a crash leaves with nothing to
 * offer and the next start clears away.
 */
export function wholeAutosaves(library: string): number {
  const folder = join(library, '.ptcache', 'autosave');
  return existsSync(folder)
    ? readdirSync(folder, { recursive: true, encoding: 'utf8' }).filter(file => basename(file) === 'document').length
    : 0;
}

/** The autosave begun last in a library, whole or still being written, by its folder's name: when it began. */
export function newestGeneration(library: string): string {
  const folder = join(library, '.ptcache', 'autosave');
  return existsSync(folder)
    ? (readdirSync(folder, { recursive: true, encoding: 'utf8' })
        .filter(path => /^\d{19}$/.test(basename(path)))
        .map(path => basename(path))
        .sort()
        .at(-1) ?? '')
    : '';
}

/**
 * The app's end, from a quit about to begin. Past the wait it fails with every step the quit took, as the app wrote
 * them, so a quit that stalls says where it stopped (#236).
 */
export function ending(app: ElectronApplication, waitMs = 30_000): Promise<void> {
  const steps: string[] = [];
  const heard = (chunk: Buffer | string) => {
    steps.push(
      ...String(chunk)
        .split(/\r?\n/u)
        .filter(line => line.includes('Patina quit:'))
    );
  };
  app.process().stdout?.on('data', heard);
  return new Promise<void>((resolve, reject) => {
    const timer = setTimeout(() => {
      const went = steps.join('\n') || '(no step)';
      reject(new Error(`The app did not end within ${waitMs} ms. Its quit went:\n${went}`));
    }, waitMs);
    void app.waitForEvent('close', { timeout: 0 }).then(() => {
      clearTimeout(timer);
      resolve();
    });
  });
}

/** Ends the app with no one to answer its question about unsaved paint. */
export async function leave(app: ElectronApplication): Promise<void> {
  // The engine's processes outlive Electron's exit for a moment, and on Windows a file they still hold cannot be
  // deleted, so the scratch folder would fail to go. Wait for every process to end before the test cleans up.
  const pids = [electronPid(app), ...(await sidecarPids(app))].filter((pid): pid is number => pid !== undefined);
  await app
    .evaluate(({ app: electron }) => {
      electron.exit(0);
    })
    .catch(() => undefined); // the app can be gone before the answer is
  const deadline = Date.now() + 10_000;
  while (pids.some(running) && Date.now() < deadline) {
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  // ⚠ Killed outright: a plain kill is SIGTERM on Linux, which .NET answers with an orderly shutdown, and a Worker deep
  // in an evaluation on the software GPU can draw that out past the runner's teardown. Windows ends it either way.
  for (const pid of pids.filter(running)) {
    process.kill(pid, 'SIGKILL');
  }
}

function electronPid(app: ElectronApplication): number | undefined {
  try {
    return app.process().pid;
  } catch {
    return undefined; // the app has already quit
  }
}

async function sidecarPids(app: ElectronApplication): Promise<number[]> {
  const window = app.windows()[0];
  if (window === undefined) {
    return [];
  }
  const statuses = await window
    .evaluate(() =>
      (globalThis as unknown as { patina: { sidecars(): Promise<{ pid: number | null }[]> } }).patina.sidecars()
    )
    .catch(() => []);
  return statuses.flatMap(status => (status.pid === null ? [] : [status.pid]));
}

function running(pid: number): boolean {
  try {
    process.kill(pid, 0);
    return true;
  } catch {
    return false;
  }
}

/**
 * A mesh as Blender exports one with colour attributes: two quads side by side, red and blue, in `COLOR_0` as
 * normalised 16-bit, each with its own half of the UV square. Written here so that nothing of ours has read it first.
 */
export function colouredGlb(height = 1, layoutTop = 0): Buffer {
  const corners = (x: number, u: number, colour: [number, number, number]) =>
    [
      [x, -1, 0, 0, 0, 1, u, 1],
      [x + 1, -1, 0, 0, 0, 1, u + 0.5, 1],
      [x + 1, height, 0, 0, 0, 1, u + 0.5, layoutTop],
      [x, height, 0, 0, 0, 1, u, layoutTop]
    ].map(vertex => ({ vertex, colour }));
  const vertices = [...corners(-1, 0, [1, 0, 0]), ...corners(0, 0.5, [0, 0, 1])];
  const floats = Buffer.alloc(vertices.length * 32);
  vertices.forEach(({ vertex }, row) => {
    vertex.forEach((value, lane) => floats.writeFloatLE(value, (row * 8 + lane) * 4));
  });
  const colours = Buffer.alloc(vertices.length * 8);
  vertices.forEach(({ colour }, row) => {
    [...colour, 1].forEach((value, lane) => colours.writeUInt16LE(value * 65535, row * 8 + lane * 2));
  });
  const indices = Buffer.alloc(24);
  [0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7].forEach((index, at) => indices.writeUInt16LE(index, at * 2));
  const binary = Buffer.concat([floats, colours, indices]);
  const json = JSON.stringify({
    asset: { version: '2.0' },
    scene: 0,
    scenes: [{ nodes: [0] }],
    nodes: [{ mesh: 0 }],
    meshes: [{ primitives: [{ attributes: { POSITION: 0, NORMAL: 1, TEXCOORD_0: 2, COLOR_0: 3 }, indices: 4 }] }],
    accessors: [
      {
        bufferView: 0,
        componentType: 5126,
        count: 8,
        type: 'VEC3',
        byteOffset: 0,
        min: [-1, -1, 0],
        max: [1, height, 0]
      },
      { bufferView: 0, componentType: 5126, count: 8, type: 'VEC3', byteOffset: 12 },
      { bufferView: 0, componentType: 5126, count: 8, type: 'VEC2', byteOffset: 24 },
      { bufferView: 1, componentType: 5123, normalized: true, count: 8, type: 'VEC4' },
      { bufferView: 2, componentType: 5123, count: 12, type: 'SCALAR' }
    ],
    bufferViews: [
      { buffer: 0, byteLength: floats.length, byteStride: 32 },
      { buffer: 0, byteOffset: floats.length, byteLength: colours.length },
      { buffer: 0, byteOffset: floats.length + colours.length, byteLength: indices.length }
    ],
    buffers: [{ byteLength: binary.length }]
  });
  const text = Buffer.from(json.padEnd(json.length + ((4 - (json.length % 4)) % 4), ' '));
  const padded = Buffer.concat([binary, Buffer.alloc((4 - (binary.length % 4)) % 4)]);
  const head = Buffer.alloc(20);
  head.writeUInt32LE(0x46546c67, 0);
  head.writeUInt32LE(2, 4);
  head.writeUInt32LE(20 + text.length + 8 + padded.length, 8);
  head.writeUInt32LE(text.length, 12);
  head.writeUInt32LE(0x4e4f534a, 16);
  const tail = Buffer.alloc(8);
  tail.writeUInt32LE(padded.length, 0);
  tail.writeUInt32LE(0x004e4942, 4);
  return Buffer.concat([head, text, tail, padded]);
}
