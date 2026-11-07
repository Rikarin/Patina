import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { appendFileSync, existsSync, mkdtempSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { answerOpen, colouredGlb, leave, newLibrary, newProject } from './projects';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
const softwareWebGl = process.platform === 'linux' ? ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] : [];

/** What leaving unsaved work, and coming back to it, does. Autosave runs every second so a test need not wait. */
let scratch: string;
let library: string;
let app: ElectronApplication;
let window: Page;

async function launch(opening = ''): Promise<void> {
  const env = {
    ...process.env,
    PATINA_USER_DATA: join(scratch, 'user-data'),
    PATINA_LIBRARY: opening,
    Patina__AutosaveSeconds: '1'
  };
  app = await electron.launch({ args: [...softwareWebGl, main], env });
  window = await app.firstWindow();
}

/** What the 2D view shows, by its pixels: its address changes with every picture, the same or not. */
const pictureHash = () =>
  window.getByTestId('paint-picture').evaluate(image => {
    const source = image as HTMLImageElement;
    const canvas = globalThis.document.createElement('canvas');
    [canvas.width, canvas.height] = [64, 64];
    const context = canvas.getContext('2d');
    context?.drawImage(source, 0, 0, 64, 64);
    return context?.getImageData(0, 0, 64, 64).data.join(',');
  });

async function stroke(): Promise<void> {
  const picture = window.getByTestId('paint-picture');
  await expect(picture).toBeVisible({ timeout: 30_000 });
  const box = await picture.boundingBox();
  if (!box) {
    throw new Error('The atlas has no box.');
  }

  await window.mouse.move(box.x + 40, box.y + 40);
  await window.mouse.down();
  await window.mouse.move(box.x + 120, box.y + 120, { steps: 5 });
  await window.mouse.up();
  await expect(window.getByTestId('paint-unsaved')).toBeVisible({ timeout: 15_000 });
}

/** The picture before and after a stroke that is left unsaved. */
async function paint(): Promise<{ saved: string | undefined; painted: string | undefined }> {
  await newLibrary(app, window, library);
  await newProject(app, window, 'Crate');
  await expect(window.getByTestId('paint-picture')).toBeVisible({ timeout: 30_000 });
  const saved = await pictureHash();
  await stroke();
  await expect.poll(pictureHash, { timeout: 15_000 }).not.toBe(saved);
  return { saved, painted: await pictureHash() };
}

const autosaves = () => {
  const folder = join(library, '.ptcache', 'autosave');
  return existsSync(folder) ? readdirSync(folder).length : 0;
};

const open = () => window.locator('[data-testid=tree-row][data-path="crate"]').dblclick();

/** Paints, waits for autosave, and goes without saving, as a crash would. */
async function crashWithUnsavedWork(): Promise<{ saved: string | undefined; painted: string | undefined }> {
  await launch();
  const pictures = await paint();
  await expect.poll(autosaves, { timeout: 30_000 }).toBeGreaterThan(0);
  await leave(app);
  await launch(library);
  await expect(window.getByTestId('app-recovery')).toBeVisible({ timeout: 60_000 });
  return pictures;
}

test.beforeEach(() => {
  test.skip(process.platform === 'win32' && !!process.env['CI'], 'Windows CI has no Vulkan.');
  scratch = mkdtempSync(join(tmpdir(), 'patina-saving-'));
  library = join(scratch, 'Ember.ptlib');
});

test.afterEach(async () => {
  await leave(app);
  rmSync(scratch, { recursive: true, force: true, maxRetries: 20, retryDelay: 250 });
});

