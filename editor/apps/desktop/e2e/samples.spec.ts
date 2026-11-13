import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { existsSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { leave, newLibrary } from './projects';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
const softwareWebGl = process.platform === 'linux' ? ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] : [];

/** A first look (#190) and named checkpoints (#209): the sample Start offers, and a snapshot taken and restored. */
let scratch: string;
let library: string;
let app: ElectronApplication;
let window: Page;

test.beforeEach(async () => {
  scratch = mkdtempSync(join(tmpdir(), 'patina-e2e-samples-'));
  library = join(scratch, 'Game.ptlib');
  app = await electron.launch({
    args: [...softwareWebGl, main],
    env: { ...process.env, PATINA_USER_DATA: join(scratch, 'user-data') }
  });
  window = await app.firstWindow();
});

test.afterEach(async () => {
  await leave(app);
  rmSync(scratch, { recursive: true, force: true, maxRetries: 20, retryDelay: 250 });
});

const command = (name: string) =>
  app.evaluate(({ BrowserWindow }, sent) => {
    BrowserWindow.getAllWindows()[0]?.webContents.send('patina:command', sent);
  }, name);

test('Start opens the Knot sample, and a snapshot of it is taken and put back', async () => {
  test.skip(process.platform === 'win32' && !!process.env['CI'], 'Windows CI has no Vulkan.');
  test.setTimeout(240_000);
  await newLibrary(app, window, library);

  await window.getByTestId('start-sample-Knot').click();
  await expect(window.getByTestId('paint-project')).toHaveText('Knot', { timeout: 60_000 });
  const layers = window.locator('[data-layer]');
  await expect(layers.first()).toBeVisible({ timeout: 60_000 });
  const count = await layers.count();

  await command('showHistory');
  await window.getByTestId('snapshot-name').fill('As shipped');
  await window.getByTestId('snapshot-take').click();
  await expect(window.getByTestId('snapshot-label')).toHaveText(['As shipped'], { timeout: 30_000 });

  // An edit not yet saved: restoring saves it first, keeps it as a snapshot of its own, then puts the sample back.
  await window.getByTestId('paint-add-fill').click();
  await expect(layers).toHaveCount(count + 1);
  await window.getByTestId('snapshot-restore').last().click();
  await expect(layers).toHaveCount(count, { timeout: 60_000 });
  // The document closed and opened again in its workspace, which shows its own panes.
  await command('showHistory');
  await expect(window.getByTestId('snapshot-label')).toHaveText(['Before restoring As shipped', 'As shipped'], {
    timeout: 60_000
  });
  expect(existsSync(join(library, 'projects', 'knot', '.snapshots'))).toBe(true);
});
