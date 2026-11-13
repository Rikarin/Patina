import { defineConfig } from '@playwright/test';
import { delimiter, dirname } from 'node:path';
import { dotnetHost } from '../src/dotnet';

// The specs run patina-bench through `dotnet`, and the app they start inherits this environment: both find the
// engine's runtime in ~/.dotnet as the app does on its own (#249), whatever `dotnet` is first on PATH.
const dotnet = dotnetHost();
if (dotnet.env['DOTNET_ROOT']) {
  process.env['DOTNET_ROOT'] = dotnet.env['DOTNET_ROOT'];
  process.env['PATH'] = [dirname(dotnet.command), process.env['PATH']].join(delimiter);
}

/** Golden paths through the real desktop app: Electron, the built studio, and the sidecars. */
export default defineConfig({
  testDir: '.',
  timeout: 120_000,
  expect: { timeout: 15_000 },
  workers: 1,
  reporter: process.env['CI']
    ? [['github'], ['html', { open: 'never', outputFolder: '../../../dist/e2e-report' }]]
    : 'list',
  use: { trace: 'retain-on-failure' },
  outputDir: '../../../dist/e2e-results'
});
