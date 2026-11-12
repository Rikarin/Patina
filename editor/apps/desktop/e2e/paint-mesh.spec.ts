import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { existsSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { answerOpen, leave, newLibrary, newProject, pick } from './projects';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
const bench = resolve(__dirname, '../../../../engine/tools/Patina.Bench/bin/Release/net11.0/patina-bench.dll');
const shots = resolve(__dirname, '../../../dist/e2e-results');

/** Linux CI runs under Xvfb with no GPU, and Chromium no longer falls back to software WebGL unless told to. */
const softwareWebGl = process.platform === 'linux' ? ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] : [];

let scratch: string;
let app: ElectronApplication;
let window: Page;

test.beforeEach(async () => {
  test.skip(process.platform === 'win32' && !!process.env['CI'], 'Windows CI has no Vulkan.');

  scratch = mkdtempSync(join(tmpdir(), 'patina-e2e-'));
  execFileSync('dotnet', [bench, 'bake-pair', join(scratch, 'meshes'), '0.05']);
  app = await electron.launch({
    args: [...softwareWebGl, main],
    env: { ...process.env, PATINA_USER_DATA: join(scratch, 'user-data') }
  });
  window = await app.firstWindow();
  await expect(window.getByRole('status', { name: 'Server status' })).toContainText('GPU ready');
});

test.afterEach(async () => {
  await leave(app);
  // The Server is still on its way out, and writes its last files as it goes.
  rmSync(scratch, { recursive: true, force: true, maxRetries: 20, retryDelay: 250 });
});

const canvas = () => window.getByRole('application', { name: 'Viewport' });

async function box() {
  const found = await canvas().boundingBox();
  if (!found) {
    throw new Error('The viewport canvas has no box.');
  }

  return found;
}

/**
 * The canvas as the window shows it, one pixel per CSS pixel. Every count below is in those, so a threshold means the
 * same on a Retina display, where a device-pixel shot holds four times as many, and on CI's Xvfb, where it does not.
 */
async function shot(): Promise<string> {
  return (await canvas().screenshot({ scale: 'css' })).toString('base64');
}

/** The picture inside the canvas's border, which lights up as the pane takes the pointer and is no part of it. */
async function inner(): Promise<string> {
  const area = await box();
  const inset = 6;
  const clip = { x: area.x + inset, y: area.y + inset, width: area.width - 2 * inset, height: area.height - 2 * inset };
  return (await window.screenshot({ clip, scale: 'css' })).toString('base64');
}

/**
 * How many pixels moved by more than `step` levels of red. The default is well above anything a settled picture does
 * between frames; a change that is only shading wants less.
 */
async function changed(before: string, after: string, step = 24): Promise<number> {
  return window.evaluate(
    async ({ first, second, step }) => {
      const load = async (data: string) => {
        const bytes = Uint8Array.from(atob(data), character => character.charCodeAt(0));
        const bitmap = await createImageBitmap(new Blob([bytes], { type: 'image/png' }));
        const surface = new OffscreenCanvas(bitmap.width, bitmap.height);
        const context = surface.getContext('2d');
        if (!context) {
          throw new Error('No 2D context.');
        }

        context.drawImage(bitmap, 0, 0);
        return context.getImageData(0, 0, bitmap.width, bitmap.height).data;
      };
      const [one, two] = [await load(first), await load(second)];
      let count = 0;
      for (let at = 0; at < one.length; at += 4) {
        count += Math.abs((one[at] ?? 0) - (two[at] ?? 0)) > step ? 1 : 0;
      }

      return count;
    },
    { first: before, second: after, step }
  );
}

async function openOnMesh(): Promise<void> {
  await newLibrary(app, window, join(scratch, 'Game.ptlib'));
  await newProject(app, window, 'Housing', join(scratch, 'meshes', 'housing_low.glb'));
  await expect(window.getByTestId('paint-picture')).toBeVisible({ timeout: 60_000 });
  await expect(window.getByTestId('viewport-mesh')).toHaveText('housing_low.glb', { timeout: 30_000 });
  await expect(window.getByTestId('viewport-paint')).toBeVisible();
  await settled();
}

