import {
  _electron as electron,
  expect,
  test,
  type ElectronApplication,
  type Locator,
  type Page
} from '@playwright/test';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { colouredGlb, leave, newLibrary, newProject } from './projects';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
const softwareWebGl = process.platform === 'linux' ? ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] : [];

let scratch: string;
let app: ElectronApplication;
let window: Page;

test.beforeEach(async () => {
  test.skip(process.platform === 'win32' && !!process.env['CI'], 'Windows CI has no Vulkan.');
  scratch = mkdtempSync(join(tmpdir(), 'patina-e2e-layers-'));
  app = await electron.launch({
    args: [...softwareWebGl, main],
    env: { ...process.env, PATINA_USER_DATA: join(scratch, 'user-data'), PATINA_LIBRARY: '' }
  });
  window = await app.firstWindow();
  await newLibrary(app, window, join(scratch, 'Game.ptlib'));
  await newProject(app, window, 'Crate');
  await expect(window.getByTestId('paint-layer-paint')).toBeVisible({ timeout: 30_000 });
});

test.afterEach(async () => {
  await leave(app);
  rmSync(scratch, { recursive: true, force: true, maxRetries: 20, retryDelay: 250 });
});

const names = () => window.getByTestId('paint-layers').getByRole('treeitem');

/** Drags a row to a place on another, as a fraction of its height. */
async function drop(from: Locator, to: Locator, along: number): Promise<void> {
  const box = await to.boundingBox();
  if (!box) {
    throw new Error('The row is not on screen.');
  }

  await from.dragTo(to, { targetPosition: { x: box.width / 2, y: box.height * along } });
}

test('layers go into folders and back, a folder closes, and a drag undoes as one step', async () => {
  await window.getByTestId('paint-add-group').click();
  const folder = window.locator('[data-testid^=paint-layer-folder]');
  await expect(folder).toBeVisible();

  const paint = window.getByTestId('paint-layer-paint');
  await drop(paint, folder, 0.5);
  await expect(paint).toHaveAttribute('aria-level', '2');

  await folder.getByTestId('layer-toggle').click();
  await expect(paint).toHaveCount(0);
  await folder.getByTestId('layer-toggle').click();
  await expect(paint).toHaveAttribute('aria-level', '2');

  await window.getByTestId('paint-undo').click();
  await expect(paint).toHaveAttribute('aria-level', '1');
});

test('a layer reorders by dragging', async () => {
  const base = window.getByTestId('paint-layer-base');
  await expect(names().first()).toHaveAttribute('data-testid', 'paint-layer-paint');

  await drop(base, window.getByTestId('paint-layer-paint'), 0.1);

  await expect(names().first()).toHaveAttribute('data-testid', 'paint-layer-base');
});

test('a mask has its own thumbnail, and choosing it is what the brush paints', async () => {
  const paint = window.getByTestId('paint-layer-paint');
  await paint.click({ button: 'right' });
  await window.getByTestId('layer-menu-mask-paint').click();

  await expect(paint.getByTestId('layer-mask')).toBeVisible();
  await expect(paint.getByTestId('layer-mask')).toHaveClass(/outline/);

  await paint.getByTestId('layer-thumbnail').click();
  await expect(paint.getByTestId('layer-thumbnail')).toHaveClass(/outline/);

  await paint.click({ button: 'right' });
  await window.getByTestId('layer-menu-mask-remove').click();
  await expect(paint.getByTestId('layer-mask')).toHaveCount(0);
});

test('the eye hides a layer, and Properties follows what is chosen', async () => {
  const base = window.getByTestId('paint-layer-base');
  await base.click();
  await expect(window.getByTestId('paint-fill-source')).toBeVisible();
  await expect(window.getByTestId('paint-size')).toHaveCount(0);

  await window.getByTestId('paint-layer-paint').click();
  await expect(window.getByTestId('paint-size')).toBeVisible();

  await base.getByTestId('layer-eye').click();
  await expect(base.getByTestId('layer-eye')).toHaveAttribute('aria-pressed', 'false');
});

test('a fill shows only the projection controls that apply', async () => {
  await window.getByTestId('paint-add-fill').click();
  await expect(window.getByTestId('paint-fill-projection')).toHaveCount(0); // a constant is the same from every side

  await window.getByTestId('paint-fill-source').click();
  await window.getByRole('option', { name: 'Texture' }).click();
  const projection = window.getByTestId('paint-fill-projection');
  await expect(projection).toHaveValue('Uv');
  await expect(window.getByTestId('paint-fill-scale')).toBeVisible(); // a UV fill can repeat, turn and move
  await expect(window.getByTestId('paint-fill-rotation')).toBeVisible();
  await expect(window.getByTestId('paint-fill-sharpness')).toHaveCount(0);

  await projection.selectOption('Triplanar');
  await expect(window.getByTestId('paint-fill-sharpness')).toBeVisible();
  await expect(window.getByTestId('paint-fill-axis')).toHaveCount(0);

  await projection.selectOption('Planar');
  await window.getByTestId('paint-fill-axis').selectOption('Z');
  await expect(window.getByTestId('paint-fill-axis')).toHaveValue('Z');
  await expect(window.getByTestId('paint-fill-scale')).toBeVisible();
});

