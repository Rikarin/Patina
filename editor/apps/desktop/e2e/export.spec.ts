import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { existsSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
const bench = resolve(__dirname, '../../../../engine/tools/Patina.Bench/bin/Release/net10.0/patina-bench.dll');
const softwareWebGl = process.platform === 'linux' ? ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] : [];

let scratch: string;
let app: ElectronApplication;
let window: Page;

test.beforeEach(async () => {
  scratch = mkdtempSync(join(tmpdir(), 'patina-e2e-'));
  const library = join(scratch, 'Game.ptlib');
  execFileSync('dotnet', [bench, 'bake-library', library]);
  app = await electron.launch({
    args: [...softwareWebGl, main],
    env: { ...process.env, PATINA_USER_DATA: join(scratch, 'user-data'), PATINA_LIBRARY: library }
  });
  window = await app.firstWindow();
  await expect(window.getByRole('status', { name: 'Server status' })).toContainText('Worker connected');
});

test.afterEach(async () => {
  await app.close();
  rmSync(scratch, { recursive: true, force: true });
});

test('a preset is chosen, its impact shown, and the report names what was written', async () => {
  await window.getByRole('tab', { name: 'Export' }).click();
  await expect(window.getByTestId('export-preset-info')).toContainText('Godot');

  await window.getByTestId('export-preview').click();
  await expect(window.getByTestId('export-impact-rebuilds')).toHaveText('1');
  await expect(window.getByTestId('export-impact-exports')).toHaveText('1');

  await window.getByTestId('export-start').click();
  await expect(window.getByTestId('export-state')).toHaveText('done', { timeout: 120_000 });
  await expect(window.getByTestId('export-report-built')).toHaveText('1');
  await expect(window.getByTestId('export-report-failed')).toHaveText('0');
  await expect(window.getByTestId('export-report-project')).toContainText('godot');

  expect(existsSync(join(scratch, 'Game.ptlib', 'exports', 'godot', 'crate', 'Crate_normal.png'))).toBe(true);
});
