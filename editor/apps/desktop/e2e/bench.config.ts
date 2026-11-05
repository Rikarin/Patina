import { defineConfig } from '@playwright/test';

/** Measurements: spike S3's (docs/adr/0003-frame-transport.md) and the Library's. Not tests: run them by hand on an otherwise idle machine. */
export default defineConfig({
  testDir: '.',
  testMatch: '*.bench.ts',
  timeout: 300_000,
  workers: 1,
  reporter: 'list',
  outputDir: '../../../dist/e2e-results'
});
