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
import { leave, newLibrary, newProject, serverPicture } from './projects';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
const shots = resolve(__dirname, '../../../dist/e2e-results');
const softwareWebGl = process.platform === 'linux' ? ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] : [];

let scratch: string;
let app: ElectronApplication;
let window: Page;

test.beforeEach(async () => {
  test.skip(process.platform === 'win32' && !!process.env['CI'], 'Windows CI has no Vulkan.');
  scratch = mkdtempSync(join(tmpdir(), 'patina-e2e-find-'));
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

/** Clicks the 2D view at a place, as fractions of it. */
async function clickMap(u: number, v: number): Promise<void> {
  const box = await window.getByTestId('paint-picture').boundingBox();
  if (!box) {
    throw new Error('The picture has no box.');
  }

  await window.mouse.click(box.x + box.width * u, box.y + box.height * v);
}

test('Find Layer chooses the layer on top where the map is clicked', async () => {
  test.setTimeout(120_000);
  await newProject(app, window, 'Crate');
  const picture = window.getByTestId('paint-picture');
  await expect(picture).toBeVisible({ timeout: 60_000 });
  const before = await picture.getAttribute('src');
  const box = await picture.boundingBox();
  if (!box) {
    throw new Error('The picture has no box.');
  }

  await window.mouse.move(box.x + box.width * 0.2, box.y + box.height * 0.5);
  await window.mouse.down();
  await window.mouse.move(box.x + box.width * 0.8, box.y + box.height * 0.5, { steps: 12 });
  await window.mouse.up();
  await expect(picture).not.toHaveAttribute('src', before ?? '', { timeout: 30_000 });
  await expect(window.getByTestId('layers-evaluation')).not.toContainText('Evaluating', { timeout: 30_000 });

  const base = window.getByTestId('paint-layer-base');
  const paint = window.getByTestId('paint-layer-paint');
  await base.click();
  await expect(base).toHaveAttribute('aria-selected', 'true');

  await window.getByTestId('layers-find').click();
  await expect(window.getByTestId('layers-find')).toHaveAttribute('aria-pressed', 'true');
  await clickMap(0.5, 0.5);
  await expect(paint).toHaveAttribute('aria-selected', 'true', { timeout: 30_000 });
  await expect(window.getByTestId('layers-find')).toHaveAttribute('aria-pressed', 'false');
  // A click that found something made no stroke.
  await expect(window.getByTestId('app-undo')).toHaveAttribute('aria-label', 'Undo Stroke');

  await window.getByTestId('layers-find').click();
  await clickMap(0.5, 0.9);
  await expect(base).toHaveAttribute('aria-selected', 'true', { timeout: 30_000 });
});

test('a texture set shows alone in the viewport, and the whole model comes back', async () => {
  test.setTimeout(120_000);
  const corners = [
    ...['0 0 0', '1 0 0', '1 1 0', '0 1 0', '1.2 0 0', '2.2 0 0', '2.2 1 0', '1.2 1 0'].map(v => `v ${v}`),
    ...['0 0', '1 0', '1 1', '0 1'].map(t => `vt ${t}`),
    'usemtl metal',
    'f 1/1 2/2 3/3 4/4',
    'usemtl wood',
    'f 5/1 6/2 7/3 8/4'
  ];
  writeFileSync(join(scratch, 'sets.obj'), corners.join('\n'));
  await newProject(app, window, 'Sets', join(scratch, 'sets.obj'));
  await expect(window.getByTestId('texture-sets')).toBeVisible({ timeout: 60_000 });
  const viewport = window.getByRole('application', { name: 'Viewport' });
  await expect(viewport).toBeVisible();
  await expect
    .poll(async () => Number(await viewport.getAttribute('data-frames')), { timeout: 30_000 })
    .toBeGreaterThan(0);

  /**
   * How many pixels of the composited canvas are a quad's pale grey, which the sky never is; or marked orange. The wash
   * is laid over the finished picture after the tone curve (#286), red about 85 over blue on a pale quad; laid on before
   * it, red was only 50 over, and a count that asked for that failed on Linux CI (#274). Unmarked grey is within 5.
   */
  const quads = async (marked = false): Promise<number> => {
    const box = await viewport.boundingBox();
    if (!box) {
      throw new Error('The viewport has no box.');
    }

    return count((await window.screenshot({ clip: box })).toString('base64'), marked);
  };

  /** The same count of a PNG given as base64. */
  const count = (png: string, marked: boolean): Promise<number> =>
    window.evaluate(
      async ({ data, orange }) => {
        const image = new Image();
        image.src = `data:image/png;base64,${data}`;
        await image.decode();
        const surface = new OffscreenCanvas(image.width, image.height);
        const context = surface.getContext('2d');
        if (!context) {
          throw new Error('No 2D context.');
        }

        context.drawImage(image, 0, 0);
        const pixels = context.getImageData(0, 0, image.width, image.height).data;
        let pale = 0;
        for (let i = 0; i < pixels.length; i += 4) {
          const [r, g, b] = [pixels[i] ?? 0, pixels[i + 1] ?? 0, pixels[i + 2] ?? 0];
          pale += (orange ? r > 120 && r > b + 60 : r > 140 && g > 140 && b > 140) ? 1 : 0;
        }

        return pale;
      },
      { data: png, orange: marked }
    );

  await expect.poll(quads, { timeout: 30_000 }).toBeGreaterThan(1_000);
  const whole = await quads();
  await viewport.screenshot({ path: join(shots, 'isolate-1-whole.png') });

  // Pointing at a set marks it on the model, and the mark goes with the pointer (#136).
  // Each step is waited for apart, so a failure says which did not happen: the Server hearing of the mark, a frame
  // drawn since, or the orange in it (#274).
  const unmarked = await quads(true);
  let frames = Number(await viewport.getAttribute('data-frames'));
  await window.getByTestId('texture-set-wood').hover();
  await drawn(viewport, 'highlight', ['wood'], frames);
  try {
    await expect
      .poll(() => quads(true), { timeout: 30_000, message: 'The mark on wood is drawn orange' })
      .toBeGreaterThan(unmarked + 1_000);
  } catch (failure) {
    // Whether the Worker drew the mark and the app lost it, or the Worker never drew it.
    const drawn = await count(await serverPicture(window, join(shots, 'isolate-0-wood-marked-server.png')), true);
    throw new Error(`${String(failure)}\nThe Server's own picture of the viewport has ${drawn} orange pixels.`, {
      cause: failure
    });
  }

  await viewport.screenshot({ path: join(shots, 'isolate-0-wood-marked.png') });
  frames = Number(await viewport.getAttribute('data-frames'));
  await viewport.hover();
  await drawn(viewport, 'highlight', [], frames);
  await expect.poll(() => quads(true), { timeout: 30_000 }).toBeLessThan(unmarked + 200);

  // One of two equal quads alone is about half of what both were.
  const isolate = window.getByTestId('texture-set-isolate-wood');
  frames = Number(await viewport.getAttribute('data-frames'));
  await isolate.click();
  await expect(isolate).toHaveAttribute('aria-pressed', 'true');
  // Off the row, so the count is of the quad and not its mark.
  await viewport.hover();
  await drawn(viewport, 'isolation', ['wood'], frames);
  await expect.poll(() => quads(), { timeout: 30_000 }).toBeLessThan(whole * 0.7);
  expect(await quads()).toBeGreaterThan(whole * 0.3);
  await viewport.screenshot({ path: join(shots, 'isolate-2-wood-alone.png') });

  frames = Number(await viewport.getAttribute('data-frames'));
  await isolate.click();
  await expect(isolate).toHaveAttribute('aria-pressed', 'false');
  // Off the row again: wood's mark takes its quad's blue under the pale grey this counts, now the wash is the orange
  // a person sees (#286).
  await viewport.hover();
  await drawn(viewport, 'isolation', [], frames);
  await expect.poll(quads, { timeout: 30_000 }).toBeGreaterThan(whole * 0.9);
  await viewport.screenshot({ path: join(shots, 'isolate-3-whole-again.png') });

  // The same from a set's own menu.
  await window.getByTestId('texture-set-wood').click({ button: 'right' });
  await expect(window.getByTestId('texture-set-menu-open')).toContainText('Paint wood');
  await window.getByTestId('texture-set-menu-alone').click();
  await expect(isolate).toHaveAttribute('aria-pressed', 'true');
  await window.getByTestId('texture-set-metal').click({ button: 'right' });
  await window.getByTestId('texture-set-menu-all').click();
  await expect(isolate).toHaveAttribute('aria-pressed', 'false');
});

/** Drags across the 2D view, as fractions of it, and waits for the stack to catch up. */
async function stroke(from: [number, number], to: [number, number]): Promise<void> {
  const picture = window.getByTestId('paint-picture');
  const before = await picture.getAttribute('src');
  const box = await picture.boundingBox();
  if (!box) {
    throw new Error('The picture has no box.');
  }

  await window.mouse.move(box.x + box.width * from[0], box.y + box.height * from[1]);
  await window.mouse.down();
  await window.mouse.move(box.x + box.width * to[0], box.y + box.height * to[1], { steps: 12 });
  await window.mouse.up();
  await expect(picture).not.toHaveAttribute('src', before ?? '', { timeout: 30_000 });
  await settled();
}

async function settled(): Promise<void> {
  await expect(window.getByTestId('layers-evaluation')).not.toContainText('Evaluating', { timeout: 30_000 });
  await window.waitForTimeout(1_000);
}

test('an instance in another texture set shows its source and follows an unsaved edit to it', async () => {
  test.setTimeout(180_000);
  const corners = [
    ...['0 0 0', '1 0 0', '1 1 0', '0 1 0', '1.2 0 0', '2.2 0 0', '2.2 1 0', '1.2 1 0'].map(v => `v ${v}`),
    ...['0 0', '1 0', '1 1', '0 1'].map(t => `vt ${t}`),
    'usemtl metal',
    'f 1/1 2/2 3/3 4/4',
    'usemtl wood',
    'f 5/1 6/2 7/3 8/4'
  ];
  writeFileSync(join(scratch, 'sets.obj'), corners.join('\n'));
  await newProject(app, window, 'Sets', join(scratch, 'sets.obj'));
  await expect(window.getByTestId('texture-sets')).toBeVisible({ timeout: 60_000 });
  const picture = window.getByTestId('paint-picture');
  await expect(picture).toBeVisible({ timeout: 60_000 });

  // A stroke on metal's paint layer, copied and saved, so that the copy names it and its canvas is on disk.
  await window.getByTestId('texture-set-metal').click();
  await expect(window.getByTestId('texture-set-metal')).toHaveAttribute('aria-selected', 'true');
  const paint = window.getByTestId('paint-layer-paint');
  await expect(paint).toBeVisible({ timeout: 30_000 });
  await paint.click();
  await stroke([0.1, 0.5], [0.9, 0.5]);
  await paint.click({ button: 'right' });
  await window.getByTestId('layer-menu-copy').click();
  await window.getByTestId('app-save').click();
  await expect(window.getByTestId('app-save')).toBeDisabled({ timeout: 30_000 });

  // Wood shows none of it, until its instance does.
  await window.getByTestId('texture-set-wood').click();
  await expect(window.getByTestId('texture-set-wood')).toHaveAttribute('aria-selected', 'true');
  await expect(window.getByTestId('paint-layer-paint')).toBeVisible({ timeout: 30_000 });
  await settled();
  const bare = await picture.getAttribute('src');
  await window.screenshot({ path: join(shots, 'instance-1-wood-bare.png') });
  await window.getByTestId('paint-layer-paint').click({ button: 'right' });
  await window.getByTestId('layer-menu-paste-instance').click();
  const instance = window.locator('[data-testid^=paint-layer-paint-instance]');
  await expect(instance).toBeVisible({ timeout: 30_000 });
  await expect(picture).not.toHaveAttribute('src', bare ?? '', { timeout: 30_000 });
  await settled();
  const shown = await picture.getAttribute('src');
  // The source is named as metal names it, not by its id (#108).
  await expect(window.getByTestId('paint-instance-source')).toContainText('Stands for Paint in metal');
  await expect(window.getByTestId('app-undo')).toHaveAttribute('aria-label', 'Undo Paste as instances');
  await window.screenshot({ path: join(shots, 'instance-2-wood-instance.png') });

  // A black mask on the source in metal, unsaved, hides it in wood too.
  await window.getByTestId('texture-set-metal').click();
  await expect(window.getByTestId('texture-set-metal')).toHaveAttribute('aria-selected', 'true');
  await expect(paint).toBeVisible({ timeout: 30_000 });
  // The source lists its instance in wood, and Go opens wood on it.
  await paint.click();
  await expect(window.getByTestId('paint-instance-wood')).toContainText('in wood', { timeout: 30_000 });
  await paint.click({ button: 'right' });
  await window.getByTestId('layer-menu-mask-black').click();
  await settled();
  await window.getByTestId('texture-set-wood').click();
  await expect(window.getByTestId('texture-set-wood')).toHaveAttribute('aria-selected', 'true');
  await expect(instance).toBeVisible({ timeout: 30_000 });
  await expect(picture).not.toHaveAttribute('src', shown ?? '', { timeout: 30_000 });
  await settled();
  await window.screenshot({ path: join(shots, 'instance-3-source-masked.png') });
});

test('the 2D view draws the UV layout over the map, says where the pointer is, and fits again', async () => {
  test.setTimeout(120_000);
  const corners = [
    ...['0 0 0', '1 0 0', '1 1 0', '0 1 0'].map(v => `v ${v}`),
    ...['0.1 0.1', '0.6 0.1', '0.6 0.6', '0.1 0.6'].map(t => `vt ${t}`),
    'f 1/1 2/2 3/3 4/4'
  ];
  writeFileSync(join(scratch, 'plate.obj'), corners.join('\n'));
  await newProject(app, window, 'Plate', join(scratch, 'plate.obj'));
  const picture = window.getByTestId('paint-picture');
  await expect(picture).toBeVisible({ timeout: 60_000 });

  const wire = window.getByTestId('paint-uv-wire');
  await wire.click();
  await expect(wire).toHaveAttribute('aria-pressed', 'true');
  const layout = window.getByTestId('paint-uv-layout');
  await expect(layout).toBeVisible({ timeout: 30_000 });
  await expect(layout).toHaveAttribute('src', /^blob:/);
  await window.screenshot({ path: join(shots, 'uv-wire-1-on.png') });

  const box = await picture.boundingBox();
  if (!box) {
    throw new Error('The picture has no box.');
  }

  await window.mouse.move(box.x + box.width * 0.5, box.y + box.height * 0.25);
  await expect(window.getByTestId('paint-under')).toContainText('u 0.50');
  await expect(window.getByTestId('paint-under')).toContainText('v 0.25');

  const fit = window.getByTestId('paint-fit');
  await expect(fit).toBeDisabled();
  await window.mouse.wheel(0, -400);
  await expect(fit).toBeEnabled();
  await fit.click();
  await expect(fit).toBeDisabled();

  await wire.click();
  await expect(layout).toHaveCount(0);
});

/** The Server's address and token, as the app holds them. */
async function server(): Promise<{ baseUrl: string; token: string }> {
  return window.evaluate(() =>
    (
      globalThis as unknown as { patina: { connection(): Promise<{ baseUrl: string; token: string }> } }
    ).patina.connection()
  );
}

/**
 * Waits until the Server draws these materials alone or marks them, none for the whole model or no mark, and frames
 * drawn since have reached the viewport, so a count of its pixels is of what was asked for (#271). The Server draws
 * only when something changes, so the frames counted are those since `frames`, read before the change was asked for.
 */
async function drawn(
  viewport: Locator,
  what: 'isolation' | 'highlight',
  materials: string[],
  frames: number
): Promise<void> {
  const { baseUrl, token } = await server();
  const headers = { Authorization: `Bearer ${token}` };
  const held = async (): Promise<string[]> => {
    const state = (await (await fetch(`${baseUrl}/api/v1/viewport`, { headers })).json()) as Record<
      string,
      { materials: string[] } | null | undefined
    >;
    return state[what]?.materials ?? [];
  };
  await expect
    .poll(held, { timeout: 30_000, message: `The Server's ${what} names ${materials.join(', ') || 'nothing'}` })
    .toEqual(materials);
  await expect
    .poll(async () => Number(await viewport.getAttribute('data-frames')), {
      timeout: 30_000,
      message: `A frame reaches the viewport after the ${what} changed`
    })
    .toBeGreaterThan(frames);
}