test("Don't Save on a stack throws the edits away, and they stay gone", async () => {
  await launch();
  const { saved } = await paint();
  await expect.poll(autosaves, { timeout: 30_000 }).toBeGreaterThan(0);

  await window.getByTestId('document-tab').filter({ hasText: 'Crate' }).getByTestId('document-tab-close').click();
  await expect(window.getByTestId('project-leaving')).toBeVisible();
  await window.getByTestId('project-discard').click();
  await expect(window.getByTestId('document-tab')).toHaveCount(0);
  await expect(window.getByTestId('app-failure')).toHaveCount(0);
  expect(autosaves()).toBe(0);

  await open();
  await expect(window.getByTestId('paint-picture')).toBeVisible({ timeout: 30_000 });
  await expect.poll(pictureHash, { timeout: 15_000 }).toBe(saved);
  await expect(window.getByTestId('paint-unsaved')).toHaveCount(0);
  await expect(window.getByTestId('app-recovery')).toHaveCount(0);
});

const command = (name: string) =>
  app.evaluate(({ BrowserWindow }, sent) => {
    BrowserWindow.getAllWindows()[0]?.webContents.send('patina:command', sent);
  }, name);

/** One grey pixel. */
const Pixel = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAAAAAA6fptVAAAACklEQVR4nGP4DwABAQEAG7buVgAAAABJRU5ErkJggg==';

test('File > Import says what it did, lists the file, and refuses what nothing reads', async () => {
  await launch();
  await newLibrary(app, window, library);
  await newProject(app, window, 'Crate');

  const picture = join(scratch, 'rust.png');
  writeFileSync(picture, Buffer.from(Pixel, 'base64'));
  await answerOpen(app, picture);
  await command('import');
  await expect(window.getByTestId('app-notice')).toHaveText('Imported rust.png into Crate');
  await expect(window.getByTestId('app-failure')).toHaveCount(0);

  await command('projectSettings');
  await expect(window.getByTestId('project-file').filter({ hasText: 'rust.png' })).toContainText('Picture');
  await window.keyboard.press('Escape');
  await expect(window.getByTestId('project-files')).toHaveCount(0);

  const model = join(scratch, 'model.dae');
  writeFileSync(model, '<COLLADA/>');
  await answerOpen(app, model);
  await command('import');
  await expect(window.getByTestId('app-failure')).toContainText("Nothing in Patina reads 'model.dae'");
  await expect(window.getByTestId('app-notice')).toHaveCount(0);
  expect(existsSync(join(library, 'projects', 'crate', 'pictures', 'model.dae'))).toBe(false);
});

test('Reloading a mesh asks first, keeps paint on the same layout and moves it on a new one', async () => {
  const mesh = join(scratch, 'plate.glb');
  writeFileSync(mesh, colouredGlb());
  await launch();
  await newLibrary(app, window, library);
  await newProject(app, window, 'Plate', mesh);
  await stroke();
  await command('save');
  await expect(window.getByTestId('paint-unsaved')).toHaveCount(0, { timeout: 30_000 });

  await command('reloadMesh');
  await expect(window.getByTestId('app-notice')).toHaveText('The mesh has not changed.');
  await expect(window.getByTestId('document-tab')).toHaveCount(1);

  const backup = join(library, 'projects', 'plate', '.paint-backup');

  // A sculpt: the file changed and the islands did not.
  writeFileSync(mesh, colouredGlb(1.5));
  await command('reloadMesh');
  await expect(window.getByTestId('mesh-reload')).toContainText('lose their undo');
  await window.getByRole('button', { name: 'Cancel' }).click();
  await expect(window.getByTestId('mesh-reload')).toHaveCount(0);
  expect(existsSync(backup)).toBe(false);

  await command('reloadMesh');
  await window.getByTestId('mesh-reload-confirm').click();
  await expect(window.getByTestId('mesh-reload')).toHaveCount(0);
  await expect(window.getByTestId('document-tab')).toHaveCount(1, { timeout: 60_000 });
  await expect(window.getByTestId('paint-picture')).toBeVisible({ timeout: 60_000 });
  await expect(window.getByTestId('app-failure')).toHaveCount(0);
  expect(existsSync(backup)).toBe(false);

  // A new layout moves the paint, and keeps what it was.
  writeFileSync(mesh, colouredGlb(1.5, 0.25));
  await command('reloadMesh');
  await window.getByTestId('mesh-reload-confirm').click();
  await expect.poll(() => existsSync(backup), { timeout: 60_000 }).toBe(true);
  await expect(window.getByTestId('paint-picture')).toBeVisible({ timeout: 60_000 });
});

