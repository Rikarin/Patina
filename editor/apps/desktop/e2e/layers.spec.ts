import { _electron as electron, expect, test, type ElectronApplication, type Locator, type Page } from '@playwright/test';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { leave, newLibrary, newProject } from './projects';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
const softwareWebGl = process.platform === 'linux' ? ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] : [];

let scratch: string;
let app: ElectronApplication;
let window: Page;

test.beforeEach(async () => {
  test.skip(process.platform === 'win32', 'Windows CI has no Vulkan.');
  scratch = mkdtempSync(join(tmpdir(), 'patina-e2e-layers-'));
  app = await electron.launch({
    args: [...softwareWebGl, main],
    env: { ...process.env, PATINA_USER_DATA: join(scratch, 'user-data'), PATINA_LIBRARY: '' }
  });
  window = await app.firstWindow();
  await newLibrary(app, window, join(scratch, 'Game.ptlib'));
  await newProject(app, window, 'Crate');
  await expect(window.getByTestId('paint-layer-paint')).toBeVisible({ timeout: 30_000 });
});

test.afterEach(async () => {
  await leave(app);
  rmSync(scratch, { recursive: true, force: true, maxRetries: 20, retryDelay: 250 });
});

const names = () => window.getByTestId('paint-layers').getByRole('treeitem');

/** Drags a row to a place on another, as a fraction of its height. */
async function drop(from: Locator, to: Locator, along: number): Promise<void> {
  const box = await to.boundingBox();
  if (!box) {
    throw new Error('The row is not on screen.');
  }

  await from.dragTo(to, { targetPosition: { x: box.width / 2, y: box.height * along } });
}

test('layers go into folders and back, a folder closes, and a drag undoes as one step', async () => {
  await window.getByTestId('paint-add-group').click();
  const folder = window.locator('[data-testid^=paint-layer-folder]');
  await expect(folder).toBeVisible();

  const paint = window.getByTestId('paint-layer-paint');
  await drop(paint, folder, 0.5);
  await expect(paint).toHaveAttribute('aria-level', '2');

  await folder.getByTestId('layer-toggle').click();
  await expect(paint).toHaveCount(0);
  await folder.getByTestId('layer-toggle').click();
  await expect(paint).toHaveAttribute('aria-level', '2');

  await window.getByTestId('paint-undo').click();
  await expect(paint).toHaveAttribute('aria-level', '1');
});

test('a layer reorders by dragging', async () => {
  const base = window.getByTestId('paint-layer-base');
  await expect(names().first()).toHaveAttribute('data-testid', 'paint-layer-paint');

  await drop(base, window.getByTestId('paint-layer-paint'), 0.1);

  await expect(names().first()).toHaveAttribute('data-testid', 'paint-layer-base');
});

test('a mask has its own thumbnail, and choosing it is what the brush paints', async () => {
  const paint = window.getByTestId('paint-layer-paint');
  await paint.click({ button: 'right' });
  await window.getByTestId('layer-menu-mask-paint').click();

  await expect(paint.getByTestId('layer-mask')).toBeVisible();
  await expect(paint.getByTestId('layer-mask')).toHaveClass(/outline/);

  await paint.getByTestId('layer-thumbnail').click();
  await expect(paint.getByTestId('layer-thumbnail')).toHaveClass(/outline/);

  await paint.click({ button: 'right' });
  await window.getByTestId('layer-menu-mask-remove').click();
  await expect(paint.getByTestId('layer-mask')).toHaveCount(0);
});

test('the eye hides a layer, and Properties follows what is chosen', async () => {
  const base = window.getByTestId('paint-layer-base');
  await base.click();
  await expect(window.getByTestId('paint-fill-source')).toBeVisible();
  await expect(window.getByTestId('paint-size')).toHaveCount(0);

  await window.getByTestId('paint-layer-paint').click();
  await expect(window.getByTestId('paint-size')).toBeVisible();

  await base.getByTestId('layer-eye').click();
  await expect(base.getByTestId('layer-eye')).toHaveAttribute('aria-pressed', 'false');
});
