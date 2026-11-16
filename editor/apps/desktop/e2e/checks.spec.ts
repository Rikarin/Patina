import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { mkdtempSync, readFileSync, readdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, relative, resolve } from 'node:path';
import { leave, newLibrary, newProject, pick, removeScratch } from './projects';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
const mesh = resolve(__dirname, '../../../../content/fixtures/viewport-test.glb');
const shots = resolve(__dirname, '../../../dist/e2e-results');

/** Linux CI runs under Xvfb with no GPU, and Chromium no longer falls back to software WebGL unless told to. */
const softwareWebGl = process.platform === 'linux' ? ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] : [];

let scratch: string;
let app: ElectronApplication;
let window: Page;

test.beforeEach(async () => {
  test.skip(process.platform === 'win32' && !!process.env['CI'], 'Windows CI has no Vulkan.');
  scratch = mkdtempSync(join(tmpdir(), 'patina-e2e-'));
  app = await electron.launch({
    args: [...softwareWebGl, main],
    env: { ...process.env, PATINA_USER_DATA: join(scratch, 'user-data') }
  });
  window = await app.firstWindow();
});

test.afterEach(async () => {
  await leave(app);
  removeScratch(scratch);
});

/** How many pixels of the viewport's picture are clearly blue, as the window composites them. */
async function blue(): Promise<number> {
  const png = (await window.getByRole('application', { name: 'Viewport' }).screenshot({ scale: 'css' })).toString(
    'base64'
  );
  return window.evaluate(async data => {
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
    let count = 0;
    for (let i = 0; i < pixels.length; i += 4) {
      const [r, g, b] = [pixels[i] ?? 0, pixels[i + 1] ?? 0, pixels[i + 2] ?? 0];
      count += b > r + 80 && b > g + 40 ? 1 : 0;
    }

    return count;
  }, png);
}

/** The project's file, which keeps the limits its sets are held to. */
function projectFile(): string {
  const projects = join(scratch, 'Game.ptlib', 'projects');
  const found = readdirSync(projects, { recursive: true, encoding: 'utf8' }).find(path =>
    path.endsWith('project.yaml')
  );
  if (!found) {
    throw new Error('The library has no project.');
  }

  return join(projects, found);
}

/**
 * Checks measures the set in front against the project (#159): its texel density and memory, and a problem for a limit
 * it breaks. A limit set in the pane is kept in the project's file, and a problem goes to the view that shows it.
 */