/** Waits for the picture to stop changing: the mesh opens first, and the stack's maps reach it after that. */
async function settled(): Promise<void> {
  await expect
    .poll(async () => Number(await canvas().getAttribute('data-frames')), { timeout: 30_000 })
    .toBeGreaterThan(0);
  await expect
    .poll(
      async () => {
        const earlier = await shot();
        await window.waitForTimeout(700);
        return changed(earlier, await shot());
      },
      { timeout: 30_000 }
    )
    .toBeLessThan(40);
}

async function drag(fromX: number, fromY: number, toX: number, toY: number): Promise<void> {
  const area = await box();
  await window.mouse.move(area.x + area.width * fromX, area.y + area.height * fromY);
  await window.mouse.down();
  await window.mouse.move(area.x + area.width * toX, area.y + area.height * toY, { steps: 24 });
  await window.mouse.up();
}

test('height painted on the mesh lights it as a bump, sharp or smooth', async () => {
  await openOnMesh();
  await window.getByTestId('paint-add-channel').click();
  await window.getByTestId('paint-add-channel-height').click();
  await expect(window.getByTestId('texture-set-height-normal')).toBeVisible({ timeout: 30_000 });
  await window.getByTestId('layers-channel').click();
  await window.getByRole('option', { name: 'Height' }).locator('visible=true').click();
  await window.getByTestId('paint-colour').locator('button').first().click();
  await window.getByRole('textbox', { name: 'Hex color' }).fill('#ffffff');
  await window.keyboard.press('Enter');
  await window.keyboard.press('Escape');
  // ⚠ Every shot is inside the canvas's border, with the pointer off it. The brush ring and the border the pane
  // lights each move more pixels than this counts, and a test that counted them passed on a build where height
  // reached nothing.
  const away = () => window.mouse.move(2, 2);
  // The opening view fits the whole model in the pane. Three steps in bring it to where the stroke below was drawn
  // for, about 2.2 bounding radii away, so the bump covers as much of the picture as it did.
  await canvas().focus();
  for (let step = 0; step < 3; step++) {
    await window.keyboard.press('+');
  }

  await away();
  await settled();
  const before = await inner();
  await window.screenshot({ path: join(shots, 'paint-height-before.png') });

  await drag(0.3, 0.45, 0.72, 0.55);
  await away();
  // The stroke is in the 2D view once the stack has evaluated it, whatever the viewport makes of it.
  await expect(window.getByTestId('layers-evaluation')).not.toContainText('Evaluating', { timeout: 30_000 });
  await settled();
  await window.screenshot({ path: join(shots, 'paint-height.png') });
  // Height has no colour of its own, so whatever changed on the picture is the surface's lighting.
  await expect.poll(async () => changed(before, await inner()), { timeout: 30_000 }).toBeGreaterThan(300);

  const smooth = await inner();
  await window.getByTestId('texture-set-height-normal').getByText('Sharp').click();
  await away();
  // Smooth blurs the height a texel, which a frame drawn one pixel per CSS pixel (CI's Xvfb) shows as a softer change
  // than a Retina frame does: no pixel moved 24 levels there, though hundreds moved 8. Nothing reaching the viewport
  // moves none.
  await expect.poll(async () => changed(smooth, await inner(), 8), { timeout: 30_000 }).toBeGreaterThan(50);
  await settled();
  await window.screenshot({ path: join(shots, 'paint-height-sharp.png') });
});

test('a brush on the mesh shows on it in the viewport, across its flats, and undo takes it back', async () => {
  await openOnMesh();
  await window.getByTestId('paint-colour').locator('button').first().click();
  await window.getByRole('textbox', { name: 'Hex color' }).fill('#e01810');
  await window.keyboard.press('Enter');
  await window.keyboard.press('Escape');
  const before = await shot();

  await drag(0.3, 0.45, 0.72, 0.55);
  await expect.poll(async () => changed(before, await shot()), { timeout: 30_000 }).toBeGreaterThan(1500);
  await expect(window.getByTestId('viewport-stamp')).toBeAttached();
  await window.screenshot({ path: join(shots, 'paint-mesh.png') });

  await window.getByTestId('app-undo').click();
  await expect.poll(async () => changed(before, await shot()), { timeout: 30_000 }).toBeLessThan(40);
  await window.getByTestId('app-redo').click();
  await expect.poll(async () => changed(before, await shot()), { timeout: 30_000 }).toBeGreaterThan(1500);
});

