import { defineConfig } from '@playwright/test';

/** Spike S3's measurements (docs/adr/0003-frame-transport.md). Not a test: run it by hand on an otherwise idle machine. */
export default defineConfig({
  testDir: '.',
  testMatch: '*.bench.ts',
  timeout: 300_000,
  workers: 1,
  reporter: 'list',
  outputDir: '../../../dist/e2e-results'
});
