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

test('a start with no library asks nothing the engine refuses (#192)', async () => {
  // Whatever the window asked from its first request on: the listener is on before anything is drawn.
  const refused: string[] = [];
  window.on('response', response => {
    if (response.status() >= 400) {
      refused.push(`${response.status()} ${new URL(response.url()).pathname}`);
    }
  });
  window.on('requestfailed', request => {
    refused.push(`${request.failure()?.errorText ?? 'failed'} ${new URL(request.url()).pathname}`);
  });
  await window.reload();

  const status = window.getByRole('status', { name: 'Engine status' });
  await expect(status).toContainText('No library open');
  await expect(status).toContainText('GPU ready');
  await expect(window.getByText('Start', { exact: true }).first()).toBeVisible();

  expect(refused.filter(line => line.includes('/api/'))).toEqual([]);
});

test('the app starts both sidecars and shows the shell', async () => {
  const status = window.getByRole('status', { name: 'Engine status' });

  await expect(status).toContainText('No library open');
  await expect(status).toContainText('GPU ready');
  for (const pane of ['Library', 'Start', 'Inspector']) {
    await expect(window.getByText(pane, { exact: true }).first()).toBeVisible();
  }

  await expect(window.locator('html')).toHaveAttribute('data-skin', 'bezel');
});
