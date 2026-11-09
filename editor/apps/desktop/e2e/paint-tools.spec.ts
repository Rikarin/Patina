import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { crc32, deflateSync } from 'node:zlib';
import { answerOpen, leave, newLibrary, newProject } from './projects';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
const bench = resolve(__dirname, '../../../../engine/tools/Patina.Bench/bin/Release/net10.0/patina-bench.dll');
const shots = resolve(__dirname, '../../../dist/e2e-results');

/** Linux CI runs under Xvfb with no GPU, and Chromium no longer falls back to software WebGL unless told to. */
const softwareWebGl = process.platform === 'linux' ? ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] : [];

let scratch: string;
let app: ElectronApplication;
let window: Page;

test.beforeEach(async () => {
  test.skip(process.platform === 'win32' && !!process.env['CI'], 'Windows CI has no Vulkan.');
  // A project, a mesh, a stack evaluated and several strokes each wait on the Worker, which CI's runner is slow at.
  test.setTimeout(240_000);

  scratch = mkdtempSync(join(tmpdir(), 'patina-e2e-'));
  execFileSync('dotnet', [bench, 'bake-pair', join(scratch, 'meshes'), '0.05']);
  app = await electron.launch({
    args: [...softwareWebGl, main],
    env: { ...process.env, PATINA_USER_DATA: join(scratch, 'user-data') }
  });
  window = await app.firstWindow();
  await expect(window.getByRole('status', { name: 'Server status' })).toContainText('GPU ready');
});

test.afterEach(async () => {
  await leave(app);
  rmSync(scratch, { recursive: true, force: true, maxRetries: 20, retryDelay: 250 });
});

const canvas = () => window.getByRole('application', { name: 'Viewport' });

async function box() {
  const found = await canvas().boundingBox();
  if (!found) {
    throw new Error('The viewport canvas has no box.');
  }

  return found;
}

async function shot(): Promise<string> {
  return (await canvas().screenshot({ scale: 'css' })).toString('base64');
}

/** How many pixels moved by more than `step` levels of red. */
async function changed(before: string, after: string, step = 24): Promise<number> {
  return window.evaluate(
    async ({ first, second, step }) => {
      const load = async (data: string) => {
        const bytes = Uint8Array.from(atob(data), character => character.charCodeAt(0));
        const bitmap = await createImageBitmap(new Blob([bytes], { type: 'image/png' }));
        const surface = new OffscreenCanvas(bitmap.width, bitmap.height);
        const context = surface.getContext('2d');
        if (!context) {
          throw new Error('No 2D context.');
        }

        context.drawImage(bitmap, 0, 0);
        return context.getImageData(0, 0, bitmap.width, bitmap.height).data;
      };
      const [one, two] = [await load(first), await load(second)];
      let count = 0;
      for (let at = 0; at < one.length; at += 4) {
        count += Math.abs((one[at] ?? 0) - (two[at] ?? 0)) > step ? 1 : 0;
      }

      return count;
    },
    { first: before, second: after, step }
  );
}

async function openOnMesh(): Promise<void> {
  await newLibrary(app, window, join(scratch, 'Game.ptlib'));
  await newProject(app, window, 'Housing', join(scratch, 'meshes', 'housing_low.glb'));
  await expect(window.getByTestId('paint-picture')).toBeVisible({ timeout: 60_000 });
  await expect(window.getByTestId('viewport-mesh')).toHaveText('housing_low.glb', { timeout: 30_000 });
  await settled();
}

/** Waits for the stack to finish evaluating and the picture to stop changing. */
async function settled(): Promise<void> {
  await expect(window.getByTestId('layers-evaluation')).not.toContainText('Evaluating', { timeout: 60_000 });
  await expect
    .poll(async () => Number(await canvas().getAttribute('data-frames')), { timeout: 30_000 })
    .toBeGreaterThan(0);
  await expect
    .poll(
      async () => {
        const earlier = await shot();
        await window.waitForTimeout(700);
        return changed(earlier, await shot());
      },
      { timeout: 30_000 }
    )
    .toBeLessThan(40);
}

async function at(x: number, y: number): Promise<{ x: number; y: number }> {
  const area = await box();
  return { x: area.x + area.width * x, y: area.y + area.height * y };
}

