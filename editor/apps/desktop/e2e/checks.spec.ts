import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { mkdtempSync, readFileSync, readdirSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { leave, newLibrary, newProject } from './projects';

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
  rmSync(scratch, { recursive: true, force: true, maxRetries: 20, retryDelay: 250 });
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
  await expect(window.getByTestId('checks-density')).toHaveText(/\d+ per unit/u, { timeout: 120_000 });
  await expect(window.getByTestId('checks-memory')).toHaveText(/ MB with /u);

  // A density far past what the set has is one it falls short of, and the problem shows it on the mesh.
  await window.getByTestId('checks-limits').locator('summary').click();
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
});
