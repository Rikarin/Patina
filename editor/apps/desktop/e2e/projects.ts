import { expect, type ElectronApplication, type Page } from '@playwright/test';

/** Answers the system's next save dialog, which a test cannot click. */
export const answerSave = (app: ElectronApplication, path: string) =>
  app.evaluate(({ dialog }, filePath) => {
    Object.assign(dialog, { showSaveDialog: () => Promise.resolve({ canceled: false, filePath }) });
  }, path);

/** Answers the system's next open dialog. */
export const answerOpen = (app: ElectronApplication, path: string) =>
  app.evaluate(({ dialog }, filePath) => {
    Object.assign(dialog, { showOpenDialog: () => Promise.resolve({ canceled: false, filePaths: [filePath] }) });
  }, path);

/** Makes a library the way a person does. */
export async function newLibrary(app: ElectronApplication, window: Page, path: string): Promise<void> {
  await answerSave(app, path);
  await window.getByTestId('library-new').click();
  await expect(window.getByRole('status', { name: 'Server status' })).toContainText(path.split('/').pop() ?? path, {
    timeout: 30_000
  });
  await expect(window.getByRole('status', { name: 'Server status' })).toContainText('Worker connected');
}

/** Makes a project the way a person does, which also opens it. */
export async function newProject(
  app: ElectronApplication,
  window: Page,
  name: string,
  mesh?: string,
  kind = mesh ? 'texturing' : 'layered-material'
): Promise<void> {
  await window.getByTestId('library-new-project').click();
  await window.getByTestId('new-project-name').fill(name);
  await window.getByTestId('new-project-kind').selectOption(kind);
  if (mesh) {
    await answerOpen(app, mesh);
    await window.getByTestId('new-project-mesh').click();
    await expect(window.getByTestId('new-project-mesh-name')).toHaveText(mesh.split('/').pop() ?? mesh);
  }

  await window.getByRole('button', { name: 'Create' }).click();
  await expect(window.getByTestId('document-tab-name').filter({ hasText: name })).toBeVisible({ timeout: 60_000 });
}

/** Ends the app with no one to answer its question about unsaved paint. */
export async function leave(app: ElectronApplication): Promise<void> {
  await app.evaluate(({ app: electron }) => {
    electron.exit(0);
  }).catch(() => undefined); // the app can be gone before the answer is
}

/**
 * A mesh as Blender exports one with colour attributes: two quads side by side, red and blue, in `COLOR_0` as
 * normalised 16-bit, each with its own half of the UV square. Written here so that nothing of ours has read it first.
 */
export function colouredGlb(): Buffer {
  const corners = (x: number, u: number, colour: [number, number, number]) =>
    [
      [x, -1, 0, 0, 0, 1, u, 1],
      [x + 1, -1, 0, 0, 0, 1, u + 0.5, 1],
      [x + 1, 1, 0, 0, 0, 1, u + 0.5, 0],
      [x, 1, 0, 0, 0, 1, u, 0]
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
      { bufferView: 0, componentType: 5126, count: 8, type: 'VEC3', byteOffset: 0, min: [-1, -1, 0], max: [1, 1, 0] },
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