test('Saving over a file that changed asks first, and Overwrite saves', async () => {
  await launch();
  await paint();
  appendFileSync(join(library, 'projects', 'crate', 'crate.vxlayers'), '\n# someone else\n');

  await window.getByTestId('paint-save').click();

  await expect(window.getByTestId('file-conflict')).toContainText('Crate changed on disk', { timeout: 30_000 });
  await expect(window.getByTestId('paint-unsaved')).toBeVisible();
  await window.getByRole('button', { name: 'Cancel' }).click();
  await expect(window.getByTestId('file-conflict')).toHaveCount(0);
  await expect(window.getByTestId('paint-unsaved')).toBeVisible();

  await window.getByTestId('paint-save').click();
  await window.getByTestId('file-overwrite').click();
  await expect(window.getByTestId('file-conflict')).toHaveCount(0);
  await expect(window.getByTestId('paint-unsaved')).toHaveCount(0, { timeout: 30_000 });
  await expect(window.getByTestId('app-failure')).toHaveCount(0);
});

test('A save the Server cannot do says why, and keeps the tab', async () => {
  await launch();
  await paint();
  await window.route('**/api/v1/stacks/*/save*', route =>
    route.fulfill({
      status: 503,
      contentType: 'application/problem+json',
      body: JSON.stringify({ title: 'Unavailable', detail: 'No Worker came.' })
    })
  );

  await window.getByTestId('paint-save').click();

  await expect(window.getByTestId('app-failure')).toContainText('No Worker came.');
  await expect(window.getByTestId('paint-unsaved')).toBeVisible();

  await window.getByTestId('document-tab-close').click();
  await window.getByTestId('project-save').click();
  await expect(window.getByTestId('app-failure')).toContainText('No Worker came.');
  await expect(window.getByTestId('document-tab')).toHaveCount(1);
});

test('Discard after a crash goes back to the saved file, and nothing is left to offer', async () => {
  const { saved } = await crashWithUnsavedWork();

  await window.getByTestId('recovery-discard').click();

  await expect(window.getByTestId('app-recovery')).toHaveCount(0);
  await expect(window.getByTestId('paint-picture')).toBeVisible({ timeout: 30_000 });
  await expect.poll(pictureHash, { timeout: 15_000 }).toBe(saved);
  await expect(window.getByTestId('app-failure')).toHaveCount(0);
  expect(autosaves()).toBe(0);
});

test('Restore after a crash brings the work back', async () => {
  const { painted } = await crashWithUnsavedWork();

  await window.getByTestId('recovery-restore').click();

  await expect(window.getByTestId('app-recovery')).toHaveCount(0);
  await expect(window.getByTestId('paint-picture')).toBeVisible({ timeout: 30_000 });
  await expect.poll(pictureHash, { timeout: 15_000 }).toBe(painted);
  await expect(window.getByTestId('app-failure')).toHaveCount(0);
});

test('Escape decides nothing: the work stays and the next open asks again', async () => {
  await crashWithUnsavedWork();

  await window.keyboard.press('Escape');

  await expect(window.getByTestId('app-recovery')).toHaveCount(0);
  await expect(window.getByTestId('app-failure')).toHaveCount(0);
  expect(autosaves()).toBeGreaterThan(0);

  await window.getByTestId('document-tab-close').click();
  await expect(window.getByTestId('document-tab')).toHaveCount(0);
  await open();
  await expect(window.getByTestId('app-recovery')).toBeVisible({ timeout: 30_000 });
});