test('a fill takes a painted mask, stroked on the mesh', async () => {
  await openOnMesh();
  await window.getByTestId('paint-add-fill').click();
  await window.getByTestId('paint-fill-baseColor').locator('button').first().click();
  await window.getByRole('textbox', { name: 'Hex color' }).fill('#00ff00');
  await window.getByRole('textbox', { name: 'Hex color' }).press('Enter');
  await window.keyboard.press('Escape');
  const fill = window.locator('[data-testid^=paint-layer-fill]').first();
  await fill.click({ button: 'right' });
  await window.getByTestId('layer-menu-mask-paint').click();
  await expect(window.getByTestId('paint-size')).toBeVisible();
  await settled();
  const before = await shot();

  await drag(0.3, 0.45, 0.72, 0.55);

  // More than the first dab: the whole stroke, and the settled picture. A dab alone changes about 2,200 pixels and the
  // stroke about 11,000.
  await expect.poll(async () => changed(before, await shot()), { timeout: 30_000 }).toBeGreaterThan(5000);
  await settled();
  await expect(window.getByTestId('app-failure')).toHaveCount(0);
  await expect(window.getByTestId('layers-evaluation')).toBeVisible();
  await window.screenshot({ path: join(shots, 'paint-mask-on-fill.png') });
});

test('a drag turns the model when nothing chosen can be painted', async () => {
  await openOnMesh();
  await expect(canvas()).toHaveClass(/cursor-crosshair/);
  await window.getByTestId('paint-add-fill').click();
  await expect(canvas()).toHaveClass(/cursor-grab/);
  await settled();
  const before = await shot();

  await drag(0.3, 0.45, 0.72, 0.55);

  await expect.poll(async () => changed(before, await shot()), { timeout: 30_000 }).toBeGreaterThan(1500);
  await expect(window.getByTestId('app-failure')).toHaveCount(0);
  await expect(window.getByTestId('viewport-brush')).toHaveCount(0);
});

test('a mesh with no UVs is unwrapped when a project is made, and the setting stays on the project', async () => {
  await newLibrary(app, window, join(scratch, 'Unwrap.ptlib'));
  const mesh = join(scratch, 'plain.obj');
  writeFileSync(
    mesh,
    [
      ...['-1 -1 -1', '1 -1 -1', '1 1 -1', '-1 1 -1', '-1 -1 1', '1 -1 1', '1 1 1', '-1 1 1'].map(v => `v ${v}`),
      ...['5 6 7 8', '4 3 2 1', '2 3 7 6', '1 5 8 4', '4 8 7 3', '1 2 6 5'].map(f => `f ${f}`)
    ].join('\n')
  );
  await newProject(app, window, 'Plain', mesh);
  await expect(window.getByTestId('viewport-paint')).toBeVisible({ timeout: 60_000 });
  await expect(window.getByTestId('app-failure')).toHaveCount(0);

  await app.evaluate(({ BrowserWindow }) => {
    BrowserWindow.getAllWindows()[0]?.webContents.send('patina:command', 'projectSettings');
  });
  await expect(window.getByTestId('project-settings-mesh-name')).toHaveText('plain_unwrapped.glb');
  await window.getByTestId('project-settings-unwrap').click();
  await window.getByTestId('project-settings-apply').click();
  await expect(window.getByTestId('project-settings')).toHaveCount(0);
  const yaml = readFileSync(join(scratch, 'Unwrap.ptlib', 'projects', 'plain', 'project.yaml'), 'utf8');
  expect(yaml).toContain('autoUnwrap: false');
  expect(yaml).toContain('plain_unwrapped.glb');
  expect(yaml).not.toContain('file: plain.obj');
});

