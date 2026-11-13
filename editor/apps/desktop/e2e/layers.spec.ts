import {
  _electron as electron,
  expect,
  test,
  type ElectronApplication,
  type Locator,
  type Page
} from '@playwright/test';
import { existsSync, mkdirSync, mkdtempSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { answerOpen, colouredGlb, leave, newLibrary, newProject, pick } from './projects';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
const shots = resolve(__dirname, '../../../dist/e2e-results');
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

  await window.getByTestId('app-undo').click();
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

test('a half set layer says so on its row, and leaves the stack working', async () => {
  await window.getByTestId('paint-add-fill').click();
  const fill = window.locator('[data-testid^=paint-layer-fill]').first();
  await fill.click({ button: 'right' });
  await window.getByTestId('layer-menu-mask-generator').click();

  await expect(fill.getByTestId('layer-fault')).toBeVisible({ timeout: 30_000 });
  await expect(window.getByTestId('layers-problems')).toContainText('generator');
  await expect(window.getByTestId('layers-evaluation')).not.toHaveText('Not evaluated');

  await window.getByTestId('paint-mask-source').click();
  await window.getByRole('option', { name: 'None' }).click();
  await expect(fill.getByTestId('layer-fault')).toHaveCount(0, { timeout: 30_000 });
});

test('layers added with one name are numbered', async () => {
  await window.getByTestId('paint-add').click();
  await expect(names().first()).toContainText('Paint 2');
});

test('Delete Layers deletes the layer with its mask in front, and the mask menu keeps a working mask', async () => {
  await window.getByTestId('paint-add-fill').click();
  const fill = window.locator('[data-testid^=paint-layer-fill]').first();
  await fill.click({ button: 'right' });
  await window.getByTestId('layer-menu-mask-white').click();
  await fill.getByTestId('layer-mask').click();

  await fill.click({ button: 'right' });
  await expect(window.getByTestId('layer-menu-mask-generator')).toHaveCount(0);
  await window.getByTestId('paint-remove').click();

  await expect(fill).toHaveCount(0);
});

test('a graph fill chooses its graph in Properties', async () => {
  await window.getByTestId('paint-add-fill').click();
  await window.getByTestId('paint-fill-source').click();
  await window.getByRole('option', { name: 'Graph' }).click();
  await expect(window.getByTestId('paint-fill-graph')).toBeVisible();
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
  // A constant is the same from every side: it lands over the UVs or on a decal or a path, and nothing projects it.
  // ⚠ Waits for the new fill to be in Properties: a count of none passed before it got there, whatever it offered.
  await expect(window.getByTestId('paint-fill-source')).toContainText('Uniform');
  await window.getByTestId('paint-fill-projection').click();
  await expect(window.getByRole('option', { name: 'UV', exact: true }).locator('visible=true')).toHaveCount(1);
  await expect(window.getByRole('option', { name: 'Tri-planar' }).locator('visible=true')).toHaveCount(0);
  await window.keyboard.press('Escape');

  await window.getByTestId('paint-fill-source').click();
  await window.getByRole('option', { name: 'Bitmap' }).click();
  const projection = window.getByTestId('paint-fill-projection');
  await expect(projection).toContainText('UV');
  await expect(window.getByTestId('paint-fill-scale')).toBeVisible(); // a UV fill can repeat, turn and move
  await expect(window.getByTestId('paint-fill-rotation')).toBeVisible();
  await expect(window.getByTestId('paint-fill-sharpness')).toHaveCount(0);

  await pick(window, 'paint-fill-projection', 'Tri-planar');
  await expect(window.getByTestId('paint-fill-sharpness')).toBeVisible();
  await expect(window.getByTestId('paint-fill-axis')).toHaveCount(0);

  await pick(window, 'paint-fill-projection', 'Planar');
  await pick(window, 'paint-fill-axis', 'Z');
  await expect(window.getByTestId('paint-fill-axis')).toContainText('Z');
  await expect(window.getByTestId('paint-fill-scale')).toBeVisible();
});

test('colours painted in Blender bake to an id map, and a mask selects one', async () => {
  // The bake alone may take the 120 s its wait allows, so the test's own 120 s limit would cut it off first.
  test.setTimeout(300_000);
  const mesh = join(scratch, 'coloured.glb');
  writeFileSync(mesh, colouredGlb());
  await newProject(app, window, 'Coloured', mesh);
  await expect(window.getByTestId('paint-picture')).toBeVisible({ timeout: 30_000 });

  await window.getByTestId('paint-add-fill').click();
  await window.getByTestId('paint-fill-baseColor').locator('button').first().click();
  await window.getByRole('textbox', { name: 'Hex color' }).fill('#00ff00');
  await window.getByRole('textbox', { name: 'Hex color' }).press('Enter');
  await window.keyboard.press('Escape');
  const fill = window.locator('[data-testid^=paint-layer-fill]');
  await fill.click({ button: 'right' });
  await window.getByTestId('layer-menu-mask-colour').click();
  await expect(window.getByTestId('paint-id-none')).toBeVisible();

  await window.getByTestId('open-bake').click();
  await window.getByTestId('bake-start').click();
  await expect(window.getByTestId('bake-state')).toHaveText('Done', { timeout: 120_000 });
  await window.keyboard.press('Escape');

  // Both colours came through the file, as they were painted.
  const swatches = window.getByTestId('paint-id-colour');
  await expect(swatches).toHaveCount(2, { timeout: 30_000 });
  // A swatch says its colour's name and share, not a hex code a screen reader spells out.
  await expect(window.getByRole('button', { name: /^Select red, \d+% of the map$/u })).toBeVisible();
  await expect(window.getByRole('button', { name: /^Select blue, \d+% of the map$/u })).toBeVisible();

  // Red is the left half of the map, so the fill lands there and nowhere else.
  await window.getByRole('button', { name: /^Select red/u }).click();
  await expect.poll(() => greenAt(0.25), { timeout: 30_000 }).toBeGreaterThan(200);
  expect(await greenAt(0.75)).toBeLessThan(200);

  // The eyedropper finds the other colour from a click on the map.
  await window.getByTestId('paint-eyedropper').click();
  const box = await window.getByTestId('paint-picture').boundingBox();
  if (!box) {
    throw new Error('The map is not on screen.');
  }

  await window.mouse.click(box.x + box.width * 0.75, box.y + box.height / 2);
  // A colour picked joins the selection, so both halves are filled now (#110).
  await expect.poll(() => greenAt(0.75), { timeout: 30_000 }).toBeGreaterThan(200);
  expect(await greenAt(0.25)).toBeGreaterThan(200);

  // A chosen swatch clicked again takes its colour out of the selection.
  await window.getByRole('button', { name: /^Select red/u }).click();
  await expect.poll(() => greenAt(0.25), { timeout: 30_000 }).toBeLessThan(200);
  expect(await greenAt(0.75)).toBeGreaterThan(200);

  // It finds one on the mesh too. Which side faces the camera is the camera's business, so it tries both.
  const canvas = await window.getByRole('application', { name: 'Viewport' }).boundingBox();
  if (!canvas) {
    throw new Error('The viewport is not on screen.');
  }

  // The last try wins, so it stops at the first that lands on red rather than going on to the other side.
  for (const across of [0.42, 0.58]) {
    await window.getByTestId('paint-eyedropper').click();
    await window.mouse.click(canvas.x + canvas.width * across, canvas.y + canvas.height / 2);
    await window.waitForTimeout(500);
    if ((await greenAt(0.25)) > 200) {
      break;
    }
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
  await window.keyboard.press('Backspace');
  await expect(rows).toHaveCount(count - selected - 1);

  // Cmd+Z on a Mac reaches the menu and never the page, so it is the menu item that has to undo.
  await app.evaluate(({ Menu }) => {
    Menu.getApplicationMenu()?.getMenuItemById('undo')?.click();
  });
  await expect(rows).toHaveCount(count - selected);
});

test('a mask runs effects, and they stay in order', async () => {
  await window.getByTestId('paint-add-fill').click();
  const fill = window.locator('[data-testid^=paint-layer-fill]').first();
  await fill.click({ button: 'right' });
  await window.getByTestId('layer-menu-mask-colour').click();

  await pick(window, 'paint-mask-effect-add', /^Invert/u);
  await expect(window.getByTestId('paint-mask-effect-0')).toContainText('Invert');
  await pick(window, 'paint-mask-effect-add', /^Blur/u);
  await expect(window.getByTestId('paint-mask-effect-1')).toContainText('Blur');

  await window.getByRole('button', { name: 'Remove Invert', exact: true }).click();
  await expect(window.getByTestId('paint-mask-effect-0')).toContainText('Blur');
  await expect(window.getByTestId('paint-mask-effect-1')).toHaveCount(0);
});

test('a layer duplicates, copies, cuts and pastes, and each undoes as one step', async () => {
  const undo = () =>
    app.evaluate(({ Menu }) => {
      Menu.getApplicationMenu()?.getMenuItemById('undo')?.click();
    });
  const rows = names();
  const count = await rows.count();
  const paint = window.getByTestId('paint-layer-paint');

  await paint.click({ button: 'right' });
  await window.getByTestId('layer-menu-duplicate').click();
  await expect(rows).toHaveCount(count + 1);
  await expect(window.getByTestId('paint-layer-paint-2')).toBeVisible();
  await undo();
  await expect(rows).toHaveCount(count);

  await paint.click({ button: 'right' });
  await window.getByTestId('layer-menu-cut').click();
  await expect(rows).toHaveCount(count - 1);
  await expect(paint).toHaveCount(0);

  await rows.first().click({ button: 'right' });
  await window.getByTestId('layer-menu-paste').click();
  await expect(rows).toHaveCount(count);
  await expect(window.getByTestId('paint-layer-paint')).toBeVisible();

  // Edit > Paste reaches the stack as well, and a paste is new layers every time.
  await app.evaluate(({ Menu }) => {
    Menu.getApplicationMenu()?.getMenuItemById('paste')?.click();
  });
  await expect(rows).toHaveCount(count + 1);
  await undo();
  await expect(rows).toHaveCount(count);
});

test('a filter layer runs any filter, with the fields its node declares', async () => {
  await window.getByTestId('paint-add-filter').click();
  const filter = window.getByTestId('paint-filter');
  await expect(filter).toBeVisible();
  await expect(window.getByTestId('paint-setting-Gamma')).toBeVisible();

  await filter.click();
  await window.getByRole('option', { name: /^Gradient Map/ }).click();
  await expect(window.getByTestId('paint-setting-Ramp').locator('ptn-gradient-ramp')).toBeVisible();
  await expect(window.getByTestId('paint-setting-Gamma')).toHaveCount(0);

  // A filter that reads one channel takes the grey of what is beneath it, rather than being refused.
  await expect(window.getByTestId('layers-evaluation')).toHaveText(/Updated|Up to date/, { timeout: 30_000 });
  await expect(window.locator('[data-testid^=paint-layer-filter] [data-testid=layer-fault]')).toHaveCount(0);

  await filter.click();
  await window.getByRole('option', { name: /^Mirror/ }).click();
  await expect(window.getByTestId('paint-setting-Axis')).toBeVisible();
});

test('a filter that reads a second image has a slot for it, which a generator fills', async () => {
  test.setTimeout(90_000);
  await window.getByTestId('paint-add-filter').click();
  const filter = window.getByTestId('paint-filter');
  await filter.click();
  await window.getByRole('option', { name: /^Warp/ }).first().click();

  // The subject reads the layers beneath until it is told otherwise; the field reads nothing yet, which a draft says.
  await expect(window.getByTestId('paint-input-Input-source')).toContainText('this channel');
  const field = window.getByTestId('paint-input-Warp-source');
  await expect(field).toBeVisible();
  await expect(window.locator('[data-testid^=paint-layer-filter] [data-testid=layer-fault]')).not.toHaveCount(0, {
    timeout: 30_000
  });

  await pick(window, 'paint-input-Warp-source', 'Generator');
  await expect(window.getByTestId('paint-input-Warp-generator')).toContainText('Clouds');
  await expect(window.getByTestId('layers-evaluation')).toHaveText(/Updated|Up to date/, { timeout: 30_000 });
  await expect(window.locator('[data-testid^=paint-layer-filter] [data-testid=layer-fault]')).toHaveCount(0, {
    timeout: 30_000
  });
  await window.screenshot({ path: join(shots, 'filter-second-image.png') });
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

test('the layer menu and the Add menu work from the keyboard alone', async () => {
  const paint = window.getByTestId('paint-layer-paint');
  const base = window.getByTestId('paint-layer-base');
  await paint.click();
  // ⚠ Keyboard alone: the pointer that clicked the row goes, since the layer menu is now taller than the window and
  // opens over it. Chromium hovers what appears under a resting pointer, and xUI's menu gives that item the focus and
  // shuts the submenu the arrow keys opened.
  await window.mouse.move(0, 0);

  // Shift+F10 is the key every keyboard has: the menu opens on the row, with its first item in hand.
  await paint.press('Shift+F10');
  const menu = window.getByTestId('layer-menu');
  await expect(menu).toBeVisible();
  await expect(window.getByTestId('layer-menu-rename')).toBeFocused();
  await window.keyboard.press('ArrowUp');
  await expect(window.getByTestId('paint-remove')).toBeFocused(); // the last, and red, item
  await window.screenshot({ path: join(shots, 'layer-menu-keys.png') });
  await window.keyboard.press('Escape');
  await expect(menu).toHaveCount(0);
  await expect(paint).toBeFocused();

  // The arrows walk the rows, and choose as they go.
  await window.keyboard.press('ArrowDown');
  await expect(base).toBeFocused();
  await expect(base).toHaveAttribute('aria-selected', 'true');
  await window.keyboard.press('ArrowUp');
  await expect(paint).toBeFocused();

  // A filter from the menu, by its heading, with no mouse.
  await paint.press('Shift+F10');
  await window.getByTestId('layer-menu-effect').focus();
  await window.keyboard.press('ArrowRight');
  await window.getByTestId('layer-menu-filter').focus();
  await window.keyboard.press('ArrowRight');
  await expect(window.getByTestId('layer-menu-filters')).toBeVisible();
  await window.getByTestId('layer-menu-category-Filters').focus();
  await window.keyboard.press('ArrowRight');
  await window.getByTestId('layer-menu-step-Blur').focus();
  await window.keyboard.press('Enter');
  await expect(window.getByTestId('layer-effect-mark')).toHaveCount(1);

  // Add… is the toolbar's way to the same things.
  await window.getByTestId('layers-add').focus();
  await window.keyboard.press('Enter');
  await expect(window.getByTestId('add-paint')).toBeFocused();
  await window.screenshot({ path: join(shots, 'layers-add-menu.png') });
  await window.getByTestId('add-fill').focus();
  await window.keyboard.press('Enter');
  await expect(window.locator('[data-testid^=paint-layer-fill]')).toHaveCount(1);
});

test('every slider shows its number, and a share is a percentage', async () => {
  const paint = window.getByTestId('paint-layer-paint');
  await expect(paint.getByTestId('paint-layer-opacity-readout')).toHaveText('100%');

  await paint.click({ button: 'right' });
  await window.getByTestId('layer-menu-mask-black').click();
  await paint.getByTestId('layer-mask').click();
  await expect(window.getByTestId('paint-mask-value-readout')).toHaveText('0%');
  await expect(window.getByTestId('paint-mask-source')).toContainText('Uniform');
  await window.screenshot({ path: join(shots, 'layer-readouts.png') });
});

test('Export says when it would read files with unsaved changes', async () => {
  await window.getByTestId('paint-add').click();
  await expect(window.getByTestId('active-unsaved')).toBeVisible();
  await window.getByTestId('open-export').click();
  await expect(window.getByTestId('export-unsaved')).toContainText('without its unsaved changes');
  await window.screenshot({ path: join(shots, 'export-unsaved.png') });

  await window.getByTestId('export-save-all').click();
  await expect(window.getByTestId('export-unsaved')).toHaveCount(0, { timeout: 30_000 });
});

test('a folder flattens into one row, and unflattens to its layers', async () => {
  await window.getByTestId('paint-add-group').click();
  const folder = window.locator('[data-testid^=paint-layer-folder]');
  const paint = window.getByTestId('paint-layer-paint');
  await drop(paint, folder, 0.5);
  await expect(paint).toHaveAttribute('aria-level', '2');

  await folder.click({ button: 'right' });
  await window.getByTestId('layer-menu-flatten').click();
  await expect(folder.getByTestId('layer-flattened')).toBeVisible();
  await expect(paint).toHaveCount(0);
  await expect(window.getByTestId('layers-evaluation')).not.toHaveText('Not evaluated', { timeout: 30_000 });
  await expect(folder.getByTestId('layer-fault')).toHaveCount(0);
  await window.screenshot({ path: join(shots, 'layers-flattened.png') });

  await folder.click({ button: 'right' });
  await window.getByTestId('layer-menu-unflatten').click();
  await expect(folder.getByTestId('layer-flattened')).toHaveCount(0);
  await expect(paint).toHaveAttribute('aria-level', '2');

  await window.getByTestId('app-undo').click();
  await expect(folder.getByTestId('layer-flattened')).toBeVisible();
});

test('an anchor point is named, read by a mask in one channel, and lists who reads it', async () => {
  await window.getByTestId('paint-layer-base').click();
  await expect(window.getByTestId('paint-layer-name')).toHaveValue('Base');
  await window.getByTestId('paint-anchor-point').fill('Ground');
  await window.getByTestId('paint-anchor-point').press('Enter');
  await expect(window.getByTestId('paint-anchor-uses')).toContainText('Nothing reads it yet');

  const paint = window.getByTestId('paint-layer-paint');
  await paint.click({ button: 'right' });
  await window.getByTestId('layer-menu-mask-white').click();
  await paint.getByTestId('layer-mask').click();
  await window.getByTestId('paint-mask-source').click();
  await window.getByRole('option', { name: 'Anchor' }).click();
  await window.getByTestId('paint-mask-anchor').click();
  await expect(window.getByRole('option', { name: 'Ground' })).toBeVisible();
  await window.getByRole('option', { name: 'Ground' }).click();
  await expect(window.getByTestId('paint-mask-anchor')).toContainText('Ground');
  await pick(window, 'paint-mask-anchor-channel', 'Roughness');
  await expect(window.getByTestId('paint-mask-anchor-channel')).toContainText('Roughness');

  await window.getByTestId('paint-layer-base').click();
  await expect(window.getByTestId('paint-anchor-uses')).toContainText('Read by Paint · Mask · Roughness');
  await window.screenshot({ path: join(shots, 'layers-anchor-uses.png') });
});

test('a name typed for one layer is never written onto another when a row is pressed', async () => {
  const name = window.getByTestId('paint-layer-name');
  const base = window.getByTestId('paint-layer-base');
  const paint = window.getByTestId('paint-layer-paint');
  await base.click();
  await expect(name).toHaveValue('Base');
  await name.fill('Bottom');

  // Pressing another row may choose it before the field's blur commits, which once wrote "Paint" onto Base. Base keeps
  // its name or takes the one typed for it, depending on which comes first, and never takes Paint's.
  await paint.click();
  await expect(name).toHaveValue('Paint');
  await name.fill('Top');
  await name.press('Enter');
  await expect(paint).toContainText('Top');
  await expect(base).toContainText(/Base|Bottom/);
  await expect(base).not.toContainText('Paint');
  await expect(base).not.toContainText('Top');

  await window.getByTestId('app-undo').click();
  await expect(paint).toContainText('Paint');
});

test('a layer takes a color tag from Properties and from its menu, and a tagged row shows it', async () => {
  const paint = window.getByTestId('paint-layer-paint');
  const base = window.getByTestId('paint-layer-base');
  await paint.click();
  await window.getByTestId('paint-tag-Violet').click();
  await expect(paint.getByTestId('layer-tag')).toHaveAttribute('data-tag', 'Violet');
  await expect(window.getByTestId('paint-tag-Violet')).toHaveAttribute('aria-checked', 'true');
  await expect(window.getByTestId('app-undo')).toHaveAttribute('aria-label', 'Undo Tag layer');

  // From the row's menu, whose submenu opens from the keyboard as the others do.
  await base.click({ button: 'right' });
  await window.getByTestId('layer-menu-tag').focus();
  await window.keyboard.press('ArrowRight');
  await window.getByTestId('layer-menu-tag-Green').click();
  await expect(base.getByTestId('layer-tag')).toHaveAttribute('data-tag', 'Green');
  await window.screenshot({ path: join(shots, 'layers-tags.png') });

  await paint.click();
  await window.getByTestId('paint-tag-None').click();
  await expect(paint.getByTestId('layer-tag')).toHaveCount(0);
});

test('a flattened folder exports on its own, named after it', async () => {
  // Waits on a flatten and an export, each an evaluation.
  test.setTimeout(120_000);
  await window.getByTestId('paint-add-group').click();
  const folder = window.locator('[data-testid^=paint-layer-folder]');
  await drop(window.getByTestId('paint-layer-paint'), folder, 0.5);
  await folder.click({ button: 'right' });
  await window.getByTestId('layer-menu-flatten').click();
  await expect(folder.getByTestId('layer-flattened')).toBeVisible();
  await expect(window.getByTestId('layers-evaluation')).not.toHaveText('Not evaluated', { timeout: 30_000 });

  const out = join(scratch, 'alone');
  await answerOpen(app, out);
  await folder.click({ button: 'right' });
  await window.getByTestId('layer-menu-export').focus();
  await window.keyboard.press('ArrowRight');
  await window.getByTestId('layer-menu-export-alone').click();
  await expect
    .poll(() => (existsSync(out) ? readdirSync(out) : []).some(file => file.endsWith('.Folder.baseColor.png')), {
      timeout: 60_000
    })
    .toBe(true);
});

test('any layer exports on its own, and so does its mask as one map', async () => {
  // Waits on two exports, each an evaluation.
  test.setTimeout(120_000);
  await window.getByTestId('paint-add-fill').click();
  const fill = window.locator('[data-testid^=paint-layer-fill]').first();
  await fill.click({ button: 'right' });
  await window.getByTestId('layer-menu-export').focus();
  await window.keyboard.press('ArrowRight');
  await expect(window.getByTestId('layer-menu-export-alone')).toBeVisible();
  await expect(window.getByTestId('layer-menu-export-mask')).toHaveCount(0);
  await window.keyboard.press('Escape');
  await window.keyboard.press('Escape');
  await fill.click({ button: 'right' });
  await window.getByTestId('layer-menu-mask-white').click();

  const layer = join(scratch, 'layer-alone');
  await answerOpen(app, layer);
  await fill.click({ button: 'right' });
  await window.getByTestId('layer-menu-export').focus();
  await window.keyboard.press('ArrowRight');
  await window.getByTestId('layer-menu-export-alone').click();
  await expect
    .poll(() => (existsSync(layer) ? readdirSync(layer) : []).some(file => file.endsWith('.baseColor.png')), {
      timeout: 60_000
    })
    .toBe(true);

  const mask = join(scratch, 'mask-alone');
  await answerOpen(app, mask);
  await fill.click({ button: 'right' });
  await window.getByTestId('layer-menu-export').focus();
  await window.keyboard.press('ArrowRight');
  await window.getByTestId('layer-menu-export-mask').click();
  await expect
    .poll(() => (existsSync(mask) ? readdirSync(mask) : []), { timeout: 60_000 })
    .toEqual([expect.stringMatching(/\.mask\.png$/)]);
});

test('a picture is picked from the library, the project’s own first, and comes into the project (#4)', async () => {
  await newProject(app, window, 'Barrel');
  await expect(window.getByTestId('paint-layer-paint')).toBeVisible({ timeout: 30_000 });
  // A picture of this project's and one of another's, as an artist or git put them there.
  const pixel = Buffer.from(
    'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAAAAAA6fptVAAAACklEQVR4nGP4DwABAQEAG7buVgAAAABJRU5ErkJggg==',
    'base64'
  );
  for (const [project, name] of [
    ['barrel', 'wood.png'],
    ['crate', 'rust.png']
  ] as const) {
    const folder = join(scratch, 'Game.ptlib', 'projects', project, 'pictures');
    mkdirSync(folder, { recursive: true });
    writeFileSync(join(folder, name), pixel);
  }

  await window.getByTestId('paint-screen-picture-choose').click();
  const dialog = window.getByTestId('pick-file');
  await expect(dialog).toBeVisible();
  await expect(dialog.getByTestId('pick-file-item')).toHaveText(['wood.png', 'rust.png']);
  await expect(dialog).toContainText('This project');
  await expect(dialog.locator('img')).toHaveCount(2);
  await window.screenshot({ path: join(shots, 'pick-file.png') });

  await window.getByTestId('pick-file-search').fill('rust');
  await expect(dialog.getByTestId('pick-file-item')).toHaveText(['rust.png']);
  await dialog.getByTestId('pick-file-item').click();
  await window.getByTestId('pick-file-choose').click();
  await expect(dialog).toHaveCount(0);
  // Another project's picture comes into this one, so the project travels with what it reads.
  await expect(window.getByTestId('paint-screen-picture-name')).toHaveText('rust.png', { timeout: 30_000 });
  expect(existsSync(join(scratch, 'Game.ptlib', 'projects', 'barrel', 'pictures', 'rust.png'))).toBe(true);
});
