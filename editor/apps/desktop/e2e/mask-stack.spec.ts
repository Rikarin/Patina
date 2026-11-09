import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { mkdtempSync, rmSync } from 'node:fs';
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
  scratch = mkdtempSync(join(tmpdir(), 'patina-e2e-mask-stack-'));
  app = await electron.launch({
    args: [...softwareWebGl, main],
    env: { ...process.env, PATINA_USER_DATA: join(scratch, 'user-data'), PATINA_LIBRARY: '' }
  });
  window = await app.firstWindow();
  await newLibrary(app, window, join(scratch, 'Game.ptlib'));
  await newProject(app, window, 'Crate');
  await expect(window.getByTestId('paint-picture')).toBeVisible({ timeout: 60_000 });
});

test.afterEach(async () => {
  await leave(app);
  rmSync(scratch, { recursive: true, force: true, maxRetries: 20, retryDelay: 250 });
});

/** Drags across the 2D view from one place to another, as fractions of it. */
async function drag(from: [number, number], to: [number, number]): Promise<void> {
  const box = await window.getByTestId('paint-picture').boundingBox();
  if (!box) {
    throw new Error('The picture has no box.');
  }

  await window.mouse.move(box.x + box.width * from[0], box.y + box.height * from[1]);
  await window.mouse.down();
  await window.mouse.move(box.x + box.width * to[0], box.y + box.height * to[1], { steps: 12 });
  await window.mouse.up();
}

/** Waits until the 2D view shows something other than it did, and answers what it shows now. */
async function changed(before: string | null): Promise<string | null> {
  const picture = window.getByTestId('paint-picture');
  await expect(picture).not.toHaveAttribute('src', before ?? '', { timeout: 30_000 });
  await expect(window.getByTestId('paint-undo')).toBeEnabled();
  // A drag's picture arrives in parts, so what is on screen is read once the stack has caught up with the edit.
  await expect(window.getByTestId('layers-evaluation')).not.toContainText('Evaluating', { timeout: 30_000 });
  await window.waitForTimeout(1_000);
  return picture.getAttribute('src');
}

/** The mean of the 2D view's red, green and blue, 0 to 255, which tells two different pictures apart. */
async function mean(): Promise<number> {
  return window.getByTestId('paint-picture').evaluate(async (element: HTMLImageElement) => {
    await element.decode();
    const surface = new OffscreenCanvas(element.naturalWidth, element.naturalHeight);
    const context = surface.getContext('2d');
    if (!context) {
      throw new Error('No 2D context.');
    }

    context.drawImage(element, 0, 0);
    const pixels = context.getImageData(0, 0, element.naturalWidth, element.naturalHeight).data;
    let sum = 0;
    for (let i = 0; i < pixels.length; i += 4) {
      sum += (pixels[i] ?? 0) + (pixels[i + 1] ?? 0) + (pixels[i + 2] ?? 0);
    }

    return sum / ((pixels.length / 4) * 3);
  });
}