test('each material of a mesh is a texture set, and paint follows a repacked mesh', async () => {
  await newLibrary(app, window, join(scratch, 'Sets.ptlib'));
  const quads = (u: number, v: number): string[] => [
    'usemtl metal',
    'f 1/1 2/2 3/3 4/4',
    'usemtl wood',
    'f 5/1 6/2 7/3 8/4',
    `# ${u} ${v}`
  ];
  const obj = (name: string, low: number, high: number): string => {
    writeFileSync(
      join(scratch, name),
      [
        ...['0 0 0', '1 0 0', '1 1 0', '0 1 0', '2 0 0', '3 0 0', '3 1 0', '2 1 0'].map(v => `v ${v}`),
        ...[`${low} ${low}`, `${high} ${low}`, `${high} ${high}`, `${low} ${high}`].map(t => `vt ${t}`),
        ...quads(low, high)
      ].join('\n')
    );
    return join(scratch, name);
  };
  await newProject(app, window, 'Sets', obj('sets.obj', 0, 0.5));
  await expect(window.getByTestId('texture-sets')).toBeVisible({ timeout: 60_000 });
  await expect(window.getByTestId('app-failure')).toHaveCount(0);
  await expect(window.getByTestId('texture-set-wood')).toHaveAttribute('aria-selected', 'false');

  await window.getByTestId('texture-set-wood').click();
  await expect(window.getByTestId('texture-set-wood')).toHaveAttribute('aria-selected', 'true');
  await window.getByTestId('texture-set-metal').click();
  await expect(window.getByTestId('texture-set-metal')).toHaveAttribute('aria-selected', 'true');

  await window.getByTestId('paint-add').click();
  const picture = await window.getByTestId('paint-picture').boundingBox();
  if (!picture) {
    throw new Error('The map is not on screen.');
  }

  await window.mouse.move(picture.x + picture.width * 0.2, picture.y + picture.height * 0.2);
  await window.mouse.down();
  await window.mouse.move(picture.x + picture.width * 0.3, picture.y + picture.height * 0.3, { steps: 8 });
  await window.mouse.up();
  const unsaved = window.getByTestId('document-tab-name').filter({ hasText: '•' });
  await expect(unsaved).toHaveCount(1, { timeout: 30_000 });
  await app.evaluate(({ BrowserWindow }) => {
    BrowserWindow.getAllWindows()[0]?.webContents.send('patina:command', 'save');
  });
  await expect(unsaved).toHaveCount(0, { timeout: 30_000 });

  await answerOpen(app, obj('sets.obj', 0.5, 1));
  await app.evaluate(({ BrowserWindow }) => {
    BrowserWindow.getAllWindows()[0]?.webContents.send('patina:command', 'projectSettings');
  });
  await window.getByTestId('project-settings-mesh').click();
  // A new mesh closes the open documents and loses their undo, and the dialog says so before Apply does it.
  await expect(window.getByTestId('project-settings-closes')).toContainText('undo history is lost');
  await expect(window.getByTestId('project-settings-apply')).toHaveText('Close and Apply');
  await window.screenshot({ path: join(shots, 'project-settings-closes.png') });
  await window.getByTestId('project-settings-apply').click();
  await expect(window.getByTestId('project-settings')).toHaveCount(0, { timeout: 60_000 });
  await expect(window.getByTestId('texture-sets')).toBeVisible({ timeout: 60_000 });
  expect(readdirSync(join(scratch, 'Sets.ptlib', 'projects', 'sets', '.paint-backup'))).toHaveLength(1);
});

