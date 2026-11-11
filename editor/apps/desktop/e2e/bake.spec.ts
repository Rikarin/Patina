import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { answerOpen, leave, newLibrary, newProject } from './projects';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
const bench = resolve(__dirname, '../../../../engine/tools/Patina.Bench/bin/Release/net11.0/patina-bench.dll');
const shots = resolve(__dirname, '../../../dist/e2e-results');

/** Linux CI runs under Xvfb with no GPU, and Chromium no longer falls back to software WebGL unless told to. */
const softwareWebGl = process.platform === 'linux' ? ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] : [];

let scratch: string;
let app: ElectronApplication;
let window: Page;

test.beforeEach(async () => {
  scratch = mkdtempSync(join(tmpdir(), 'patina-e2e-'));
  execFileSync('dotnet', [bench, 'bake-pair', join(scratch, 'meshes'), '0.02']);
  app = await electron.launch({
    args: [...softwareWebGl, main],
    env: { ...process.env, PATINA_USER_DATA: join(scratch, 'user-data') }
  });
  window = await app.firstWindow();
  await expect(window.getByRole('status', { name: 'Server status' })).toContainText('GPU ready');
  await newLibrary(app, window, join(scratch, 'Game.ptlib'));
  await newProject(app, window, 'Housing', join(scratch, 'meshes', 'housing_low.glb'));
});

test.afterEach(async () => {
  await leave(app);
  rmSync(scratch, { recursive: true, force: true, maxRetries: 20, retryDelay: 250 });
});

/** Answers the system's file dialog for a field of the Bake dialog. */
async function choose(field: string, path: string): Promise<void> {
  await answerOpen(app, path);
  await window.getByTestId(`bake-choose-${field}`).click();
}

test('a bake runs from the panel and its maps show in the Texture pane', async () => {
  // A real bake on a CI runner takes minutes, and the test's own limit would cut it off first.
  test.setTimeout(300_000);
  const output = join(scratch, 'maps');

  await window.getByTestId('open-bake').click();
  await choose('high', join(scratch, 'meshes', 'housing_high.glb'));
  await choose('output', output);
  await window.getByTestId('bake-start').click();

  await expect(window.getByTestId('bake-state')).toHaveText('Done', { timeout: 240_000 });
  await expect(window.getByTestId('bake-percent')).toHaveText('100%');

  // The results are files where they were asked for, whatever the panel shows of them.
  expect(existsSync(join(output, 'housing_low_normal.png'))).toBe(true);
  expect(readdirSync(output)).toContain('housing_low.bake.json');

  await window.getByTestId('bake-result-normal').click();
  const picture = window.getByTestId('texture-bake');
  await expect(picture).toBeVisible();
  await expect(picture).toHaveJSProperty('complete', true);
  expect(await picture.evaluate(image => (image as HTMLImageElement).naturalWidth)).toBe(2048);
  await expect(window.getByTestId('texture-size')).toContainText('Normal, 2048 × 2048');

  mkdirSync(shots, { recursive: true });
  await window.screenshot({ path: join(shots, 'bake-panel.png') });

  // The same bake again runs nothing.
  await window.getByTestId('open-bake').click();
  await window.getByTestId('bake-start').click();
  await expect(window.getByTestId('bake-cached')).toBeVisible();
});

test('a bake can be judged on its low mesh in the viewport, with a cage and a skew map through the panel', async () => {
  // The same bake as the one above, every map the project's masks read at 2048, so the same limits: on CI's
  // software Vulkan runner it ran out of 120 s twice while the one above, given minutes, did not.
  test.setTimeout(300_000);
  const output = join(scratch, 'maps');
  const low = join(scratch, 'meshes', 'housing_low.glb');
  const skew = join(scratch, 'skew.png');

  // A grey picture: one white pixel is an image, and the bake only asks that it be square.
  writeFileSync(
    skew,
    Buffer.from(
      'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAAAAAA6fptVAAAACklEQVR4nGP4DwABAQEAG7buVgAAAABJRU5ErkJggg==',
      'base64'
    )
  );

  await window.getByTestId('open-bake').click();
  await choose('high', join(scratch, 'meshes', 'housing_high.glb'));
  await choose('cage', low);
  await choose('skewMap', skew);
  await choose('output', output);
  await window.getByTestId('bake-start').click();
  await expect(window.getByTestId('bake-state')).toHaveText('Done', { timeout: 240_000 });

  const frames = () =>
    window
      .getByRole('application', { name: 'Viewport' })
      .getAttribute('data-frames')
      .then(count => Number(count));
  const before = await frames();

  await window.getByTestId('bake-show-on-mesh').click();

  await expect(window.getByTestId('viewport-mesh')).toHaveText('housing_low.glb');
  await expect.poll(frames, { timeout: 30_000 }).toBeGreaterThan(before);
  mkdirSync(shots, { recursive: true });
  await window.screenshot({ path: join(shots, 'bake-on-mesh.png') });
});

