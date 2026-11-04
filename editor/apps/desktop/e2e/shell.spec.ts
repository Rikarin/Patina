import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');

let scratch: string;
let app: ElectronApplication;
let window: Page;

test.beforeEach(async () => {
  scratch = mkdtempSync(join(tmpdir(), 'patina-e2e-'));
  app = await electron.launch({
    args: [main],
    env: { ...process.env, PATINA_USER_DATA: join(scratch, 'user-data') }
  });
  window = await app.firstWindow();
});

test.afterEach(async () => {
  await app.close();
  rmSync(scratch, { recursive: true, force: true });
});

test('the app starts both sidecars and shows the shell', async () => {
  const status = window.getByRole('status', { name: 'Server status' });

  await expect(status).toContainText('No library open');
  await expect(status).toContainText('Worker connected');
  for (const pane of ['Library', 'Viewport', 'Inspector']) {
    await expect(window.getByText(pane, { exact: true }).first()).toBeVisible();
  }

  await expect(window.locator('html')).toHaveAttribute('data-skin', 'bezel');
});