test('a layer keeps to named meshes, a folder takes a mask, and the mesh reloads from its source', async () => {
  await newLibrary(app, window, join(scratch, 'Parts.ptlib'));
  const mesh = join(scratch, 'parts.obj');
  writeFileSync(
    mesh,
    [
      ...['0 0 0', '1 0 0', '1 1 0', '0 1 0', '2 0 0', '3 0 0', '3 1 0', '2 1 0'].map(v => `v ${v}`),
      ...['0 0', '0.5 0', '0.5 1', '0 1', '0.5 0', '1 0', '1 1', '0.5 1'].map(t => `vt ${t}`),
      'o Lid',
      'f 1/1 2/2 3/3 4/4',
      'o Hinge',
      'f 5/5 6/6 7/7 8/8'
    ].join('\n')
  );
  await newProject(app, window, 'Parts', mesh);
  await expect(window.getByTestId('paint-picture')).toBeVisible({ timeout: 60_000 });

  await window.getByTestId('paint-add-fill').click();
  await window.getByTestId('paint-fill-baseColor').locator('button').first().click();
  await window.getByRole('textbox', { name: 'Hex color' }).fill('#00ff00');
  await window.getByRole('textbox', { name: 'Hex color' }).press('Enter');
  await window.keyboard.press('Escape');
  const fillRow = window.locator('[data-testid^=paint-layer-fill]');
  await expect(fillRow.getByTestId('paint-layer-blend')).toContainText('Normal');
  await fillRow.getByTestId('paint-layer-opacity').locator('[role=slider]').focus();
  await window.keyboard.press('ArrowLeft');
  await expect(fillRow.getByTestId('layer-blending')).toContainText('99');
  await window.keyboard.press('ArrowRight');

  const greenAt = (across: number): Promise<number> =>
    window.getByTestId('paint-picture').evaluate((image, x) => {
      const canvas = globalThis.document.createElement('canvas');
      [canvas.width, canvas.height] = [64, 64];
      const context = canvas.getContext('2d');
      context?.drawImage(image as HTMLImageElement, 0, 0, 64, 64);
      return context?.getImageData(Math.floor(x * 64), 32, 1, 1).data[1] ?? 0;
    }, across);
  await expect.poll(() => greenAt(0.75), { timeout: 30_000 }).toBeGreaterThan(200);

  await window.locator('[data-testid^=paint-layer-fill]').getByTestId('layer-mesh-mask').click();
  await expect(window.getByTestId('layer-meshes')).toContainText('Hinge');
  await window.getByTestId('layer-mesh-Hinge').click();
  await expect(window.getByTestId('layer-mesh-Hinge')).not.toBeChecked();
  await window.mouse.click(4, 300); // anywhere else closes the list
  await expect.poll(() => greenAt(0.75), { timeout: 30_000 }).toBeLessThan(200);
  expect(await greenAt(0.25)).toBeGreaterThan(200);

  const fillLayer = window.locator('[data-testid^=paint-layer-fill]').first();
  await fillLayer.click({ button: 'right' });
  await window.getByTestId('layer-menu-effect').focus();
  await window.keyboard.press('ArrowRight');
  await window.getByTestId('layer-menu-effect-fill').click();
  await expect(fillLayer.getByTestId('layer-content-mark')).toHaveClass(/bg-warning/);
  await expect(window.getByTestId('layer-effect-mark')).toHaveCount(1);
  await window.getByTestId('paint-fill-baseColor').locator('button').first().click();
  await window.getByRole('textbox', { name: 'Hex color' }).fill('#ff0000');
  await window.getByRole('textbox', { name: 'Hex color' }).press('Enter');
  await window.keyboard.press('Escape');
  // Inside the layer it keeps to the layer's meshes: red on the lid, and nothing on the hinge.
  const redAt = (across: number): Promise<number> =>
    window.getByTestId('paint-picture').evaluate((image, x) => {
      const canvas = globalThis.document.createElement('canvas');
      [canvas.width, canvas.height] = [64, 64];
      const context = canvas.getContext('2d');
      context?.drawImage(image as HTMLImageElement, 0, 0, 64, 64);
      return context?.getImageData(Math.floor(x * 64), 32, 1, 1).data[0] ?? 0;
    }, across);
  await expect.poll(() => redAt(0.25), { timeout: 30_000 }).toBeGreaterThan(200);
  expect(await greenAt(0.25)).toBeLessThan(50);
  expect(await redAt(0.75)).toBeLessThan(200);
  await window.getByTestId('layer-effect-remove').click();
  await expect(window.getByTestId('layer-effect-mark')).toHaveCount(0);
  await expect.poll(() => greenAt(0.25), { timeout: 30_000 }).toBeGreaterThan(200);

  await fillLayer.click();
  await fillLayer.click({ button: 'right' });
  await window.getByTestId('layer-menu-mask-white').click();
  await expect(fillLayer.getByTestId('layer-mask')).toBeVisible();
  await fillLayer.getByTestId('layer-mask').click();
  await fillLayer.click({ button: 'right' });
  await window.getByTestId('layer-menu-effect').focus();
  await window.keyboard.press('ArrowRight');
  await window.getByTestId('layer-menu-effect-Levels').click();
  await expect(fillLayer.getByTestId('layer-mask-mark')).toHaveClass(/bg-warning/);
  await expect(window.getByTestId('layer-work')).toContainText('Levels');
  await window.getByTestId('layer-work-remove').click();
  await expect(window.getByTestId('layer-work')).toHaveCount(0);

  await window.getByTestId('paint-add-group').click();
  const folder = window.locator('[data-testid^=paint-layer-]', { hasText: 'Folder' }).first();
  await folder.click({ button: 'right' });
  await window.getByTestId('layer-menu-mask-paint').click();
  await expect(folder.getByTestId('layer-mask')).toBeVisible();

  await window.locator('[data-testid^=paint-layer-paint]').first().click();
  await window.keyboard.press('ControlOrMeta+g');
  await expect(window.locator('[data-testid^=paint-layer-]', { hasText: 'Folder' })).toHaveCount(2);
  // The folder arrives before the layer moves in it, and a save that overtakes that move rightly stays unsaved.
  await expect(window.locator('[data-testid^=paint-layer-paint]').first()).toHaveAttribute('aria-level', '2');

  await app.evaluate(({ BrowserWindow }) => {
    BrowserWindow.getAllWindows()[0]?.webContents.send('patina:command', 'save');
  });
  await expect(window.getByTestId('document-tab-name').filter({ hasText: '•' })).toHaveCount(0, { timeout: 30_000 });
  const yaml = join(scratch, 'Parts.ptlib', 'projects', 'parts', 'project.yaml');
  expect(readFileSync(yaml, 'utf8')).not.toContain(mesh);
  await app.evaluate(({ BrowserWindow }) => {
    BrowserWindow.getAllWindows()[0]?.webContents.send('patina:command', 'reloadMesh');
  });
  await expect(window.getByTestId('app-notice')).toHaveText('The mesh has not changed.');
  await expect(window.getByTestId('app-failure')).toHaveCount(0);
  await expect(window.getByTestId('paint-picture')).toBeVisible({ timeout: 60_000 });
  expect(existsSync(join(scratch, 'Parts.ptlib', 'projects', 'parts', '.paint-backup'))).toBe(false);

  await window.locator('[data-testid=tree-row][data-path="parts"]').click({ button: 'right' });
  await window.getByTestId('menu-project-settings').click();
  await expect(window.getByTestId('project-settings')).toBeVisible();
});