async function drag(
  fromX: number,
  fromY: number,
  toX: number,
  toY: number,
  modifier?: 'Meta' | 'Control'
): Promise<void> {
  const from = await at(fromX, fromY);
  const to = await at(toX, toY);
  await window.mouse.move(from.x, from.y);
  if (modifier) {
    await window.keyboard.down(modifier);
  }

  await window.mouse.down();
  await window.mouse.move(to.x, to.y, { steps: 24 });
  await window.mouse.up();
  if (modifier) {
    await window.keyboard.up(modifier);
  }
}

async function click(x: number, y: number, modifier?: 'Meta' | 'Control'): Promise<void> {
  const place = await at(x, y);
  if (modifier) {
    await window.keyboard.down(modifier);
  }

  await window.mouse.click(place.x, place.y);
  if (modifier) {
    await window.keyboard.up(modifier);
  }
}

async function colour(hex: string): Promise<void> {
  await window.getByTestId('paint-colour').locator('button').first().click();
  await window.getByRole('textbox', { name: 'Hex color' }).fill(hex);
  await window.keyboard.press('Enter');
  await window.keyboard.press('Escape');
}

/** A PNG of four squares, green and magenta, to project and to mask with. */
function checker(path: string, side = 64): void {
  const chunk = (type: string, data: Buffer) => {
    const length = Buffer.alloc(4);
    length.writeUInt32BE(data.length);
    const named = Buffer.concat([Buffer.from(type, 'ascii'), data]);
    const sum = Buffer.alloc(4);
    sum.writeUInt32BE(crc32(named));
    return Buffer.concat([length, named, sum]);
  };
  const header = Buffer.alloc(13);
  header.writeUInt32BE(side, 0);
  header.writeUInt32BE(side, 4);
  header.set([8, 6, 0, 0, 0], 8);
  const rows: Buffer[] = [];
  for (let y = 0; y < side; y++) {
    const row = Buffer.alloc(1 + side * 4);
    for (let x = 0; x < side; x++) {
      const green = x < side / 2 === y < side / 2;
      row.set(green ? [30, 220, 60, 255] : [230, 30, 200, 255], 1 + x * 4);
    }

    rows.push(row);
  }

  const signature = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
  writeFileSync(
    path,
    Buffer.concat([
      signature,
      chunk('IHDR', header),
      chunk('IDAT', deflateSync(Buffer.concat(rows))),
      chunk('IEND', Buffer.alloc(0))
    ])
  );
}

test('the tool strip follows the tool, stays when a fill is chosen, and a polygon fill fills the flat clicked', async () => {
  await openOnMesh();
  const strip = window.getByTestId('tool-strip');
  await expect(strip).toHaveAttribute('data-tool', 'brush');
  await window.getByTestId('paint-tool-smudge').click();
  await expect(strip).toHaveAttribute('data-tool', 'smudge');
  await expect(window.getByTestId('tool-strip-smudge')).toHaveAttribute('aria-pressed', 'true');

  await window.getByTestId('tool-strip-fill').click();
  await colour('#1060e0');
  const before = await shot();
  await click(0.5, 0.5);
  await expect.poll(async () => changed(before, await shot()), { timeout: 60_000 }).toBeGreaterThan(1500);
  await settled();
  await window.screenshot({ path: join(shots, 'tool-strip-polygon-fill.png') });
  const polygon = await shot();

  await window.getByTestId('paint-fill-mode').selectOption('part');
  await click(0.5, 0.5);
  await expect.poll(async () => changed(polygon, await shot()), { timeout: 60_000 }).toBeGreaterThan(1500);
  await settled();
  await window.screenshot({ path: join(shots, 'tool-strip-part-fill.png') });

  // A fill layer chosen used to hide the tools, which lived only in Properties.
  await window.getByTestId('paint-add-fill').click();
  await expect(strip).toBeVisible();
  expect(await window.getByTestId('app-failure').allTextContents()).toEqual([]);
});

