import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { leave, newLibrary, newProject } from './projects';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
const shots = resolve(__dirname, '../../../dist/e2e-results');
const softwareWebGl = process.platform === 'linux' ? ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] : [];

let scratch: string;
let app: ElectronApplication;
let window: Page;

test.beforeEach(async () => {
  test.skip(process.platform === 'win32' && !!process.env['CI'], 'Windows CI has no Vulkan.');
  scratch = mkdtempSync(join(tmpdir(), 'patina-e2e-sets-'));
  app = await electron.launch({
    args: [...softwareWebGl, main],
    env: { ...process.env, PATINA_USER_DATA: join(scratch, 'user-data'), PATINA_LIBRARY: '' }
  });
  window = await app.firstWindow();
  await newLibrary(app, window, join(scratch, 'Game.ptlib'));
});

test.afterEach(async () => {
  await leave(app);
  rmSync(scratch, { recursive: true, force: true, maxRetries: 20, retryDelay: 250 });
});

/** Two quads side by side, red and blue by the material library beside them in a folder of its own. */
function twoColouredQuads(): string {
  mkdirSync(join(scratch, 'looks'), { recursive: true });
  writeFileSync(join(scratch, 'looks', 'paint.mtl'), 'newmtl red\nKd 0.9 0.1 0.1\nnewmtl blue\nKd 0.1 0.2 0.9\n');
  const lines = [
    'mtllib looks/paint.mtl',
    ...['0 0 0', '1 0 0', '1 1 0', '0 1 0', '1.2 0 0', '2.2 0 0', '2.2 1 0', '1.2 1 0'].map(v => `v ${v}`),
    ...['0 0', '1 0', '1 1', '0 1'].map(t => `vt ${t}`),
    'usemtl red',
    'f 1/1 2/2 3/3 4/4',
    'usemtl blue',
    'f 5/1 6/2 7/3 8/4'
  ];
  const path = join(scratch, 'quads.obj');
  writeFileSync(path, lines.join('\n'));
  return path;
}

test('a mesh says what it is, and a texture set changes its size in Properties', async () => {
  test.setTimeout(180_000);
  await newProject(app, window, 'Quads', twoColouredQuads());
  await expect(window.getByTestId('texture-sets')).toBeVisible({ timeout: 60_000 });

  // The open set's size, in Properties' document section, which the change reaches the Texture Sets readout of.
  const settings = window.getByTestId('texture-set-settings');
  await expect(settings).toBeVisible({ timeout: 30_000 });
  await expect(window.getByTestId('paint-document-mesh-facts')).toContainText('2 triangles');
  await window.getByTestId('paint-document').screenshot({ path: join(shots, 'sets-1-settings.png') });
  await window.getByTestId('texture-set-height').click();
  await window.getByRole('option', { name: '512', exact: true }).click();
  await expect(window.getByTestId('texture-set-size')).toContainText('×512', { timeout: 30_000 });
  await expect(window.getByTestId('app-undo')).toHaveAttribute('aria-label', 'Undo Texture set settings');
  await settings.screenshot({ path: join(shots, 'sets-2-non-square.png') });

  await app.evaluate(({ BrowserWindow }) => {
    BrowserWindow.getAllWindows()[0]?.webContents.send('patina:command', 'projectSettings');
  });
  const facts = window.getByTestId('mesh-facts');
  await expect(facts).toBeVisible({ timeout: 30_000 });
  await expect(window.getByTestId('mesh-facts-triangles')).toContainText('4 triangles');
  await expect(window.getByTestId('mesh-facts-material')).toHaveCount(2);
  await expect(window.getByTestId('mesh-facts-units')).toContainText('does not say');
  await window.getByTestId('project-settings').screenshot({ path: join(shots, 'sets-3-mesh-facts.png') });
});
