import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { crc32, deflateSync } from 'node:zlib';
import {
  answerOpen,
  chooseFromComputer,
  chooseInLibrary,
  leave,
  newLibrary,
  newProject,
  pick,
  removeScratch,
  showDocumentSettings
} from './projects';

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
  await expect(window.getByRole('status', { name: 'Engine status' })).toContainText('GPU ready');
  await newLibrary(app, window, join(scratch, 'Game.ptlib'));
  await newProject(app, window, 'Housing', join(scratch, 'meshes', 'housing_low.glb'));
});

test.afterEach(async () => {
  await leave(app);
  removeScratch(scratch);
});

/** Answers the system's file dialog for a field of the Bake dialog. */
async function choose(field: string, path: string): Promise<void> {
  await answerOpen(app, path);
  await chooseFromComputer(app, window, `bake-choose-${field}`);
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
  // The cage is the project's own mesh, picked from the library rather than from the disk (#4).
  await chooseInLibrary(window, 'bake-choose-cage', 'housing_low.glb');
  await expect(window.getByTestId('bake-cage')).toHaveValue('housing_low.glb');
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

/** A one-pixel PNG of 8-bit red, green and blue. */
function pixelPng(red: number, green: number, blue: number): Buffer {
  const chunk = (type: string, data: Buffer): Buffer => {
    const body = Buffer.concat([Buffer.from(type, 'latin1'), data]);
    const length = Buffer.alloc(4);
    length.writeUInt32BE(data.length);
    const check = Buffer.alloc(4);
    check.writeUInt32BE(crc32(body));
    return Buffer.concat([length, body, check]);
  };
  const header = Buffer.alloc(13);
  header.writeUInt32BE(1, 0);
  header.writeUInt32BE(1, 4);
  header[8] = 8;
  header[9] = 2;
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', header),
    chunk('IDAT', deflateSync(Buffer.from([0, red, green, blue]))),
    chunk('IEND', Buffer.alloc(0))
  ]);
}

/** The lines of a block of a YAML file: the line that names it and every line indented under it. */
function block(yaml: string, name: string): string {
  const lines = yaml.split(/\r?\n/u);
  const start = lines.findIndex(line => line.trimStart() === `${name}:`);
  if (start < 0) {
    return '';
  }

  const indent = (line: string) => line.length - line.trimStart().length;
  const end = lines.findIndex(
    (line, at) => at > start && line.trim() !== '' && indent(line) <= indent(lines[start] ?? '')
  );
  return lines.slice(start, end < 0 ? undefined : end).join('\n');
}

test('a map baked elsewhere comes in through the panel with its green as the project says, and curvature keeps to the part', async () => {
  test.setTimeout(180_000);
  const library = join(scratch, 'Game.ptlib');
  const projects = join(library, 'projects');
  const yaml = () => {
    const name = readdirSync(projects).find(folder => existsSync(join(projects, folder, 'project.yaml')));
    return name ? readFileSync(join(projects, name, 'project.yaml'), 'utf8') : '';
  };

  // The project's normal maps run their green down, as DirectX's do (#144).
  await app.evaluate(({ BrowserWindow }) => {
    BrowserWindow.getAllWindows()[0]?.webContents.send('patina:command', 'projectSettings');
  });
  await pick(window, 'project-settings-normal-format', 'DirectX (Y−)');
  await window.getByTestId('project-settings-apply').click();
  await expect(window.getByTestId('project-settings')).toHaveCount(0);
  expect(yaml()).toContain('normalFormat: y-down');

  // Bring In starts from the project's green, and a normal whose green runs down comes in turned up: written again
  // by the GPU engine as a 16-bit PNG rather than copied (#300).
  const normal = join(scratch, 'normal_dx.png');
  writeFileSync(normal, pixelPng(128, 64, 255));
  await window.getByTestId('open-bake').click();
  await window.getByTestId('bake-section-normal').click();
  const greenDown = window.getByTestId('bake-import-green-down');
  await expect(greenDown).toBeChecked();
  const imported = () =>
    readdirSync(library, { recursive: true, encoding: 'utf8' }).find(file => file.endsWith('imported_normal.png'));
  const depth = () => {
    const file = imported();
    return file ? readFileSync(join(library, file))[24] : 0;
  };
  await answerOpen(app, normal);
  await chooseFromComputer(app, window, 'bake-import');
  await expect.poll(depth, { timeout: 60_000 }).toBe(16);
  await expect(window.getByTestId('bake-import')).toHaveText('Bring In…');

  // Told its green runs up, the same picture is copied as it is.
  await greenDown.click();
  await expect(greenDown).not.toBeChecked();
  await answerOpen(app, normal);
  await chooseFromComputer(app, window, 'bake-import');
  await expect
    .poll(
      () => {
        const file = imported();
        return file ? readFileSync(join(library, file)).equals(readFileSync(normal)) : false;
      },
      { timeout: 60_000 }
    )
    .toBe(true);
  await expect(window.getByTestId('app-failure')).toHaveCount(0);

  // Curvature by rays meets only a part's own surface once parts match by name (#300), and the project keeps it.
  await window.getByTestId('bake-section-common').click();
  await window.getByTestId('bake-match').click();
  await window.getByTestId('bake-section-curvature').click();
  await window.getByTestId('bake-curvature-source').getByRole('radio', { name: 'Rays' }).click();
  const within = window.getByTestId('bake-curvature-within-part');
  await expect(within).toBeEnabled();
  await within.click();
  await expect(within).toBeChecked();
  await expect.poll(() => block(yaml(), 'curvature'), { timeout: 10_000 }).toContain('withinPart: true');
  expect(block(yaml(), 'curvature')).toContain('source: rays');
  mkdirSync(shots, { recursive: true });
  await window.screenshot({ path: join(shots, 'bake-curvature-within-part.png') });
});

