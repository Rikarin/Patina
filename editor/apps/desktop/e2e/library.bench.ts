/* eslint-disable no-console --
   A measurement, not product code: it prints its numbers. Run it by hand on an otherwise idle machine:
   pnpm exec playwright test --config apps/desktop/e2e/bench.config.ts library */
import { _electron as electron, expect, test } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { existsSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
const bench = resolve(__dirname, '../../../../engine/tools/Patina.Bench/bin/Release/net11.0/patina-bench.dll');
const shots = resolve(__dirname, '../../../dist/e2e-results');
const Projects = Number(process.env['BENCH_PROJECTS'] ?? 10_000);
const Seconds = Number(process.env['BENCH_SECONDS'] ?? 6);

interface Scroll {
  label: string;
  frames: number;
  seconds: number;
  over17: number;
  over33: number;
  worstMs: number;
  /** Rendered rows still waiting for their page, as a share of all rows over all frames. */
  blankShare: number;
  mostRows: number;
  requests: number;
}

/** Moves the list once per display frame and times the frames, which is what a user sees as smoothness. */
async function scroll([label, pixelsPerFrame, seconds]: readonly [string, number, number]): Promise<Scroll> {
  const list = document.querySelector<HTMLElement>('[data-testid=library-list]');
  if (!list) {
    throw new Error('No list on screen.');
  }

  list.scrollTop = 0;
  await new Promise(done => setTimeout(done, 300));
  performance.clearResourceTimings();
  const max = list.scrollHeight - list.clientHeight;
  const started = performance.now();
  let last = started;
  let frames = 0;
  let over17 = 0;
  let over33 = 0;
  let worst = 0;
  let rows = 0;
  let blank = 0;
  let mostRows = 0;
  let position = 0;
  let direction = 1;
  await new Promise<void>(done => {
    const tick = (now: number) => {
      const gap = now - last;
      last = now;
      if (frames > 0) {
        over17 += gap > 17.5 ? 1 : 0;
        over33 += gap > 34 ? 1 : 0;
        worst = Math.max(worst, gap);
      }

      frames++;
      const rendered = list.querySelectorAll('[data-testid=library-row]');
      rows += rendered.length;
      blank += list.querySelectorAll('xui-skeleton').length / 2;
      mostRows = Math.max(mostRows, rendered.length);
      if (now - started > seconds * 1000) {
        done();
        return;
      }

      position += pixelsPerFrame * direction;
      if (position >= max || position <= 0) {
        direction = -direction;
        position = Math.max(0, Math.min(max, position));
      }

      list.scrollTop = position;
      requestAnimationFrame(tick);
    };
    requestAnimationFrame(tick);
  });

  const requests = performance
    .getEntriesByType('resource')
    .filter(entry => entry.name.includes('/library/projects')).length;
  return {
    label,
    frames,
    seconds: (performance.now() - started) / 1000,
    over17,
    over33,
    worstMs: Math.round(worst * 10) / 10,
    blankShare: Math.round((blank / Math.max(rows, 1)) * 1000) / 1000,
    mostRows,
    requests
  };
}

test('browsing a generated library', async () => {
  const scratch = mkdtempSync(join(tmpdir(), 'patina-bench-library-'));
  const library = process.env['BENCH_LIBRARY'] ?? join(scratch, 'Generated.ptlib');
  try {
    if (!existsSync(join(library, 'library.yaml'))) {
      execFileSync('dotnet', [bench, 'generate', library, String(Projects)], { stdio: 'inherit' });
    }

    const started = Date.now();
    const app = await electron.launch({
      args: [main],
      env: { ...process.env, PATINA_USER_DATA: join(scratch, 'user-data'), PATINA_LIBRARY: library }
    });
    const window = await app.firstWindow();
    await app.evaluate(({ BrowserWindow }) => {
      BrowserWindow.getAllWindows()[0]?.setSize(1440, 900);
    });

    const count = window.getByTestId('library-count');
    await expect(count).toHaveText(/[\d,]+ projects?/, { timeout: 120_000 });
    await expect(window.getByTestId('library-indexing')).toHaveCount(0, { timeout: 120_000 });
    await expect(count).toHaveText(`${Projects.toLocaleString('en')} projects`, { timeout: 120_000 });
    console.log(`launch to ${await count.textContent()}, indexed: ${Date.now() - started} ms`);

    for (const [word, label] of [
      ['rust', 'one word'],
      ['rust cr', 'two words']
    ] as const) {
      const before = await count.textContent();
      const at = Date.now();
      await window.getByTestId('library-search').fill(word);
      await expect(count).not.toHaveText(before ?? '');
      console.log(
        `search, ${label} ('${word}'), 200 ms debounce included: ${Date.now() - at} ms -> ${await count.textContent()}`
      );
    }

    await window.screenshot({ path: join(shots, 'library-search.png') });
    await window.getByTestId('library-search').fill('');
    await expect(count).toHaveText(`${Projects.toLocaleString('en')} projects`);
    await expect(window.getByTestId('library-row').first()).toContainText(/\S/);

    for (const [label, pixels] of [
      ['wheel, 40 px a frame', 40],
      ['flick, 400 px a frame', 400],
      ['scrollbar drag, 4,000 px a frame', 4000]
    ] as const) {
      console.log(JSON.stringify(await window.evaluate(scroll, [label, pixels, Seconds] as const)));
    }

    const heap = await window.evaluate(
      () => (performance as unknown as { memory: { usedJSHeapSize: number } }).memory.usedJSHeapSize
    );
    console.log(`heap after scrolling: ${(heap / 1024 / 1024).toFixed(1)} MiB`);

    await window.evaluate(() => {
      const list = document.querySelector<HTMLElement>('[data-testid=library-list]');
      if (list) {
        list.scrollTop = list.scrollHeight / 2;
      }
    });
    await expect(window.locator('[data-testid=library-row] xui-skeleton')).toHaveCount(0);
    await window.getByTestId('library-row').nth(5).click();
    await expect(window.getByTestId('inspector-title')).not.toHaveCount(0);
    await window.screenshot({ path: join(shots, 'library-10k.png') });

    await window.getByRole('radio', { name: 'Materials' }).click();
    await expect(window.getByTestId('library-row').first()).toContainText(/\S/);
    await window.getByTestId('library-row').first().click();
    const at = Date.now();
    await expect(window.getByTestId('impact-summary')).toContainText('Affects');
    console.log(
      `impact, first asset: ${Date.now() - at} ms -> ${await window.getByTestId('impact-summary').textContent()}`
    );
    await window.screenshot({ path: join(shots, 'library-10k-impact.png') });

    await window.getByRole('radio', { name: 'Projects' }).click();
    const created = Date.now();
    await window.getByRole('button', { name: 'New…' }).click();
    await window.getByTestId('new-project-name').fill('Bench Project');
    await window.getByRole('button', { name: 'Create' }).click();
    await expect(count).toHaveText(`${(Projects + 1).toLocaleString('en')} projects`);
    console.log(`create to listed, dialog included: ${Date.now() - created} ms`);
    await app.close();
  } finally {
    rmSync(join(library, 'projects/bench-project'), { recursive: true, force: true });
    rmSync(scratch, { recursive: true, force: true });
  }
});