test('the bake settings are a section per map and stay in the project', async () => {
  await window.getByTestId('open-bake').click();
  await expect(window.getByTestId('bake-low')).toHaveText('housing_low.glb');

  // A map can be turned off, the height included, and each has its own settings.
  await window.getByTestId('bake-map-height').click();
  await window.getByTestId('bake-section-ao').click();
  await expect(window.getByTestId('bake-section-title')).toHaveText('Ambient Occlusion');
  const rays = window.getByTestId('bake-ao-rays').getByRole('spinbutton');
  await rays.fill('128');
  await rays.press('Enter');
  await window.getByTestId('bake-ao-ground').click();
  mkdirSync(shots, { recursive: true });
  await window.screenshot({ path: join(shots, 'bake-sections.png') });

  // The project's own file has them a moment later, so a restart or another client starts from them. Edits further
  // apart than the dialog's save delay are written one at a time, so it waits for the last of them, not the first.
  const projects = join(scratch, 'Game.ptlib', 'projects');
  const project = join(
    projects,
    readdirSync(projects).find(name => existsSync(join(projects, name, 'project.yaml'))) ?? '',
    'project.yaml'
  );
  await expect
    .poll(() => (existsSync(project) ? readFileSync(project, 'utf8') : ''), { timeout: 10_000 })
    .toContain('groundPlane: true');
  const kept = readFileSync(project, 'utf8');
  expect(kept).toContain('rays: 128');
  expect(kept).not.toMatch(/maps:\n(\s+- [a-z]+\n)*\s+- height\n/u);

  await window.keyboard.press('Escape');
  await window.getByTestId('open-bake').click();
  await expect(window.getByTestId('bake-map-height')).not.toBeChecked();
  await window.getByTestId('bake-section-ao').click();
  await expect(window.getByTestId('bake-ao-rays').getByRole('spinbutton')).toHaveValue('128');

  // A minimum occluder distance at the occluder distance would bake white: the field says so, and nothing bakes (#228).
  const distance = await window.getByTestId('bake-ao-distance').getByRole('spinbutton').inputValue();
  const minimum = window.getByTestId('bake-ao-minimum').getByRole('spinbutton');
  await minimum.fill(distance);
  await minimum.press('Enter');
  await expect(window.getByTestId('bake-ao-minimum-problem')).toContainText('less than the occluder distance');
  await expect(window.getByTestId('bake-start')).toBeDisabled();
  await window.screenshot({ path: join(shots, 'bake-ao-minimum.png') });
  await minimum.fill('0');
  await minimum.press('Enter');
  await expect(window.getByTestId('bake-ao-minimum-problem')).toHaveCount(0);
  await expect(window.getByTestId('bake-start')).toBeEnabled();
  await window.getByTestId('bake-section-common').click();
  await window.screenshot({ path: join(shots, 'bake-common.png') });
});

test('a bake shows its setup in the viewport before it runs, and the maps that need no high mesh have their settings', async () => {
  // The Worker reads both meshes before the first frame with the setup on it, which on CI's software Vulkan is slow.
  test.setTimeout(180_000);
  await window.getByTestId('open-bake').click();
  await choose('high', join(scratch, 'meshes', 'housing_high.glb'));
  await window.getByTestId('bake-match').click();

  const frames = () =>
    window
      .getByRole('application', { name: 'Viewport' })
      .getAttribute('data-frames')
      .then(count => Number(count));
  const before = await frames();
  await window.getByTestId('bake-preview').click();

  // The key names each part on both sides: the housing's low and high parts share a name once their endings are gone.
  const parts = window.getByTestId('bake-preview-parts');
  await expect(parts).toBeVisible({ timeout: 120_000 });
  await expect(parts).toContainText('low,');
  await expect.poll(frames, { timeout: 30_000 }).toBeGreaterThan(before);
  mkdirSync(shots, { recursive: true });
  await window.screenshot({ path: join(shots, 'bake-preview.png') });

  // Off takes it off the viewport for every client.
  await window.getByTestId('bake-preview').click();
  await expect(parts).toHaveCount(0);

  await window.getByTestId('bake-section-normal').click();
  await window.getByTestId('bake-round').click();
  await expect(window.getByTestId('bake-round-width')).toBeVisible();
  await window.getByTestId('bake-section-colour').click();
  await expect(window.getByTestId('bake-colour-source')).toBeVisible();
  await window.screenshot({ path: join(shots, 'bake-colour.png') });
});

test('the panel says why the Server refused a high poly mesh', async () => {
  await window.getByTestId('open-bake').click();
  await choose('high', '/nowhere/high.glb');

  await expect(window.getByTestId('app-failure')).toContainText('/nowhere/high.glb');
});

test('a bake goes on reporting once its dialog closes: the status bar says how far, and Activity keeps how it ended', async () => {
  test.setTimeout(300_000);
  await window.getByTestId('open-bake').click();
  await choose('high', join(scratch, 'meshes', 'housing_high.glb'));
  await choose('output', join(scratch, 'maps'));
  await window.getByTestId('bake-start').click();
  await window.keyboard.press('Escape');

  // The dialog is gone, and the work is still in sight (#192).
  await expect(window.getByTestId('activity-doing')).toContainText('Bake of', { timeout: 30_000 });
  await window.getByTestId('activity-readout').click();
  const panel = window.getByTestId('activity-panel');
  await expect(panel.getByTestId('activity-job')).toHaveCount(1);
  await expect(panel.getByTestId('activity-job')).toHaveAttribute('data-state', 'done', { timeout: 240_000 });
  await expect(panel.getByTestId('activity-notice').first()).toContainText(/Bake of .+ finished\./u);
  await expect(window.getByTestId('activity-doing')).toHaveCount(0);
  mkdirSync(shots, { recursive: true });
  await window.screenshot({ path: join(shots, 'activity.png') });

  // A refusal is kept there too, after its bar is dismissed.
  await window.getByTestId('activity-close').click();
  await window.getByTestId('open-bake').click();
  await choose('high', '/nowhere/high.glb');
  await expect(window.getByTestId('app-failure')).toContainText('/nowhere/high.glb');
  await window.keyboard.press('Escape');
  await window.getByTestId('app-failure-dismiss').click();
  await expect(window.getByTestId('activity-unread')).toBeVisible();
  await window.getByTestId('activity-readout').click();
  await expect(panel.getByTestId('activity-notice').first()).toContainText('/nowhere/high.glb');
  await expect(window.getByTestId('activity-unread')).toHaveCount(0);
});
