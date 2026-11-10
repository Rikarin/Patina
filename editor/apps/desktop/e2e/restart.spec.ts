import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { leave, newLibrary, newProject, wholeAutosaves } from './projects';

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
    // Never the person's own engine preferences: autosave runs as it does by default.
    env: {
      ...process.env,
      PATINA_USER_DATA: join(scratch, 'user-data'),
      Patina__PreferencesFile: join(scratch, 'engine-preferences.json')
    }
  });
  window = await app.firstWindow();
  await expect(window.getByRole('status', { name: 'Server status' })).toContainText('GPU ready');
});

test.afterEach(async () => {
  await leave(app);
  // The Server is still on its way out, and writes its last files as it goes.
  rmSync(scratch, { recursive: true, force: true, maxRetries: 20, retryDelay: 250 });
});

interface Sidecar {
  name: string;
  pid: number | null;
  state: string;
}

const sidecars = () =>
  window.evaluate(() => (globalThis as unknown as { patina: { sidecars(): Promise<Sidecar[]> } }).patina.sidecars());

/** What the 2D view shows, by its pixels: its address changes with every picture, the same or not. */
const pixels = () =>
  window.getByTestId('paint-picture').evaluate(image => {
    const canvas = globalThis.document.createElement('canvas');
    [canvas.width, canvas.height] = [64, 64];
    const context = canvas.getContext('2d');
    context?.drawImage(image as HTMLImageElement, 0, 0, 64, 64);
    return context?.getImageData(0, 0, 64, 64).data.join(',');
  });

/** How many whole autosaves the library holds. */
const autosaves = () => wholeAutosaves(join(scratch, 'Game.ptlib'));

/** Drags a stroke across the 2D view, from one share of it to another. */
async function stroke(from: [number, number], to: [number, number]): Promise<void> {
  const box = await window.getByTestId('paint-picture').boundingBox();
  if (!box) {
    throw new Error('The picture has no box.');
  }

  await window.mouse.move(box.x + box.width * from[0], box.y + box.height * from[1]);
  await window.mouse.down();
  await window.mouse.move(box.x + box.width * to[0], box.y + box.height * to[1], { steps: 12 });
  await window.mouse.up();
}

test('a texturing document comes back from its file when the Server dies, as it was left (#226)', async () => {
  test.skip(process.platform === 'win32' && !!process.env['CI'], 'Windows CI has no Vulkan.');
  // The Server restarts after a back-off, and the new one starts the Worker's session again: well past 120 s on CI.
  test.setTimeout(300_000);

  await newLibrary(app, window, join(scratch, 'Game.ptlib'));
  await newProject(app, window, 'Crate');
  const picture = window.getByTestId('paint-picture');
  await expect(picture).toBeVisible({ timeout: 60_000 });
  await expect(window.getByTestId('paint-layer-paint')).toBeVisible();

  // An edit the Server holds and has not saved, and a layer chosen that is not the one a document opens on.
  const blank = await picture.getAttribute('src');
  const blankPixels = await pixels();
  await stroke([0.2, 0.5], [0.8, 0.5]);
  await expect(picture).not.toHaveAttribute('src', blank ?? '', { timeout: 30_000 });
  await window.getByTestId('paint-layer-base').click();
  await expect(window.getByTestId('paint-layer-base')).toHaveAttribute('aria-selected', 'true');
  await window.screenshot({ path: join(shots, 'restart-before.png') });
  // Autosave keeps the stroke once the edits have been still a moment (#162).
  await expect.poll(autosaves, { timeout: 60_000 }).toBeGreaterThan(0);

  const server = (await sidecars()).find(sidecar => sidecar.name === 'server')?.pid ?? 0;
  expect(server).toBeGreaterThan(0);
  process.kill(server, 'SIGKILL');
  await expect
    .poll(
      async () => {
        const again = (await sidecars()).find(sidecar => sidecar.name === 'server');
        return again?.state === 'running' && again.pid !== server;
      },
      { timeout: 120_000 }
    )
    .toBe(true);

  // The document opens again from its file, where the stroke never was, and the person is told so.
  await expect(window.getByTestId('paint-notice')).toContainText('opened this document again from its last save', {
    timeout: 120_000
  });
  await expect(window.getByTestId('paint-layer-base')).toHaveAttribute('aria-selected', 'true');
  await expect(window.getByTestId('app-failure')).toHaveCount(0);

  // What autosave kept of it is offered back, and restoring it brings the stroke back (#162).
  await expect(window.getByTestId('app-recovery')).toBeVisible({ timeout: 30_000 });
  await window.screenshot({ path: join(shots, 'restart-recovery.png') });
  await window.getByTestId('recovery-restore').click();
  await expect(picture).toBeVisible({ timeout: 60_000 });
  await expect.poll(pixels, { timeout: 60_000 }).not.toBe(blankPixels);

  // And it takes a brush again, through the new Server.
  await window.getByTestId('paint-layer-paint').click();
  const reopened = await picture.getAttribute('src');
  await stroke([0.5, 0.2], [0.5, 0.8]);
  await expect(picture).not.toHaveAttribute('src', reopened ?? '', { timeout: 60_000 });
  await expect(window.getByTestId('app-undo')).toBeEnabled();
  await expect(window.getByTestId('app-failure')).toHaveCount(0);
  await expect(window.getByRole('status', { name: 'Server status' })).toContainText('GPU ready', { timeout: 60_000 });
  await window.waitForTimeout(3_000);
  await window.screenshot({ path: join(shots, 'restart-after.png') });
});
