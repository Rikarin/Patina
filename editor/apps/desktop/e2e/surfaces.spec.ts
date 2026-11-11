import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { leave, newLibrary, newProject } from './projects';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
const bench = resolve(__dirname, '../../../../engine/tools/Patina.Bench/bin/Release/net11.0/patina-bench.dll');
const shots = resolve(__dirname, '../../../dist/e2e-results');

/** Linux CI runs under Xvfb with no GPU, and Chromium no longer falls back to software WebGL unless told to. */
const softwareWebGl = process.platform === 'linux' ? ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] : [];

let scratch: string;
let app: ElectronApplication;
let window: Page;

test.beforeEach(async () => {
  test.skip(process.platform === 'win32' && !!process.env['CI'], 'Windows CI has no Vulkan.');
  // A project, a bake, and an evaluation after every placement each wait on the Worker, which CI's runner is slow at.
  test.setTimeout(420_000);

  scratch = mkdtempSync(join(tmpdir(), 'patina-e2e-surfaces-'));
  execFileSync('dotnet', [bench, 'bake-pair', join(scratch, 'meshes'), '0.05']);
  app = await electron.launch({
    args: [...softwareWebGl, main],
    env: { ...process.env, PATINA_USER_DATA: join(scratch, 'user-data'), PATINA_LIBRARY: '' }
  });
  window = await app.firstWindow();
  await expect(window.getByRole('status', { name: 'Server status' })).toContainText('GPU ready');
});

test.afterEach(async () => {
  await leave(app);
  rmSync(scratch, { recursive: true, force: true, maxRetries: 20, retryDelay: 250 });
});

const canvas = () => window.getByRole('application', { name: 'Viewport' });

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

/** Waits for the stack to finish evaluating and the picture to stop changing. */
async function settled(): Promise<void> {
  await expect(window.getByTestId('layers-evaluation')).not.toContainText('Evaluating', { timeout: 60_000 });
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

async function click(x: number, y: number): Promise<void> {
  const area = await canvas().boundingBox();
  if (!area) {
    throw new Error('The viewport canvas has no box.');
  }

  await window.mouse.click(area.x + area.width * x, area.y + area.height * y);
}

test('a decal is placed with a click and scaled by its handle, a path takes points, and both move without a stroke', async () => {
  await newLibrary(app, window, join(scratch, 'Game.ptlib'));
  await newProject(app, window, 'Housing', join(scratch, 'meshes', 'housing_low.glb'));
  await expect(window.getByTestId('paint-picture')).toBeVisible({ timeout: 60_000 });
  await expect(window.getByTestId('viewport-mesh')).toHaveText('housing_low.glb', { timeout: 30_000 });

  // A decal and a path read where each texel is on the model, which the project's own bake says.
  await window.getByTestId('open-bake').click();
  await window.getByTestId('bake-start').click();
  await expect(window.getByTestId('bake-state')).toHaveText('Done', { timeout: 180_000 });
  await window.keyboard.press('Escape');

  await window.getByTestId('paint-add-fill').click();
  await window.getByTestId('paint-fill-baseColor').locator('button').first().click();
  await window.getByRole('textbox', { name: 'Hex color' }).fill('#e02010');
  await window.getByRole('textbox', { name: 'Hex color' }).press('Enter');
  await window.keyboard.press('Escape');
  await settled();
  // A red fill with no projection covers the whole housing; as a decal not yet placed it covers none of it.
  await window.getByTestId('paint-fill-projection').selectOption('Decal');
  await settled();
  const before = await shot();
  await window.screenshot({ path: join(shots, 'surface-decal-before.png') });

  await window.getByTestId('surface-place-decal').click();
  await click(0.5, 0.5);
  await expect(window.getByTestId('surface-decal')).toBeVisible({ timeout: 30_000 });
  await expect.poll(async () => changed(before, await shot()), { timeout: 60_000 }).toBeGreaterThan(500);
  await settled();
  const placed = await shot();
  await window.screenshot({ path: join(shots, 'surface-decal-placed.png') });
  const red = await changed(before, placed);

  // Dragging the corner handle out makes it bigger: one edit, drawn by the Worker once the hand comes off.
  const handle = await window.getByTestId('surface-decal-scale').boundingBox();
  if (!handle) {
    throw new Error('The decal has no scale handle on screen.');
  }

  await window.mouse.move(handle.x + handle.width / 2, handle.y + handle.height / 2);
  await window.mouse.down();
  await window.mouse.move(handle.x + handle.width / 2 + 60, handle.y + handle.height / 2 + 60, { steps: 12 });
  await window.mouse.up();
  await expect.poll(async () => changed(before, await shot()), { timeout: 60_000 }).toBeGreaterThan(red * 1.3);
  await settled();
  await window.screenshot({ path: join(shots, 'surface-decal-scaled.png') });

  // And undo puts it back as it was placed: it was never paint.
  await window.keyboard.press(process.platform === 'darwin' ? 'Meta+z' : 'Control+z');
  await expect.poll(async () => changed(placed, await shot()), { timeout: 60_000 }).toBeLessThan(red * 0.2);

  // The same fill along a path through three clicks.
  await window.getByTestId('paint-fill-projection').selectOption('Path');
  await settled();
  const unpathed = await shot();
  await window.getByTestId('surface-add-points').click();
  for (const across of [0.24, 0.32, 0.4]) {
    await click(across, 0.62);
    await expect(window.getByTestId('surface-path-count')).toContainText(
      `${Math.round((across - 0.16) / 0.08)} points`,
      {
        timeout: 30_000
      }
    );
  }

  await window.getByTestId('surface-add-points').click();
  await expect.poll(async () => changed(unpathed, await shot()), { timeout: 60_000 }).toBeGreaterThan(100);
  await settled();
  await window.screenshot({ path: join(shots, 'surface-path.png') });
  await expect(window.getByTestId('surface-path-point-2')).toBeVisible();

  await window.getByTestId('surface-path-ribbon').click();
  await settled();
  await window.screenshot({ path: join(shots, 'surface-ribbon.png') });
});
