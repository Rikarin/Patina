import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, mkdtempSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
const bench = resolve(__dirname, '../../../../engine/tools/Patina.Bench/bin/Release/net10.0/patina-bench.dll');
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
  await expect(window.getByRole('status', { name: 'Server status' })).toContainText('Worker connected');
});

test.afterEach(async () => {
  await app.close();
  rmSync(scratch, { recursive: true, force: true });
});

test('a bake runs from the panel and its maps show in the Texture pane', async () => {
  const output = join(scratch, 'maps');

  await window.getByRole('tab', { name: 'Bake' }).click();
  await window.getByTestId('bake-high').fill(join(scratch, 'meshes', 'housing_high.glb'));
  await window.getByTestId('bake-low').fill(join(scratch, 'meshes', 'housing_low.glb'));
  await window.getByTestId('bake-output').fill(output);
  await window.getByTestId('bake-start').click();

  await expect(window.getByTestId('bake-state')).toHaveText('done', { timeout: 120_000 });
  await expect(window.getByTestId('bake-percent')).toHaveText('100%');

  // The results are files where they were asked for, whatever the panel shows of them.
  expect(existsSync(join(output, 'housing_low_normal.png'))).toBe(true);
  expect(readdirSync(output)).toContain('housing_low.bake.json');

  await window.getByTestId('bake-result-normal').click();
  const picture = window.getByTestId('texture-bake');
  await expect(picture).toBeVisible();
  await expect(picture).toHaveJSProperty('complete', true);
  expect(await picture.evaluate(image => (image as HTMLImageElement).naturalWidth)).toBe(2048);
  await expect(window.getByTestId('texture-size')).toContainText('normal, 2048 × 2048');

  mkdirSync(shots, { recursive: true });
  await window.screenshot({ path: join(shots, 'bake-panel.png') });

  // The same bake again runs nothing.
  await window.getByTestId('bake-start').click();
  await expect(window.getByTestId('bake-cached')).toBeVisible();
});

test('a bake can be judged on its low mesh in the viewport, with a cage and a skew map through the panel', async () => {
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

  await window.getByRole('tab', { name: 'Bake' }).click();
  await window.getByTestId('bake-high').fill(join(scratch, 'meshes', 'housing_high.glb'));
  await window.getByTestId('bake-low').fill(low);
  await window.getByTestId('bake-cage').fill(low);
  await window.getByTestId('bake-skewMap').fill(skew);
  await window.getByTestId('bake-output').fill(output);
  await window.getByTestId('bake-start').click();
  await expect(window.getByTestId('bake-state')).toHaveText('done', { timeout: 120_000 });

  const frames = () =>
    window
      .getByRole('img', { name: 'Viewport' })
      .getAttribute('data-frames')
      .then(count => Number(count));
  const before = await frames();

  await window.getByTestId('bake-show-on-mesh').click();

  await expect(window.getByTestId('viewport-mesh')).toHaveText('housing_low.glb');
  await expect.poll(frames, { timeout: 30_000 }).toBeGreaterThan(before);
  mkdirSync(shots, { recursive: true });
  await window.screenshot({ path: join(shots, 'bake-on-mesh.png') });
});

test('the panel says why the Server refused a bake', async () => {
  await window.getByRole('tab', { name: 'Bake' }).click();
  await window.getByTestId('bake-high').fill('/nowhere/high.glb');
  await window.getByTestId('bake-low').fill('/nowhere/low.glb');
  await window.getByTestId('bake-output').fill(scratch);
  await window.getByTestId('bake-start').click();

  await expect(window.getByTestId('bake-refused')).toContainText('/nowhere/high.glb');
});
