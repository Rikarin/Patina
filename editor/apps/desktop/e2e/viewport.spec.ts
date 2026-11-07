import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { leave, newLibrary, newProject } from './projects';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
const mesh = resolve(__dirname, '../../../../content/fixtures/viewport-test.glb');
const shots = resolve(__dirname, '../../../dist/e2e-results');

/** Linux CI runs under Xvfb with no GPU, and Chromium no longer falls back to software WebGL unless told to. */
const softwareWebGl = process.platform === 'linux' ? ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] : [];

let scratch: string;
let app: ElectronApplication;
let window: Page;

test.beforeEach(async () => {
  scratch = mkdtempSync(join(tmpdir(), 'patina-e2e-'));
  app = await electron.launch({
    args: [...softwareWebGl, main],
    env: { ...process.env, PATINA_USER_DATA: join(scratch, 'user-data') }
  });
  window = await app.firstWindow();
  // The native file dialog is the one thing a test cannot click.
  await app.evaluate(({ dialog }, path) => {
    dialog.showOpenDialog = () => Promise.resolve({ canceled: false, filePaths: [path] });
  }, mesh);
});

/** A mesh reaches the viewport through a project. */
async function openMesh(): Promise<void> {
  await newLibrary(app, window, join(scratch, 'Game.ptlib'));
  await newProject(app, window, 'Test', undefined, 'graph');
  await window.getByRole('tab', { name: 'Preview' }).click();
  await window.getByRole('button', { name: 'Open mesh…' }).click();
  await expect(window.getByTestId('viewport-mesh')).toHaveText('viewport-test.glb', { timeout: 30_000 });
}

test.afterEach(async () => {
  await leave(app);
  rmSync(scratch, { recursive: true, force: true, maxRetries: 20, retryDelay: 250 });
});

const canvas = () => window.getByRole('img', { name: 'Viewport' });

async function canvasBox(): Promise<{ x: number; y: number; width: number; height: number }> {
  const box = await canvas().boundingBox();
  if (!box) {
    throw new Error('The viewport canvas has no box.');
  }

  return box;
}

async function frames(): Promise<number> {
  return Number(await canvas().getAttribute('data-frames'));
}

/** Counts what the window's own screenshot shows inside the canvas: this reads composited pixels, not a frame counter. */
async function colours(): Promise<{ gold: number; red: number; total: number }> {
  const png = (await window.screenshot({ clip: await canvasBox() })).toString('base64');
  return window.evaluate(async data => {
    const image = new Image();
    image.src = `data:image/png;base64,${data}`;
    await image.decode();
    const surface = new OffscreenCanvas(image.width, image.height);
    const context = surface.getContext('2d');
    if (!context) {
      throw new Error('No 2D context.');
    }

    context.drawImage(image, 0, 0);
    const pixels = context.getImageData(0, 0, image.width, image.height).data;
    let [gold, red] = [0, 0];
    for (let i = 0; i < pixels.length; i += 4) {
      const [r, g, b] = [pixels[i] ?? 0, pixels[i + 1] ?? 0, pixels[i + 2] ?? 0];
      gold += r > b + 40 && g > 80 ? 1 : 0;
      red += r > 150 && g < 110 ? 1 : 0;
    }

    return { gold, red, total: pixels.length / 4 };
  }, png);
}

test('a glTF opened in the docked viewport shows up lit, and a drag orbits it', async () => {
  await expect(window.getByRole('status', { name: 'Server status' })).toContainText('GPU ready');
  await expect(window.locator('html')).toHaveAttribute('data-skin', 'bezel');

  await openMesh();
  await expect(window.getByTestId('viewport-mesh')).toHaveText('viewport-test.glb');
  await expect.poll(frames).toBeGreaterThan(0);

  await window.screenshot({ path: join(shots, 'viewport.png') });
  const lit = await colours();
  expect(lit.gold).toBeGreaterThan(lit.total / 100);
  expect(lit.red).toBeGreaterThan(20);

  const before = await frames();
  const box = await canvasBox();
  await window.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
  await window.mouse.down();
  for (let step = 1; step <= 10; step++) {
    await window.mouse.move(box.x + box.width / 2 + step * 12, box.y + box.height / 2);
  }

  await window.mouse.up();
  await expect.poll(frames).toBeGreaterThan(before);
  await window.screenshot({ path: join(shots, 'viewport-orbited.png') });

  // A new size is a new texture: a painter that kept the old one's immutable storage would stop showing frames.
  const resized = await frames();
  await app.evaluate(({ BrowserWindow }) => BrowserWindow.getAllWindows()[0]?.setSize(1100, 700));
  await expect.poll(frames).toBeGreaterThan(resized);
  await expect.poll(async () => (await colours()).gold).toBeGreaterThan(100);
  await window.screenshot({ path: join(shots, 'viewport-resized.png') });
});

test('killing the Worker keeps the view, and frames come back from its successor', async () => {
  await openMesh();
  await expect.poll(frames).toBeGreaterThan(0);

  const [worker] = await window.evaluate(() =>
    (globalThis as unknown as { patina: { sidecars(): Promise<{ name: string; pid: number }[]> } }).patina.sidecars()
  );
  expect(worker?.name).toBe('worker');
  const box = await canvasBox();
  const before = await frames();
  const killed = Date.now();
  process.kill(worker?.pid ?? 0, 'SIGKILL');

  await expect
    .poll(
      async () => {
        await window.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
        await window.mouse.down();
        await window.mouse.move(box.x + box.width / 2 + 20, box.y + box.height / 2);
        await window.mouse.up();
        return frames();
      },
      { timeout: 20_000, intervals: [50] }
    )
    .toBeGreaterThan(before);
  console.warn(`S5: a frame from the new Worker ${Date.now() - killed} ms after the kill`);
  await expect(window.getByRole('status', { name: 'Server status' })).toContainText('GPU ready');
});