test('checks measure the set in front, keep the project’s limits, and go to a problem', async () => {
  // A check reads every map of the set once, and a CI runner is slower than a laptop at that.
  test.setTimeout(240_000);
  await newLibrary(app, window, join(scratch, 'Game.ptlib'));
  await newProject(app, window, 'Checked', mesh);

  await window.getByRole('tab', { name: 'Checks' }).click();
  await window.getByTestId('checks-run').click();
  await expect(window.getByTestId('checks-density')).toHaveText(/\d+ per metre/u, { timeout: 120_000 });
  await expect(window.getByTestId('checks-memory')).toHaveText(/ MB with /u);

  // The same layout on a mesh in centimetres covers a hundredth of the metres, so it has a hundred times the texels
  // per metre (#307). A glTF is in metres; the project can say otherwise, and the check measures through it.
  const perMetre = async () => Number(/\d+/u.exec((await window.getByTestId('checks-density').textContent()) ?? '')?.[0]);
  const inMetres = await perMetre();
  await setUnits('Centimetres');
  await expect.poll(() => readFileSync(projectFile(), 'utf8'), { timeout: 10_000 }).toContain('metresPerUnit: 0.01');
  await window.getByTestId('checks-run').click();
  await expect.poll(perMetre, { timeout: 120_000 }).toBeGreaterThan(inMetres * 99);
  expect(await perMetre()).toBeLessThan(inMetres * 101);
  await setUnits('As the file says');
  await expect.poll(() => readFileSync(projectFile(), 'utf8'), { timeout: 10_000 }).not.toContain('metresPerUnit');
  await window.getByTestId('checks-run').click();
  await expect.poll(perMetre, { timeout: 120_000 }).toBe(inMetres);

  // Each file the export writes is listed with its size and the memory it takes.
  await window.getByTestId('checks-files').locator('[data-disclosure-toggle]').click();
  await expect(window.getByTestId('checks-file').first()).toHaveText(/\.\w+\s*\d+ × \d+\s*[\d.]+ MB/u);

  // A density far past what the set has is one it falls short of, and the problem shows it on the mesh.
  await window.getByTestId('checks-limits').locator('[data-disclosure-toggle]').click();
  await window.getByTestId('checks-limit-texelDensity').fill('1000000');
  await window.getByTestId('checks-limit-texelDensity').press('Enter');
  await window.getByTestId('checks-limit-texelDensity').blur();
  const low = window.locator('[data-testid="checks-problem"][data-code="density-low"]');
  await expect(low).toBeVisible({ timeout: 120_000 });
  await expect(low).toContainText('short of the 1000000');
  await expect
    .poll(() => readFileSync(projectFile(), 'utf8'), { timeout: 10_000 })
    .toMatch(/checks:[\s\S]*texelDensity: 1000000/u);

  // Far short of the density asked for is blue all over the model.
  const before = await blue();
  await low.click();
  await expect(window.getByTestId('viewport-view')).toContainText('Texel density');
  await expect.poll(blue, { timeout: 30_000 }).toBeGreaterThan(before + 2000);
  await window.screenshot({ path: join(shots, 'checks.png'), scale: 'css' });

  // One platform keeps limits of its own beside the project's: a largest side of 16 texels for Godot is one the set
  // breaks when it is checked for Godot.
  await window.getByTestId('checks-platform').click();
  await window.getByRole('option', { name: 'Godot' }).click();
  await expect(window.getByTestId('checks-limits').locator('[data-disclosure-toggle]')).toHaveText('Limits for Godot');
  await window.getByTestId('checks-limit-maxSize').fill('16');
  await window.getByTestId('checks-limit-maxSize').press('Enter');
  await window.getByTestId('checks-limit-maxSize').blur();
  const large = window.locator('[data-testid="checks-problem"][data-code="texture-too-large"]');
  await expect(large).toBeVisible({ timeout: 120_000 });
  await expect
    .poll(() => readFileSync(projectFile(), 'utf8'), { timeout: 10_000 })
    .toMatch(/platforms:[\s\S]*maxSize: 16/u);

  // Every set of the mesh at once, each held to the same limit, and measured where it is open.
  await window.getByRole('radio', { name: 'All sets' }).click();
  await window.getByTestId('checks-run').click();
  const sets = window.getByTestId('checks-set');
  await expect(sets).toHaveCount(2, { timeout: 120_000 });
  await expect(window.getByTestId('checks-project-memory')).toHaveText(/ MB for 2 sets with Godot/u);
  await expect(sets.locator('[data-code="texture-too-large"]')).toHaveCount(2);
  await expect(sets.filter({ hasText: 'Gold' }).getByTestId('checks-unmeasured')).toHaveCount(0);

  // A layered material nobody has open is judged from its file, until the check is asked to measure every set: then
  // the engine opens it for the GPU to measure, and none is left unmeasured (#300).
  await addLayeredMaterial('Trim');
  await window.getByTestId('checks-run').click();
  await expect(sets).toHaveCount(3, { timeout: 120_000 });
  const trim = window.locator('[data-testid="checks-set"][data-document$="/trim.vxlayers"]');
  await expect(trim.getByTestId('checks-unmeasured')).toHaveCount(1);
  await window.getByTestId('checks-measure-all').getByRole('checkbox').click();
  await expect(window.getByTestId('checks-measure-all').getByRole('checkbox')).toBeChecked();
  await window.getByTestId('checks-run').click();
  await expect(trim.getByTestId('checks-unmeasured')).toHaveCount(0, { timeout: 180_000 });
  await expect(sets).toHaveCount(3);
  await expect(window.getByTestId('checks-unmeasured')).toHaveCount(0);
});

/** Says in Project Settings what unit the project's mesh is in, and applies it. */
async function setUnits(name: string): Promise<void> {
  await app.evaluate(({ BrowserWindow }) => {
    BrowserWindow.getAllWindows()[0]?.webContents.send('patina:command', 'projectSettings');
  });
  await pick(window, 'project-settings-units', name);
  await window.getByTestId('project-settings-apply').click();
  await expect(window.getByTestId('project-settings')).toHaveCount(0);
}

/** Adds a layered material to the project through the engine's API, as another client would, without opening it. */
async function addLayeredMaterial(name: string): Promise<void> {
  const { baseUrl, token } = await window.evaluate(() =>
    (
      globalThis as unknown as { patina: { connection(): Promise<{ baseUrl: string; token: string }> } }
    ).patina.connection()
  );
  const project = relative(join(scratch, 'Game.ptlib', 'projects'), dirname(projectFile())).replaceAll('\\', '/');
  const response = await fetch(`${baseUrl}/api/v1/library/project/documents`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
    body: JSON.stringify({ project, kind: 'layered-material', name, resolution: 256 })
  });
  if (!response.ok) {
    throw new Error(`The engine added no ${name}: ${response.status} ${await response.text()}`);
  }
}
