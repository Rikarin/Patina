import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { existsSync, mkdtempSync, readFileSync, readdirSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { answerSave, leave, newLibrary, newProject, pick } from './projects';

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

const canvas = () => window.getByRole('application', { name: 'Viewport' });

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
  await expect(window.getByRole('status', { name: 'Engine status' })).toContainText('GPU ready');
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

test('the viewport turns, pans and zooms from the keyboard', async () => {
  await openMesh();
  await expect.poll(frames, { timeout: 30_000 }).toBeGreaterThan(0);
  await canvas().focus();
  await expect(canvas()).toBeFocused();

  for (const key of ['ArrowRight', 'Shift+ArrowUp', '+', '-', 'f']) {
    const before = await frames();
    await window.keyboard.press(key);
    await expect.poll(frames, { message: key }).toBeGreaterThan(before);
  }
});

/** The project's file, which keeps the viewport as it was left. */
function projectFile(): string {
  const projects = join(scratch, 'Game.ptlib', 'projects');
  const found = readdirSync(projects, { recursive: true, encoding: 'utf8' }).find(path =>
    path.endsWith('project.yaml')
  );
  if (!found) {
    throw new Error('The library has no project.');
  }

  return join(projects, found);
}

test('the display settings change the picture, and the project keeps them', async () => {
  await openMesh();
  await expect.poll(frames, { timeout: 30_000 }).toBeGreaterThan(0);
  const lit = await colours();

  // Clay is the shape in grey: the gold goes.
  await pick(window, 'viewport-view', 'Clay');
  await expect.poll(async () => (await colours()).gold).toBeLessThan(lit.gold / 10);
  await window.screenshot({ path: join(shots, 'viewport-clay.png'), scale: 'css' });
  await pick(window, 'viewport-view', 'Lit');
  await expect.poll(async () => (await colours()).gold).toBeGreaterThan(lit.gold / 2);

  await window.getByRole('button', { name: 'Maximize Preview' }).click();
  await window.getByTestId('viewport-display-toggle').click();
  await expect(window.getByTestId('viewport-display')).toBeVisible();
  const before = await frames();
  await pick(window, 'viewport-tonemap', 'AgX');
  await window.getByTestId('viewport-projection').getByText('Orthographic').click();
  await window.getByTestId('viewport-wireframe').click();
  await expect.poll(frames).toBeGreaterThan(before);

  // W switches the wireframe from the viewport, as the switch's tooltip says (#229).
  const wireframe = window.getByTestId('viewport-wireframe');
  await expect(wireframe).toBeChecked();
  await expect(wireframe.locator('..')).toHaveAttribute('title', /^Wireframe \(W\)/u);
  await window.getByRole('application', { name: 'Viewport' }).focus();
  await window.keyboard.press('w');
  await expect(wireframe).not.toBeChecked();
  await window.keyboard.press('w');
  await expect(wireframe).toBeChecked();
  await window.screenshot({ path: join(shots, 'viewport-display.png'), scale: 'css' });

  // The front view looks along the model's z axis.
  const snapping = await frames();
  await window.getByTestId('viewport-snap-front').click();
  await expect.poll(frames).toBeGreaterThan(snapping);

  await expect
    .poll(() => readFileSync(projectFile(), 'utf8'), { timeout: 10_000 })
    .toMatch(/toneMapping: agx[\s\S]*wireframe: true[\s\S]*orthographic: true/u);

  // Back later, with a new Server: the project opens its viewport as it was left.
  await app.close();
  app = await electron.launch({
    args: [...softwareWebGl, main],
    env: { ...process.env, PATINA_USER_DATA: join(scratch, 'user-data') }
  });
  window = await app.firstWindow();
  await expect(window.getByRole('status', { name: 'Engine status' })).toContainText('Game', { timeout: 30_000 });
  // The library reopens on what was in front, once it has read the project. The tree is no way in meanwhile: Preview
  // comes back maximized, which takes the Library pane out of the page.
  await expect(window.getByTestId('document-tab-name').filter({ hasText: 'Test' })).toBeVisible({ timeout: 30_000 });

  // The layout comes back too, with Preview still maximized.
  await window.getByTestId('viewport-display-toggle').click();
  await expect(window.getByTestId('viewport-tonemap')).toContainText('AgX', { timeout: 30_000 });
  await expect(window.getByTestId('viewport-wireframe')).toBeChecked();
});

/**
 * The grid draws round the model and comes back with the project (#137), and a screenshot is the picture as it is,
 * written where the save dialog says.
 */
test('the grid draws round the model, and a screenshot writes the picture to a file', async () => {
  await openMesh();
  await expect.poll(frames, { timeout: 30_000 }).toBeGreaterThan(0);
  await window.getByTestId('viewport-display-toggle').click();

  const before = await frames();
  await window.getByTestId('viewport-grid').click();
  await expect(window.getByTestId('viewport-grid')).toBeChecked();
  await expect.poll(frames).toBeGreaterThan(before);
  await expect.poll(() => readFileSync(projectFile(), 'utf8'), { timeout: 10_000 }).toMatch(/grid: true/u);

  const picture = join(scratch, 'shots', 'view.png');
  await answerSave(app, picture);
  await window.getByTestId('viewport-screenshot').click();
  await expect.poll(() => existsSync(picture), { timeout: 30_000 }).toBe(true);
  // A PNG, of the size the canvas is drawn at.
  const bytes = readFileSync(picture);
  expect(bytes.subarray(1, 4).toString('latin1')).toBe('PNG');
  expect(bytes.readUInt32BE(16)).toBeGreaterThan(100);
  await window.screenshot({ path: join(shots, 'viewport-grid.png'), scale: 'css' });
});

test('killing the Worker keeps the view, and frames come back from its successor', async () => {
  // A new Worker starts and opens the mesh again: well past 120 s on CI.
  test.setTimeout(240_000);
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
  await expect(window.getByRole('status', { name: 'Engine status' })).toContainText('GPU ready');
});