test('a mask opens as a stack of its own, takes paint over its base, and goes back to the layers', async () => {
  // Bigger setup than most, and every step waits on an evaluation.
  test.setTimeout(180_000);
  const picture = window.getByTestId('paint-picture');
  const paint = window.getByTestId('paint-layer-paint');

  // A band of colour on the paint layer, then a black mask that hides all of it.
  await paint.click();
  let shown = await picture.getAttribute('src');
  await drag([0.1, 0.5], [0.9, 0.5]);
  shown = await changed(shown);

  await paint.click({ button: 'right' });
  await window.getByTestId('layer-menu-mask-black').click();
  shown = await changed(shown);
  await window.screenshot({ path: join(shots, 'mask-stack-1-black-mask.png') });

  // The mask as a stack: its base is there, and the layers are not.
  await paint.click({ button: 'right' });
  await window.getByTestId('layer-menu-mask-open').click();
  await expect(window.getByTestId('mask-stack')).toBeVisible();
  await expect(window.getByTestId('mask-stack-title')).toHaveText('Paint · Mask');
  await expect(window.getByTestId('mask-stack-base')).toContainText('Uniform');
  await expect(window.getByTestId('paint-layers')).toHaveCount(0);

  // A paint entry over the base, chosen as it arrives, and a stroke on it shows the band back where it goes.
  await window.getByTestId('mask-stack-add').click();
  await window.getByTestId('mask-stack-add-Paint').click();
  const entry = window.getByTestId('mask-layer-0');
  await expect(entry).toBeVisible();
  await expect(entry).toHaveAttribute('aria-current', 'true');
  await expect(window.getByTestId('paint-mask-layer-paint')).toBeVisible();
  await expect(window.getByTestId('paint-undo')).toHaveAttribute('aria-label', 'Undo Add Paint to mask');

  await drag([0.5, 0.1], [0.5, 0.9]);
  shown = await changed(shown);
  await window.screenshot({ path: join(shots, 'mask-stack-2-painted-entry.png') });

  // Hiding the entry hides what it showed, and the base is still black.
  await entry.getByTestId('mask-layer-eye').click();
  await changed(shown);
  await expect(entry).toHaveClass(/opacity-50/);
  await window.screenshot({ path: join(shots, 'mask-stack-3-entry-hidden.png') });

  // Back to the layers, with the layer still chosen and its mask in front.
  await window.getByTestId('mask-stack-back').click();
  await expect(window.getByTestId('paint-layers')).toBeVisible();
  await expect(paint.getByTestId('layer-mask')).toHaveClass(/outline/);

  // A filter on the mask keeps a slot for each image it reads but the mask, in the mask's own stack too (#109, #113).
  // Submenus open from the keyboard, as the layers spec walks them.
  await paint.click({ button: 'right' });
  await window.getByTestId('layer-menu-filter').focus();
  await window.keyboard.press('ArrowRight');
  await expect(window.getByTestId('layer-menu-filters')).toHaveAttribute('aria-label', 'Filters for the mask');
  await window.getByTestId('layer-menu-category-Filters').focus();
  await window.keyboard.press('ArrowRight');
  await window.getByTestId('layer-menu-step-Warp').focus();
  await window.keyboard.press('Enter');
  await paint.click({ button: 'right' });
  await window.getByTestId('layer-menu-mask-open').click();
  await expect(window.getByTestId('mask-stack-effect')).toContainText('Warp');
  await expect(window.getByTestId('paint-effect-0-input-Warp-source')).toBeVisible();
  await expect(window.getByTestId('paint-effect-0-input-Input')).toHaveCount(0);
});

test('a mask switches off and on from its own menu and Shift-click, and pastes into another layer', async () => {
  // Every step waits on an evaluation.
  test.setTimeout(180_000);
  const picture = window.getByTestId('paint-picture');
  const paint = window.getByTestId('paint-layer-paint');

  // A band on the paint layer, hidden by a black mask.
  await paint.click();
  const bare = await picture.getAttribute('src');
  await drag([0.1, 0.5], [0.9, 0.5]);
  const band = await changed(bare);
  const withBand = await mean();
  await paint.click({ button: 'right' });
  await window.getByTestId('layer-menu-mask-black').click();
  const hidden = await changed(band);
  const withoutBand = await mean();
  expect(Math.abs(withBand - withoutBand)).toBeGreaterThan(1);

  // Switched off from the mask's own menu, the band is back and the mask is still there, struck through.
  const mask = paint.getByTestId('layer-mask');
  await mask.click({ button: 'right' });
  await expect(window.getByTestId('layer-menu')).toHaveAttribute('aria-label', 'Mask');
  await window.getByTestId('mask-menu-switch').click();
  let shown = await changed(hidden);
  expect(await mean()).toBeCloseTo(withBand, 0);
  await expect(mask).toHaveAttribute('data-off', '');
  await window.screenshot({ path: join(shots, 'mask-switch-1-off.png') });

  // Shift-click switches it on again.
  await mask.click({ modifiers: ['Shift'] });
  shown = await changed(shown);
  expect(await mean()).toBeCloseTo(withoutBand, 0);
  await expect(mask).not.toHaveAttribute('data-off', '');

  // Copied from the mask's menu and pasted into the base's: the base takes the same black mask, as its own.
  await mask.click({ button: 'right' });
  await window.getByTestId('mask-menu-copy').click();
  const base = window.getByTestId('paint-layer-base');
  await base.click({ button: 'right' });
  await window.getByTestId('layer-menu-paste-mask').click();
  await changed(shown);
  await expect(base.getByTestId('layer-mask')).toHaveAttribute('title', /^Uniform mask\./);
  await expect(window.getByTestId('paint-undo')).toHaveAttribute('aria-label', 'Undo Paste mask');
  await window.screenshot({ path: join(shots, 'mask-switch-2-pasted.png') });
});
