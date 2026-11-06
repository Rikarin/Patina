import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { leave, newLibrary, newProject } from './projects';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
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
  await expect(window.getByRole('status', { name: 'Server status' })).toContainText('Worker connected');
});

test.afterEach(async () => {
  await leave(app);
  // The Server is still on its way out, and writes its last files as it goes.
  rmSync(scratch, { recursive: true, force: true, maxRetries: 20, retryDelay: 250 });
});

test('a brush on the map paints through the Server, and undo takes it back', async () => {
  test.skip(process.platform === 'win32' && !!process.env['CI'], 'Windows CI has no Vulkan.');

  await newLibrary(app, window, join(scratch, 'Game.ptlib'));
  await newProject(app, window, 'Crate');
  const picture = window.getByTestId('paint-picture');
  await expect(picture).toBeVisible({ timeout: 60_000 });
  await expect(window.getByTestId('paint-layer-paint')).toBeVisible();
  const before = await picture.getAttribute('src');

  const box = await picture.boundingBox();
  if (!box) {
    throw new Error('The picture has no box.');
  }

  await window.mouse.move(box.x + box.width * 0.2, box.y + box.height * 0.5);
  await window.mouse.down();
  await window.mouse.move(box.x + box.width * 0.5, box.y + box.height * 0.3, { steps: 8 });
  await window.mouse.move(box.x + box.width * 0.8, box.y + box.height * 0.6, { steps: 8 });
  await window.mouse.up();

  await expect(picture).not.toHaveAttribute('src', before ?? '', { timeout: 30_000 });
  await expect(window.getByTestId('paint-undo')).toBeEnabled();
  await window.waitForTimeout(500);
  await window.screenshot({ path: join(shots, 'paint.png') });
  const painted = await picture.getAttribute('src');

  await window.getByTestId('paint-undo').click();
  await expect(picture).not.toHaveAttribute('src', painted ?? '', { timeout: 30_000 });
  await expect(window.getByTestId('paint-redo')).toBeEnabled();
});