test('a smudge drags paint along, and a clone copies it from its source', async () => {
  await openOnMesh();
  await colour('#e01810');
  const blank = await shot();
  await drag(0.38, 0.45, 0.62, 0.45);
  await expect.poll(async () => changed(blank, await shot()), { timeout: 60_000 }).toBeGreaterThan(800);
  await settled();
  const painted = await shot();

  await window.getByTestId('tool-strip-smudge').click();
  await drag(0.5, 0.45, 0.5, 0.7);
  await expect.poll(async () => changed(painted, await shot()), { timeout: 60_000 }).toBeGreaterThan(150);
  await settled();
  await window.screenshot({ path: join(shots, 'tool-smudge.png') });
  const smudged = await shot();

  await window.getByTestId('tool-strip-clone').click();
  await click(0.45, 0.45, process.platform === 'darwin' ? 'Meta' : 'Control');
  await expect(window.getByTestId('paint-clone-source')).not.toHaveText('None yet');
  await drag(0.42, 0.6, 0.6, 0.6);
  await expect.poll(async () => changed(smudged, await shot()), { timeout: 60_000 }).toBeGreaterThan(400);
  await settled();
  expect(await window.getByTestId('app-failure').allTextContents()).toEqual([]);
  await window.screenshot({ path: join(shots, 'tool-clone.png') });
});

test('a picture on the viewport projects through the brush, and as a stencil masks it', async () => {
  await openOnMesh();
  const picture = join(scratch, 'checker.png');
  checker(picture);
  await answerOpen(app, picture);
  await window.getByTestId('paint-screen-picture-choose').click();
  await expect(window.getByTestId('screen-picture-frame')).toBeVisible({ timeout: 30_000 });
  await expect(window.getByTestId('tool-strip')).toHaveAttribute('data-tool', 'projection');
  await settled();
  const before = await shot();
  await window.screenshot({ path: join(shots, 'tool-projection-placed.png') });

  // Moved off the middle by its handle, then painted through.
  const handle = await window.getByTestId('screen-picture-move').boundingBox();
  if (!handle) {
    throw new Error('The picture has no handle.');
  }

  await window.mouse.move(handle.x + handle.width / 2, handle.y + handle.height / 2);
  await window.mouse.down();
  await window.mouse.move(handle.x + handle.width / 2 + 20, handle.y + handle.height / 2, { steps: 4 });
  await window.mouse.up();
  await expect(window.getByTestId('screen-picture-frame')).not.toHaveAttribute('data-centre', /^0,0$/u);

  await drag(0.4, 0.5, 0.6, 0.5);
  await expect.poll(async () => changed(before, await shot()), { timeout: 60_000 }).toBeGreaterThan(800);
  await settled();
  expect(await window.getByTestId('app-failure').allTextContents()).toEqual([]);
  await window.screenshot({ path: join(shots, 'tool-projection-painted.png') });

  await window.getByTestId('paint-stencil').check();
  await window.getByTestId('tool-strip-brush').click();
  await colour('#1060e0');
  const projected = await shot();
  await drag(0.4, 0.62, 0.6, 0.62);
  await expect.poll(async () => changed(projected, await shot()), { timeout: 60_000 }).toBeGreaterThan(100);
  await settled();
  await window.screenshot({ path: join(shots, 'tool-stencil.png') });
  await window.getByTestId('paint-stencil').uncheck();
  await expect(window.getByTestId('screen-picture-frame')).toHaveCount(0);
  await window.screenshot({ path: join(shots, 'tool-stencil-result.png') });
});

test('a dab on the strap paints the top under it only with the occlusion test off', async () => {
  await openOnMesh();
  await canvas().focus();
  await window.keyboard.press('Numpad7');
  await settled();
  // Small enough that the dab stays on the strap, which is a sixth of the model across.
  for (let step = 0; step < 2; step++) {
    await window.keyboard.press('BracketLeft');
  }

  await colour('#e01810');
  const atlas = window.getByTestId('paint-canvas');
  await window.getByTestId('paint-occlusion').uncheck();
  await click(0.5, 0.5);
  await settled();
  await window.screenshot({ path: join(shots, 'occlusion-off-window.png') });
  await atlas.screenshot({ path: join(shots, 'occlusion-off-atlas.png') });
  const through = await atlas.screenshot();

  await window.getByTestId('paint-undo').click();
  await settled();
  await window.getByTestId('paint-occlusion').check();
  await click(0.5, 0.5);
  await settled();
  await window.screenshot({ path: join(shots, 'occlusion-on-window.png') });
  await atlas.screenshot({ path: join(shots, 'occlusion-on-atlas.png') });
  const seen = await atlas.screenshot();

  // The dab off the test reaches the top under the strap as well, so the atlas holds more of it.
  expect(through.equals(seen)).toBe(false);
});
