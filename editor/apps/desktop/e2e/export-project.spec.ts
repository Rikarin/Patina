import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { copyFileSync, existsSync, mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { leave, newLibrary, newProject } from './projects';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
const bench = resolve(__dirname, '../../../../engine/tools/Patina.Bench/bin/Release/net10.0/patina-bench.dll');
const softwareWebGl = process.platform === 'linux' ? ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] : [];

/**
 * Export from a project made here, by clicks. `export.spec.ts` starts from a library a bench tool wrote, which declares
 * its outputs, and it went on passing while a project made in the app exported nothing and said Done.
 */
let scratch: string;
let app: ElectronApplication;
let window: Page;

test.beforeEach(async () => {
  test.skip(process.platform === 'win32' && !!process.env['CI'], 'Windows CI has no Vulkan.');
  scratch = mkdtempSync(join(tmpdir(), 'patina-export-'));
  execFileSync('dotnet', [bench, 'bake-pair', join(scratch, 'meshes'), '0.05']);
  const env = { ...process.env, PATINA_USER_DATA: join(scratch, 'user-data'), PATINA_LIBRARY: '' };
  app = await electron.launch({ args: [...softwareWebGl, main], env });
  window = await app.firstWindow();
});

test.afterEach(async () => {
  await leave(app);
  rmSync(scratch, { recursive: true, force: true, maxRetries: 20, retryDelay: 250 });
});

test('an empty library says so, and a new project exports with nothing declared', async () => {
  const library = join(scratch, 'Ember.ptlib');
  await newLibrary(app, window, library);

  await window.getByTestId('open-export').click();
  await expect(window.getByTestId('export-preset-info')).toContainText('Godot');
  await window.getByTestId('export-preview').click();
  await expect(window.getByTestId('export-nothing')).toContainText('anything to export');
  await window.getByTestId('export-start').click();
  await window.getByTestId('export-confirm-yes').click();
  await expect(window.getByTestId('export-state')).toHaveText('Done', { timeout: 30_000 });
  await expect(window.getByTestId('export-report-nothing')).toContainText('anything to export');
  await window.keyboard.press('Escape');

  await newProject(app, window, 'Housing', join(scratch, 'meshes', 'housing_low.glb'));
  expect(readFileSync(join(library, 'projects', 'housing', 'project.yaml'), 'utf8')).toContain('outputs: []');

  await window.getByTestId('open-export').click();
  await window.getByTestId('export-preview').click();
  await expect(window.getByTestId('export-impact-rebuilds')).toHaveText('1');
  await expect(window.getByTestId('export-impact-exports')).toHaveText('1');
  await expect(window.getByTestId('export-nothing')).toHaveCount(0);

  await window.getByTestId('export-start').click();
  await expect(window.getByTestId('export-report-built')).toHaveText('1', { timeout: 120_000 });
  await expect(window.getByTestId('export-report-failed')).toHaveText('0');
  await expect(window.getByTestId('export-state')).toHaveText('Done');
  await expect(window.getByTestId('export-report-nothing')).toHaveCount(0);
  expect(existsSync(join(library, 'exports', 'godot', 'housing', 'Housing_albedo.png'))).toBe(true);
});

/** The TGA's alpha lane, in file order: an 18-byte header, then four bytes a pixel, alpha last. */
function alphaOf(file: string): number[] {
  const bytes = readFileSync(file);
  const width = bytes.readUInt16LE(12);
  const height = bytes.readUInt16LE(14);
  return Array.from({ length: width * height }, (_, at) => bytes[18 + at * 4 + 3] ?? -1);
}

test('the dialog exports the document in front as TGA, transparent past its padding', async () => {
  const library = join(scratch, 'Ember.ptlib');
  await newLibrary(app, window, library);
  await newProject(app, window, 'Housing', join(scratch, 'meshes', 'housing_low.glb'));

  await window.getByTestId('open-export').click();
  await expect(window.getByTestId('export-scope')).toContainText('of Housing');
  await window.getByTestId('export-padding').click();
  await window.getByRole('option', { name: 'Dilation + transparent' }).click();
  await expect(window.getByTestId('export-distance-readout')).toHaveText('16 px');
  await window.getByTestId('export-file-format').first().click();
  await window.getByRole('option', { name: 'TGA 8-bit' }).click();
  const shots = process.env['PATINA_SHOTS'];
  if (shots) {
    await window.screenshot({ path: join(shots, 'export-dialog.png') });
  }

  await window.getByTestId('export-start').click();
  await expect(window.getByTestId('export-state')).toHaveText('Done', { timeout: 120_000 });
  await expect(window.getByTestId('export-report-failed')).toHaveText('0');
  await expect(window.getByTestId('export-reveal')).toBeVisible();
  if (shots) {
    await window.screenshot({ path: join(shots, 'export-done.png') });
  }

  // The dialog's settings are the project's, in its own file, where the next export starts.
  const project = readFileSync(join(library, 'projects', 'housing', 'project.yaml'), 'utf8');
  expect(project).toContain('export:');
  expect(project).toContain('format: tga');
  expect(project).toContain('mode: transparent');

  const albedo = join(library, 'exports', 'godot', 'housing', 'Housing_albedo.tga');
  expect(existsSync(albedo)).toBe(true);
  if (shots) {
    copyFileSync(albedo, join(shots, 'export-albedo.tga'));
  }
  const alpha = alphaOf(albedo);
  // Inside the layout and its padding the albedo is opaque; past the padding, padding left nothing.
  expect(alpha.filter(value => value === 255).length).toBeGreaterThan(alpha.length / 10);
  expect(alpha.filter(value => value === 0).length).toBeGreaterThan(alpha.length / 10);
});