test('Save All on the quit dialog saves a painted stack and quits', async () => {
  await openOnMesh();
  await drag(0.3, 0.45, 0.72, 0.55);
  await expect(window.getByTestId('app-undo')).toBeEnabled();

  const closed = app.waitForEvent('close');
  void app.evaluate(({ app: electronApp }) => {
    electronApp.quit();
  });
  await expect(window.getByTestId('app-quitting')).toBeVisible();
  await window.getByRole('button', { name: 'Save All' }).click();
  await closed;
});

test('a line, a mirrored stroke and an alpha paint through the same journal', async () => {
  await openOnMesh();
  const before = await shot();

  await window.getByTestId('paint-tool-line').click();
  await drag(0.35, 0.35, 0.6, 0.4);
  await expect.poll(async () => changed(before, await shot()), { timeout: 30_000 }).toBeGreaterThan(400);
  const lined = await shot();

  await window.getByTestId('paint-tool-brush').click();
  await pick(window, 'paint-symmetry', /^mirror$/iu);
  await drag(0.62, 0.55, 0.66, 0.62);
  await expect.poll(async () => changed(lined, await shot()), { timeout: 30_000 }).toBeGreaterThan(200);
  await window.screenshot({ path: join(shots, 'paint-tools.png') });
});

/** Every file below a folder, for finding the project's own. */
function filesBelow(folder: string): string[] {
  return readdirSync(folder, { recursive: true, encoding: 'utf8' }).map(file => join(folder, file));
}