test('colours painted in Blender bake to an id map, and a mask selects one', async () => {
  const mesh = join(scratch, 'coloured.glb');
  writeFileSync(mesh, colouredGlb());
  await newProject(app, window, 'Coloured', mesh);
  await expect(window.getByTestId('paint-picture')).toBeVisible({ timeout: 30_000 });

  await window.getByTestId('paint-add-fill').click();
  await window.getByTestId('paint-fill-baseColor').fill('#00ff00');
  await window.getByTestId('paint-fill-baseColor').dispatchEvent('change');
  const fill = window.locator('[data-testid^=paint-layer-fill]');
  await fill.click({ button: 'right' });
  await window.getByTestId('layer-menu-mask-colour').click();
  await expect(window.getByTestId('paint-id-none')).toBeVisible();

  await window.getByTestId('open-bake').click();
  await window.getByTestId('bake-start').click();
  await expect(window.getByTestId('bake-state')).toHaveText('done', { timeout: 120_000 });
  await window.keyboard.press('Escape');

  // Both colours came through the file, as they were painted.
  const swatches = window.getByTestId('paint-id-colour');
  await expect(swatches).toHaveCount(2, { timeout: 30_000 });
  await expect(window.getByRole('button', { name: 'Select #ff0000' })).toBeVisible();
  await expect(window.getByRole('button', { name: 'Select #0000ff' })).toBeVisible();

  // Red is the left half of the map, so the fill lands there and nowhere else.
  await window.getByRole('button', { name: 'Select #ff0000' }).click();
  await expect.poll(() => greenAt(0.25), { timeout: 30_000 }).toBeGreaterThan(200);
  expect(await greenAt(0.75)).toBeLessThan(200);

  // The eyedropper finds the other colour from a click on the map.
  await window.getByTestId('paint-eyedropper').click();
  const box = await window.getByTestId('paint-picture').boundingBox();
  if (!box) {
    throw new Error('The map is not on screen.');
  }

  await window.mouse.click(box.x + box.width * 0.75, box.y + box.height / 2);
  await expect.poll(() => greenAt(0.75), { timeout: 30_000 }).toBeGreaterThan(200);
  expect(await greenAt(0.25)).toBeLessThan(200);

  // It finds one on the mesh too. Which side faces the camera is the camera's business, so it tries both.
  const canvas = await window.getByRole('img', { name: 'Viewport' }).boundingBox();
  if (!canvas) {
    throw new Error('The viewport is not on screen.');
  }

  for (const across of [0.42, 0.58]) {
    await window.getByTestId('paint-eyedropper').click();
    await window.mouse.click(canvas.x + canvas.width * across, canvas.y + canvas.height / 2);
    await window.waitForTimeout(500);
  }

  await expect.poll(() => greenAt(0.25), { timeout: 30_000 }).toBeGreaterThan(200);
});

test('layers are selected together, deleted by key, and a material drags in where it lands', async () => {
  await window.getByTestId('paint-add-fill').click();
  await window.getByTestId('paint-add-fill').click();
  await window.getByTestId('paint-add-fill').click();
  const fills = window.locator('[data-testid^=paint-layer-fill]');
  await expect(fills).toHaveCount(3);

  // A material dropped below the first fill sits there and not on top.
  await window.getByLabel('Search materials').fill('Plastic');
  await window
    .getByTestId('shelf-material')
    .first()
    .dragTo(fills.first(), { targetPosition: { x: 40, y: 38 } });
  const stack = window.getByTestId('paint-layers');
  const rows = stack.getByRole('treeitem');
  await expect(rows.nth(1)).not.toHaveAttribute('data-testid', /paint-layer-fill/u);
  await expect(rows.first()).toHaveAttribute('data-testid', /paint-layer-fill/u);

  const count = await rows.count();
  await fills.first().click();
  await fills.nth(2).click({ modifiers: ['Shift'] });
  // From the top fill to the bottom one, with the material's layers between them.
  const selected = count - 2;
  await expect(stack.locator('[role=treeitem][aria-selected=true]')).toHaveCount(selected);

  await window.keyboard.press('Delete');
  await expect(rows).toHaveCount(count - selected);

  await rows.first().click();
  await window.keyboard.press('Meta+Backspace');
  await expect(rows).toHaveCount(count - selected - 1);

  // Cmd+Z on a Mac reaches the menu and never the page, so it is the menu item that has to undo.
  await app.evaluate(({ Menu }) => {
    Menu.getApplicationMenu()?.getMenuItemById('edit-undo')?.click();
  });
  await expect(rows).toHaveCount(count - selected);
});

test('a mask runs effects, and they stay in order', async () => {
  await window.getByTestId('paint-add-fill').click();
  const fill = window.locator('[data-testid^=paint-layer-fill]').first();
  await fill.click({ button: 'right' });
  await window.getByTestId('layer-menu-mask-colour').click();

  await window.getByTestId('paint-mask-effect-add').selectOption({ label: 'Invert' });
  await expect(window.getByTestId('paint-mask-effect-0')).toContainText('Invert');
  await window.getByTestId('paint-mask-effect-add').selectOption({ label: 'Blur' });
  await expect(window.getByTestId('paint-mask-effect-1')).toContainText('Blur');

  await window.getByRole('button', { name: 'Remove Invert' }).click();
  await expect(window.getByTestId('paint-mask-effect-0')).toContainText('Blur');
  await expect(window.getByTestId('paint-mask-effect-1')).toHaveCount(0);
});

/** How green the map is at a place across it. */
const greenAt = (across: number) =>
  window.getByTestId('paint-picture').evaluate((image, x) => {
    const canvas = globalThis.document.createElement('canvas');
    [canvas.width, canvas.height] = [64, 64];
    const context = canvas.getContext('2d');
    context?.drawImage(image as HTMLImageElement, 0, 0, 64, 64);
    return context?.getImageData(Math.floor(x * 64), 32, 1, 1).data[1] ?? 0;
  }, across);
