import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { copyFileSync, existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { answerOpen, leave, newLibrary, newProject } from './projects';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
const bench = resolve(__dirname, '../../../../engine/tools/Patina.Bench/bin/Release/net11.0/patina-bench.dll');
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
  // Its export may take the 120 s its wait allows, so the test's own 120 s limit would cut it off first.
  test.setTimeout(300_000);
  const library = join(scratch, 'Ember.ptlib');
  await newLibrary(app, window, library);

  await window.getByTestId('open-export').click();
  await window.getByTestId('export-preset').click();
  await window.getByRole('option', { name: 'Godot' }).click();
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
  await window.getByTestId('export-preset').click();
  await window.getByRole('option', { name: 'Godot' }).click();
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
  // Its export may take the 120 s its wait allows, so the test's own 120 s limit would cut it off first.
  test.setTimeout(300_000);
  const library = join(scratch, 'Ember.ptlib');
  await newLibrary(app, window, library);
  await newProject(app, window, 'Housing', join(scratch, 'meshes', 'housing_low.glb'));

  await window.getByTestId('open-export').click();
  await expect(window.getByTestId('export-scope')).toContainText('of Housing');
  await window.getByTestId('export-preset').click();
  await window.getByRole('option', { name: 'Godot' }).click();
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

/** A USD layer of one material, its base color an image beside it and its roughness a number. */
const brick = `#usda 1.0

def Material "Brick"
{
    token outputs:surface.connect = </Brick/Surface.outputs:surface>

    def Shader "Surface"
    {
        uniform token info:id = "UsdPreviewSurface"
        color3f inputs:diffuseColor.connect = </Brick/Colour.outputs:rgb>
        float inputs:roughness = 0.7
        token outputs:surface
    }

    def Shader "Colour"
    {
        uniform token info:id = "UsdUVTexture"
        asset inputs:file = @brick.png@
        float3 outputs:rgb
    }
}
`;

test('Import Materials brings a USD file’s material into the project as a graph that reads its picture', async () => {
  const library = join(scratch, 'Ember.ptlib');
  await newLibrary(app, window, library);
  await newProject(app, window, 'Housing', join(scratch, 'meshes', 'housing_low.glb'));
  // A one-pixel PNG, which is all the graph needs to read.
  const pixel = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==';
  writeFileSync(join(scratch, 'brick.png'), Buffer.from(pixel, 'base64'));
  writeFileSync(join(scratch, 'brick.usda'), brick);

  await answerOpen(app, join(scratch, 'brick.usda'));
  await app.evaluate(({ Menu }) => {
    Menu.getApplicationMenu()?.getMenuItemById('importMaterials')?.click();
  });

  // The file is named as every new document's is, in lower case, which Linux tells apart from Brick.vxtexgraph.
  const graph = join(library, 'projects', 'housing', 'brick.vxtexgraph');
  await expect.poll(() => existsSync(graph), { timeout: 30_000 }).toBe(true);
  expect(readFileSync(graph, 'utf8')).toContain('pictures/brick/brick.png');
  expect(existsSync(join(library, 'projects', 'housing', 'pictures', 'brick', 'brick.png'))).toBe(true);
});
