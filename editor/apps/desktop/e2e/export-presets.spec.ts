import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { existsSync, mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { leave, newLibrary, newProject } from './projects';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
const bench = resolve(__dirname, '../../../../engine/tools/Patina.Bench/bin/Release/net10.0/patina-bench.dll');
const softwareWebGl = process.platform === 'linux' ? ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] : [];

/** The preset editor and the compression preview (#155, #217), by clicks, from a project made here. */
let scratch: string;
let app: ElectronApplication;
let window: Page;

test.beforeEach(async () => {
  test.skip(process.platform === 'win32' && !!process.env['CI'], 'Windows CI has no Vulkan.');
  scratch = mkdtempSync(join(tmpdir(), 'patina-presets-'));
  execFileSync('dotnet', [bench, 'bake-pair', join(scratch, 'meshes'), '0.05']);
  const env = { ...process.env, PATINA_USER_DATA: join(scratch, 'user-data'), PATINA_LIBRARY: '' };
  app = await electron.launch({ args: [...softwareWebGl, main], env });
  window = await app.firstWindow();
});

test.afterEach(async () => {
  await leave(app);
  rmSync(scratch, { recursive: true, force: true, maxRetries: 20, retryDelay: 250 });
});

test('a duplicated preset is a file in the project, and a BC7 file shows what compression does', async () => {
  // A build of the project's maps may take the 120 s its wait allows, so the test's own limit is well above it.
  test.setTimeout(300_000);
  const shots = process.env['PATINA_SHOTS'];
  const library = join(scratch, 'Ember.ptlib');
  await newLibrary(app, window, library);
  await newProject(app, window, 'Housing', join(scratch, 'meshes', 'housing_low.glb'));

  await window.getByTestId('open-export').click();
  await window.getByTestId('export-preset').click();
  await window.getByRole('option', { name: 'Godot' }).click();
  await window.getByTestId('export-edit-presets').click();
  await expect(window.getByTestId('preset-built-in')).toBeVisible();
  await window.getByTestId('preset-duplicate').click();
  await expect(window.getByTestId('preset-id')).toHaveValue('godot-copy');
  await expect(window.getByTestId('preset-file-written').first()).toContainText('Housing');
  await window.getByTestId('preset-material-gltf').check();
  if (shots) {
    await window.screenshot({ path: join(shots, 'preset-editor.png') });
  }
  await window.getByTestId('preset-editor-save').click();
  await expect(window.getByTestId('preset-reveal')).toBeVisible();
  const kept = join(library, 'projects', 'housing', 'export-presets', 'godot-copy.yaml');
  await expect.poll(() => existsSync(kept)).toBe(true);
  expect(readFileSync(kept, 'utf8')).toContain('materials: [gltf]');
  await window.getByTestId('preset-editor-close').click();
  await expect(window.getByTestId('export-preset-info')).toContainText('this project');

  // The ORM file as BC7 in a KTX2: there is nothing to compress until the maps are built.
  await window.getByTestId('export-file-format').nth(2).click();
  await window.getByRole('option', { name: 'KTX2' }).click();
  await window.getByTestId('export-file-compression').first().click();
  await window.getByRole('option', { name: 'BC7' }).click();
  await window.getByTestId('export-file-preview').first().click();
  await expect(window.getByTestId('compression-unbuilt')).toBeVisible({ timeout: 30_000 });
  await window.getByTestId('compression-build').click();
  await expect(window.getByTestId('compression-written')).toBeVisible({ timeout: 120_000 });
  // A new project's layers are flat, which BC7 holds exactly; a lane says so rather than giving a number.
  await expect(window.getByTestId('compression-psnr').first()).toHaveText(/dB|Lossless/u);
  await expect(window.getByTestId('compression-memory')).toContainText('uncompressed');
  if (shots) {
    await window.screenshot({ path: join(shots, 'compression-view.png') });
  }
});