/** A PNG's width and height, from its header. */
function pngSize(path: string): [number, number] {
  const header = readFileSync(path);
  return [header.readUInt32BE(16), header.readUInt32BE(20)];
}

test('a Quick Bake of a set twice as wide as it is tall bakes at its shape, and a new shape says to bake again', async () => {
  test.setTimeout(300_000);
  await showDocumentSettings(window);
  await expect(window.getByTestId('texture-set-settings')).toBeVisible({ timeout: 60_000 });
  const size = window.getByTestId('texture-set-size');
  const width = Number(/(\d+)×/u.exec((await size.textContent()) ?? '')?.[1]);
  expect(width).toBeGreaterThan(0);
  await window.getByTestId('texture-set-height').click();
  await window.getByRole('option', { name: String(width / 2), exact: true }).click();
  await expect(size).toContainText(`×${width / 2}`, { timeout: 30_000 });

  // No dialog: the project's settings, which follow each set's shape unless told not to (#253).
  await window.getByTestId('quick-bake').click();
  const library = join(scratch, 'Game.ptlib');
  const find = () =>
    readdirSync(library, { recursive: true, encoding: 'utf8' }).find(
      // The cache holds the shelf's own sample bake too, which is not this project's.
      file => file.endsWith('.bake.json') && !file.startsWith('.ptcache')
    );
  await expect.poll(find, { timeout: 240_000 }).toBeDefined();
  await expect(window.getByTestId('quick-bake')).toHaveText('Quick Bake', { timeout: 30_000 });
  const manifest = find();
  const baked = JSON.parse(readFileSync(join(library, manifest ?? ''), 'utf8')) as {
    result: { maps: { map: string; path: string; resolution: number; rows?: number | null }[] };
  };
  const normal = baked.result.maps.find(map => map.map === 'normal');
  expect(normal).toBeDefined();
  expect(normal?.rows).toBe((normal?.resolution ?? 0) / 2);
  const [columns, rows] = pngSize(join(library, manifest ?? '', '..', normal?.path ?? ''));
  expect(rows * 2).toBe(columns);
  await expect(window.getByTestId('layers-stale-bake')).toHaveCount(0);

  // Square again, the set's maps are stretched under it, and Layers says to bake again (#262).
  await window.getByTestId('texture-set-height').click();
  await window.getByRole('option', { name: String(width), exact: true }).click();
  await expect(window.getByTestId('layers-stale-bake')).toContainText('Width or Height', { timeout: 30_000 });
});

test('the panel says why the Server refused a high poly mesh', async () => {
  await window.getByTestId('open-bake').click();
  await choose('high', '/nowhere/high.glb');

  await expect(window.getByTestId('app-failure')).toContainText('high.glb isn’t there any more');
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
  await expect(window.getByTestId('app-failure')).toContainText('high.glb isn’t there any more');
  await window.keyboard.press('Escape');
  await window.getByTestId('app-failure-dismiss').click();
  await expect(window.getByTestId('activity-unread')).toBeVisible();
  await window.getByTestId('activity-readout').click();
  await expect(panel.getByTestId('activity-notice').first()).toContainText('high.glb isn’t there any more');
  await expect(window.getByTestId('activity-unread')).toHaveCount(0);
});
