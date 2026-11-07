/* eslint-disable no-console --
   A measurement, not product code: it prints its numbers. Run it by hand:
   pnpm exec playwright test --config apps/desktop/e2e/bench.config.ts paint
   BENCH_SIZE, BENCH_LAYERS and BENCH_STROKES change what it measures. */
import { _electron as electron, expect, test } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { copyFileSync, mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { loadavg, tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
const bench = resolve(__dirname, '../../../../engine/tools/Patina.Bench/bin/Release/net10.0/patina-bench.dll');
const patina = resolve(__dirname, '../../../../engine/src/Patina.Cli/bin/Release/net10.0/patina.dll');
const shots = resolve(__dirname, '../../../dist/e2e-results');
const Size = process.env['BENCH_SIZE'] ?? '4096';
const Layers = process.env['BENCH_LAYERS'] ?? '20';
const Strokes = Number(process.env['BENCH_STROKES'] ?? 6);
const Radius = process.env['BENCH_RADIUS'] ?? '12';

function percentile(sorted: readonly number[], share: number): number {
  return sorted[Math.floor((sorted.length - 1) * share)] ?? Number.NaN;
}

test('a stamp reaches the screen through the real window', async () => {
  const scratch = mkdtempSync(join(tmpdir(), 'patina-paint-bench-'));
  execFileSync('dotnet', [bench, 'bake-pair', join(scratch, 'meshes'), '0.05']);
  const library = join(scratch, 'Bench.ptlib');
  const project = join(library, 'projects', 's4');
  execFileSync('dotnet', [patina, 'library', 'new', library]);
  mkdirSync(project);
  copyFileSync(join(scratch, 'meshes', 'housing_low.glb'), join(project, 'housing_low.glb'));
  execFileSync('dotnet', [bench, 's4-stack', join(project, 'S4.vxlayers'), 'housing_low.glb', Size, Layers]);
  const mesh = randomUUID();
  writeFileSync(
    join(project, 'project.yaml'),
    [
      'schema: 2',
      `id: ${randomUUID()}`,
      'name: S4',
      'tags: []',
      'meshes:',
      `  - id: ${mesh}`,
      '    file: housing_low.glb',
      'documents:',
      `  - id: ${randomUUID()}`,
      '    kind: texturing',
      '    name: S4',
      '    file: S4.vxlayers',
      `    mesh: ${mesh}`,
      'references: []',
      'outputs: []',
      ''
    ].join('\n')
  );
  const app = await electron.launch({
    args: [main],
    env: { ...process.env, PATINA_USER_DATA: join(scratch, 'user-data'), PATINA_LIBRARY: library }
  });
  const window = await app.firstWindow();
  await expect(window.getByRole('status', { name: 'Server status' })).toContainText('GPU ready');

  const opened = Date.now();
  await window.locator('[data-testid=tree-row][data-path="s4"]').dblclick();
  await expect(window.getByTestId('viewport-paint')).toBeVisible({ timeout: 120_000 });
  await expect(window.getByTestId('paint-picture')).toBeVisible({ timeout: 120_000 });
  console.log(`opened and evaluated in ${Date.now() - opened} ms`);
  await window.getByTestId('paint-layer-layer1').click();
  await window.getByTestId('paint-size').fill(Radius);
  await window.waitForTimeout(1_000);

  const box = await window.getByRole('img', { name: 'Viewport' }).boundingBox();
  if (!box) {
    throw new Error('No canvas.');
  }

  const loads: number[] = [];
  for (let stroke = 0; stroke < Strokes; stroke += 1) {
    const row = box.y + box.height * (0.42 + stroke * 0.03);
    await window.mouse.move(box.x + box.width * 0.34, row);
    await window.mouse.down();
    for (let step = 1; step <= 60; step += 1) {
      await window.mouse.move(box.x + box.width * (0.34 + step * 0.005), row + Math.sin(step / 6) * 12);
      await window.waitForTimeout(16);
    }

    await window.mouse.up();
    loads.push(loadavg()[0] ?? 0);
    await window.waitForTimeout(500);
  }

  const held = await window.getByTestId('viewport-stamp').getAttribute('data-stamp-ms');
  const times = (held ?? '').split(',').map(Number).filter(Number.isFinite).sort((a, b) => a - b);
  console.log(
    `${Size}², ${Layers} layers, radius ${Radius} px, ${times.length} parts: ` +
      `stamp to frame shown p50 ${percentile(times, 0.5).toFixed(1)} ms, ` +
      `p95 ${percentile(times, 0.95).toFixed(1)} ms, worst ${percentile(times, 1).toFixed(1)} ms; ` +
      `load average ${Math.min(...loads).toFixed(0)} to ${Math.max(...loads).toFixed(0)}`
  );
  await window.screenshot({ path: join(shots, 'paint-bench.png') });
  await app.close();
  rmSync(scratch, { recursive: true, force: true });
  expect(times.length).toBeGreaterThan(20);
});
