import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { existsSync, mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
const softwareWebGl = process.platform === 'linux' ? ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] : [];

/** Preferences (#189): what only this app shows is the app's file, and autosave is the engine's own file. */
let scratch: string;
let app: ElectronApplication;
let window: Page;

const read = (file: string): unknown => (existsSync(file) ? JSON.parse(readFileSync(file, 'utf8')) : null);

test.beforeEach(async () => {
  scratch = mkdtempSync(join(tmpdir(), 'patina-e2e-'));
  app = await electron.launch({
    args: [...softwareWebGl, main],
    // Never the person's own engine preferences, which this test writes.
    env: {
      ...process.env,
      PATINA_USER_DATA: join(scratch, 'user-data'),
      Patina__PreferencesFile: join(scratch, 'engine-preferences.json')
    }
  });
  window = await app.firstWindow();
});

test.afterEach(async () => {
  await app.close();
  rmSync(scratch, { recursive: true, force: true });
});

test('Preferences opens from its key, changes the theme and keeps autosave with the engine', async () => {
  await expect(window.getByRole('status', { name: 'Engine status' })).toContainText('GPU ready', { timeout: 30_000 });
  await window.keyboard.press(process.platform === 'darwin' ? 'Meta+Comma' : 'Control+Comma');
  const dialog = window.getByTestId('preferences');
  await expect(dialog).toBeVisible();
  const html = window.locator('html');
  await expect(html).toHaveClass(/\bdark\b/);

  await window.getByTestId('preferences-theme').getByText('Light', { exact: true }).click();
  await expect(html).not.toHaveClass(/\bdark\b/);
  await expect.poll(() => read(join(scratch, 'user-data', 'preferences.json'))).toMatchObject({ theme: 'light' });
  await window.getByTestId('preferences-theme').getByText('Dark', { exact: true }).click();
  await expect(html).toHaveClass(/\bdark\b/);

  // No library is open, so there is no cache to show; autosave is the engine's whatever is open.
  await expect(window.getByTestId('preferences-cache-none')).toBeVisible();
  const interval = window.getByTestId('preferences-autosave-interval').getByRole('spinbutton');
  await expect(interval).toHaveValue('1');
  await interval.fill('3');
  await interval.press('Enter');
  await expect
    .poll(() => read(join(scratch, 'engine-preferences.json')))
    .toMatchObject({ autosave: { intervalSeconds: 180 } });

  // Open again, it asks the engine, which has kept what was set.
  await window.getByTestId('preferences-done').click();
  await expect(dialog).toBeHidden();
  await window.keyboard.press(process.platform === 'darwin' ? 'Meta+Comma' : 'Control+Comma');
  await expect(window.getByTestId('preferences-autosave-interval').getByRole('spinbutton')).toHaveValue('3');
});