test('a preset takes up its brush, a radial symmetry paints its copies, and the project keeps the palette', async () => {
  await openOnMesh();
  await window.getByTestId('brush-preset-Hard Round').click();
  await expect(window.getByTestId('paint-size-readout')).toHaveText('16 px');

  const before = await shot();
  await drag(0.42, 0.42, 0.46, 0.44);
  await expect.poll(async () => changed(before, await shot()), { timeout: 30_000 }).toBeGreaterThan(40);
  await settled();
  const one = await changed(before, await shot());

  await window.getByTestId('app-undo').click();
  await settled();
  await pick(window, 'paint-symmetry', /^radial$/iu);
  await pick(window, 'paint-symmetry-axis', /^y$/iu);
  // The axis the copies turn round is drawn over the picture, where the Worker puts it.
  await expect(window.getByTestId('viewport-symmetry')).toBeVisible({ timeout: 10_000 });
  await window.mouse.move(0, 0);
  const plain = await shot();
  await drag(0.42, 0.42, 0.46, 0.44);
  // ⚠ Waited for as the first stroke was: a picture that has not changed yet is a settled one, so `settled` alone
  // passed on the frame before the stroke arrived whenever the Worker was slow, and the copies were counted as none.
  await expect.poll(async () => changed(plain, await shot()), { timeout: 30_000 }).toBeGreaterThan(one);
  await settled();
  // Four copies round the up axis, of which the camera sees two or three: more than one stroke's worth either way.
  expect(await changed(plain, await shot())).toBeGreaterThan(one * 1.5);
  await window.screenshot({ path: join(shots, 'paint-radial.png') });
  await pick(window, 'paint-symmetry', /^mirror$/iu);
  await pick(window, 'paint-symmetry-axis', /^x$/iu);
  await expect(window.getByTestId('viewport-symmetry').locator('polygon')).toBeVisible();
  await window.screenshot({ path: join(shots, 'paint-mirror-plane.png') });

  await window.getByTestId('paint-keep-colour').click();
  await expect(window.getByTestId('paint-palette').locator('button')).toHaveCount(1);
  const project = filesBelow(join(scratch, 'Game.ptlib', 'projects')).find(file => file.endsWith('project.yaml'));
  expect(project).toBeDefined();
  await expect.poll(() => readFileSync(project ?? '', 'utf8'), { timeout: 10_000 }).toContain('palette:');
  expect(readFileSync(project ?? '', 'utf8')).toContain('kind: radial');
});

test('layers of every kind compose on the mesh', async () => {
  await openOnMesh();
  const before = await shot();

  await window.getByTestId('paint-add-fill').click();
  await expect(window.getByTestId('paint-layer-name')).toHaveValue('Fill');
  await window.getByTestId('paint-fill-baseColor').locator('button').first().click();
  await window.getByRole('textbox', { name: 'Hex color' }).fill('#2050e0');
  await window.getByRole('textbox', { name: 'Hex color' }).press('Enter');
  await window.keyboard.press('Escape');
  await expect.poll(async () => changed(before, await shot()), { timeout: 30_000 }).toBeGreaterThan(2000);

  await window
    .locator('[data-testid^=paint-layer-fill]')
    .first()
    .getByTestId('paint-layer-blend')
    .locator('button')
    .first()
    .click();
  await window.getByRole('option', { name: 'Multiply', exact: true }).click();
  await expect(window.getByTestId('paint-layers')).toContainText('Multiply');

  await window.getByTestId('paint-add-filter').click();
  await expect(window.getByTestId('paint-layers')).toContainText('Filter');
  await window.getByTestId('paint-add-group').click();
  await window.getByTestId('paint-add').click();
  await expect(window.getByTestId('paint-layers').getByText('Paint')).not.toHaveCount(0);
  await window.screenshot({ path: join(shots, 'paint-layers.png') });
  await expect(window.getByTestId('app-failure')).toHaveCount(0);
});
