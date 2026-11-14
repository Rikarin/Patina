import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { copyFileSync, existsSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { answerOpen, leave, newLibrary, newProject, removeScratch } from './projects';

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
  removeScratch(scratch);
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

  // The dialog's settings are the project's, in its own file, where the next export starts. The dialog keeps them a
  // moment after the last change, which a quick export can finish before.
  const project = () => readFileSync(join(library, 'projects', 'housing', 'project.yaml'), 'utf8');
  await expect.poll(project, { timeout: 10_000 }).toContain('export:');
  expect(project()).toContain('format: tga');
  expect(project()).toContain('mode: transparent');

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

/** Imports `brick` into the open project through Import Materials, and gives the graph file it makes. */
async function importBrick(library: string): Promise<string> {
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
  return graph;
}

test('Import Materials brings a USD file’s material into the project as a graph that reads its picture', async () => {
  const library = join(scratch, 'Ember.ptlib');
  await newLibrary(app, window, library);
  await newProject(app, window, 'Housing', join(scratch, 'meshes', 'housing_low.glb'));

  const graph = await importBrick(library);
  expect(readFileSync(graph, 'utf8')).toContain('pictures/brick/brick.png');
  expect(existsSync(join(library, 'projects', 'housing', 'pictures', 'brick', 'brick.png'))).toBe(true);
});

/**
 * A graph in a texturing project is none of the sets the project ships, and the Document scope exports it on its own
 * (#153), under its own name, leaving the project's own files alone.
 */
test('a graph its texturing project does not ship exports on its own from the Document scope', async () => {
  // Its export may take the 120 s its wait allows, so the test's own 120 s limit would cut it off first.
  test.setTimeout(300_000);
  const library = join(scratch, 'Ember.ptlib');
  await newLibrary(app, window, library);
  await newProject(app, window, 'Housing', join(scratch, 'meshes', 'housing_low.glb'));
  const exportGltf = async (scope: string) => {
    await window.getByTestId('open-export').click();
    await expect(window.getByTestId('export-scope')).toHaveText(scope);
    await window.getByTestId('export-preset').click();
    await window.getByRole('option', { name: 'glTF 2.0' }).click();
    await window.getByTestId('export-start').click();
    await expect(window.getByTestId('export-state')).toHaveText('Done', { timeout: 120_000 });
    await expect(window.getByTestId('export-report-failed')).toHaveText('0');
    await window.keyboard.press('Escape');
  };
  const exported = join(library, 'exports', 'gltf', 'housing');
  await exportGltf('The texture set Housing of Housing');
  expect(readdirSync(exported)).toContain('Housing_baseColor.png');
  await importBrick(library);

  const layout = window.getByTestId('library-layout');
  if ((await layout.getAttribute('data-layout')) !== 'list') {
    await layout.click();
  }
  const project = window.locator('[data-testid=tree-row][data-kind=project]').filter({ hasText: 'Housing' });
  if ((await project.getAttribute('aria-expanded')) !== 'true') {
    await project.getByTestId('tree-toggle').click();
  }
  const document = window.locator('[data-testid=tree-row][data-kind=document]').filter({ hasText: 'Brick' });
  await expect(document).toBeVisible({ timeout: 30_000 });
  await document.dblclick();
  await expect(window.getByTestId('document-tab-name').filter({ hasText: 'Brick' })).toBeVisible({ timeout: 60_000 });

  await exportGltf('Brick on its own, as a texture set of Housing');

  // Its textures take its own name beside the set the project ships, whose files its export left where they were.
  const written = readdirSync(exported);
  expect(written).toContain('Brick_baseColor.png');
  expect(written).toContain('Housing_baseColor.png');
});
