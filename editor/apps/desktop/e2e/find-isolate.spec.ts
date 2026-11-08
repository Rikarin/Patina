import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { leave, newLibrary, newProject } from './projects';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
const shots = resolve(__dirname, '../../../dist/e2e-results');
const softwareWebGl = process.platform === 'linux' ? ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] : [];

let scratch: string;
let app: ElectronApplication;
let window: Page;

test.beforeEach(async () => {
  test.skip(process.platform === 'win32' && !!process.env['CI'], 'Windows CI has no Vulkan.');
  scratch = mkdtempSync(join(tmpdir(), 'patina-e2e-find-'));
  app = await electron.launch({
    args: [...softwareWebGl, main],
    env: { ...process.env, PATINA_USER_DATA: join(scratch, 'user-data'), PATINA_LIBRARY: '' }
  });
  window = await app.firstWindow();
  await newLibrary(app, window, join(scratch, 'Game.ptlib'));
});

test.afterEach(async () => {
  await leave(app);
  rmSync(scratch, { recursive: true, force: true, maxRetries: 20, retryDelay: 250 });
});

/** Clicks the 2D view at a place, as fractions of it. */
async function clickMap(u: number, v: number): Promise<void> {
  const box = await window.getByTestId('paint-picture').boundingBox();
  if (!box) {
    throw new Error('The picture has no box.');
  }

  await window.mouse.click(box.x + box.width * u, box.y + box.height * v);
}

test('Find Layer chooses the layer on top where the map is clicked', async () => {
  test.setTimeout(120_000);
  await newProject(app, window, 'Crate');
  const picture = window.getByTestId('paint-picture');
  await expect(picture).toBeVisible({ timeout: 60_000 });
  const before = await picture.getAttribute('src');
  const box = await picture.boundingBox();
  if (!box) {
    throw new Error('The picture has no box.');
  }

  await window.mouse.move(box.x + box.width * 0.2, box.y + box.height * 0.5);
  await window.mouse.down();
  await window.mouse.move(box.x + box.width * 0.8, box.y + box.height * 0.5, { steps: 12 });
  await window.mouse.up();
  await expect(picture).not.toHaveAttribute('src', before ?? '', { timeout: 30_000 });
  await expect(window.getByTestId('layers-evaluation')).not.toContainText('Evaluating', { timeout: 30_000 });

  const base = window.getByTestId('paint-layer-base');
  const paint = window.getByTestId('paint-layer-paint');
  await base.click();
  await expect(base).toHaveAttribute('aria-selected', 'true');

  await window.getByTestId('layers-find').click();
  await expect(window.getByTestId('layers-find')).toHaveAttribute('aria-pressed', 'true');
  await clickMap(0.5, 0.5);
  await expect(paint).toHaveAttribute('aria-selected', 'true', { timeout: 30_000 });
  await expect(window.getByTestId('layers-find')).toHaveAttribute('aria-pressed', 'false');
  // A click that found something made no stroke.
  await expect(window.getByTestId('paint-undo')).toHaveAttribute('aria-label', 'Undo Stroke');

  await window.getByTestId('layers-find').click();
  await clickMap(0.5, 0.9);
  await expect(base).toHaveAttribute('aria-selected', 'true', { timeout: 30_000 });
});

test('a texture set shows alone in the viewport, and the whole model comes back', async () => {
  test.setTimeout(120_000);
  const corners = [
    ...['0 0 0', '1 0 0', '1 1 0', '0 1 0', '1.2 0 0', '2.2 0 0', '2.2 1 0', '1.2 1 0'].map(v => `v ${v}`),
    ...['0 0', '1 0', '1 1', '0 1'].map(t => `vt ${t}`),
    'usemtl metal',
    'f 1/1 2/2 3/3 4/4',
    'usemtl wood',
    'f 5/1 6/2 7/3 8/4'
  ];
  writeFileSync(join(scratch, 'sets.obj'), corners.join('\n'));
  await newProject(app, window, 'Sets', join(scratch, 'sets.obj'));
  await expect(window.getByTestId('texture-sets')).toBeVisible({ timeout: 60_000 });
  const viewport = window.getByRole('application', { name: 'Viewport' });
  await expect(viewport).toBeVisible();
  await expect.poll(async () => Number(await viewport.getAttribute('data-frames')), { timeout: 30_000 }).toBeGreaterThan(0);

  /** How many pixels of the composited canvas are a quad's pale grey, which the sky never is. */
  const quads = async (): Promise<number> => {
    const box = await viewport.boundingBox();
    if (!box) {
      throw new Error('The viewport has no box.');
    }

    const png = (await window.screenshot({ clip: box })).toString('base64');
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
      let pale = 0;
      for (let i = 0; i < pixels.length; i += 4) {
        pale += (pixels[i] ?? 0) > 140 && (pixels[i + 1] ?? 0) > 140 && (pixels[i + 2] ?? 0) > 140 ? 1 : 0;
      }

      return pale;
    }, png);
  };

  await expect.poll(quads, { timeout: 30_000 }).toBeGreaterThan(1_000);
  const whole = await quads();
  await viewport.screenshot({ path: join(shots, 'isolate-1-whole.png') });

  // One of two equal quads alone is about half of what both were.
  const isolate = window.getByTestId('texture-set-isolate-wood');
  await isolate.click();
  await expect(isolate).toHaveAttribute('aria-pressed', 'true');
  await expect.poll(quads, { timeout: 30_000 }).toBeLessThan(whole * 0.7);
  expect(await quads()).toBeGreaterThan(whole * 0.3);
  await viewport.screenshot({ path: join(shots, 'isolate-2-wood-alone.png') });

  await isolate.click();
  await expect(isolate).toHaveAttribute('aria-pressed', 'false');
  await expect.poll(quads, { timeout: 30_000 }).toBeGreaterThan(whole * 0.9);
  await viewport.screenshot({ path: join(shots, 'isolate-3-whole-again.png') });
});
