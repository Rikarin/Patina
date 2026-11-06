import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { leave, newLibrary, newProject } from './projects';

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

  scratch = mkdtempSync(join(tmpdir(), 'patina-e2e-'));
  execFileSync('dotnet', [bench, 'bake-pair', join(scratch, 'meshes'), '0.05']);
  app = await electron.launch({
    args: [...softwareWebGl, main],
    env: { ...process.env, PATINA_USER_DATA: join(scratch, 'user-data') }
  });
  window = await app.firstWindow();
  await expect(window.getByRole('status', { name: 'Server status' })).toContainText('Worker connected');
});

test.afterEach(async () => {
  await leave(app);
  // The Server is still on its way out, and writes its last files as it goes.
  rmSync(scratch, { recursive: true, force: true, maxRetries: 20, retryDelay: 250 });
});

const canvas = () => window.getByRole('img', { name: 'Viewport' });

async function box() {
  const found = await canvas().boundingBox();
  if (!found) {
    throw new Error('The viewport canvas has no box.');
  }

  return found;
}

/** How many pixels of the canvas differ from a shot taken earlier, read off the window's own screenshot. */
async function shot(): Promise<string> {
  return (await canvas().screenshot()).toString('base64');
}

async function changed(before: string, after: string): Promise<number> {
  return window.evaluate(
    async ({ first, second }) => {
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
        count += Math.abs((one[at] ?? 0) - (two[at] ?? 0)) > 24 ? 1 : 0;
      }

      return count;
    },
    { first: before, second: after }
  );
}

async function openOnMesh(): Promise<void> {
  await newLibrary(app, window, join(scratch, 'Game.ptlib'));
  await newProject(app, window, 'Housing', join(scratch, 'meshes', 'housing_low.glb'));
  await expect(window.getByTestId('paint-picture')).toBeVisible({ timeout: 60_000 });
  await expect(window.getByTestId('viewport-mesh')).toHaveText('housing_low.glb', { timeout: 30_000 });
  await expect(window.getByTestId('viewport-paint')).toBeVisible();
  await window.getByTestId('viewport-paint').click();
  await settled();
}

/** Waits for the picture to stop changing: the mesh opens first, and the stack's maps reach it after that. */
async function settled(): Promise<void> {
  await expect.poll(async () => Number(await canvas().getAttribute('data-frames')), { timeout: 30_000 }).toBeGreaterThan(0);
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

async function drag(fromX: number, fromY: number, toX: number, toY: number): Promise<void> {
  const area = await box();
  await window.mouse.move(area.x + area.width * fromX, area.y + area.height * fromY);
  await window.mouse.down();
  await window.mouse.move(area.x + area.width * toX, area.y + area.height * toY, { steps: 24 });
  await window.mouse.up();
}

test('a brush on the mesh shows on it in the viewport, across its flats, and undo takes it back', async () => {
  await openOnMesh();
  await window.getByTestId('paint-colour').fill('#e01810');
  const before = await shot();

  await drag(0.3, 0.45, 0.72, 0.55);
  await expect.poll(async () => changed(before, await shot()), { timeout: 30_000 }).toBeGreaterThan(1500);
  await expect(window.getByTestId('viewport-stamp')).toBeVisible();
  await window.screenshot({ path: join(shots, 'paint-mesh.png') });

  await window.getByTestId('paint-undo').click();
  await expect.poll(async () => changed(before, await shot()), { timeout: 30_000 }).toBeLessThan(40);
  await window.getByTestId('paint-redo').click();
  await expect.poll(async () => changed(before, await shot()), { timeout: 30_000 }).toBeGreaterThan(1500);
});

test('a line, a mirrored stroke and an alpha paint through the same journal', async () => {
  await openOnMesh();
  const before = await shot();

  await window.getByTestId('paint-tool-line').click();
  await drag(0.35, 0.35, 0.6, 0.4);
  await expect.poll(async () => changed(before, await shot()), { timeout: 30_000 }).toBeGreaterThan(400);
  const lined = await shot();

  await window.getByTestId('paint-tool-brush').click();
  await window.getByRole('combobox', { name: 'Mirror' }).click();
  await window.getByRole('option', { name: 'x' }).click();
  await drag(0.62, 0.55, 0.66, 0.62);
  await expect.poll(async () => changed(lined, await shot()), { timeout: 30_000 }).toBeGreaterThan(200);
  await window.screenshot({ path: join(shots, 'paint-tools.png') });
});

test('layers of every kind compose on the mesh', async () => {
  await openOnMesh();
  const before = await shot();

  await window.getByTestId('paint-add-fill').click();
  await expect(window.getByTestId('paint-layer-name')).toHaveValue('Fill');
  await window.getByTestId('paint-fill-baseColor').fill('#2050e0');
  await window.getByTestId('paint-fill-baseColor').dispatchEvent('change');
  await expect.poll(async () => changed(before, await shot()), { timeout: 30_000 }).toBeGreaterThan(2000);

  const blend = window.getByRole('combobox', { name: 'Blend mode' });
  await blend.click();
  await window.getByRole('option', { name: 'Multiply', exact: true }).click();
  await expect(window.getByTestId('paint-layers')).toContainText('Multiply');

  await window.getByTestId('paint-add-filter').click();
  await expect(window.getByTestId('paint-layers')).toContainText('Filter');
  await window.getByTestId('paint-add-group').click();
  await window.getByTestId('paint-add').click();
  await expect(window.getByTestId('paint-layers').getByText('Paint')).not.toHaveCount(0);
  await window.screenshot({ path: join(shots, 'paint-layers.png') });
  await expect(window.getByTestId('paint-failure')).toHaveCount(0);
});
