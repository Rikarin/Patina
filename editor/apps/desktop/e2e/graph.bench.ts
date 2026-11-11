/* eslint-disable no-console --
   A measurement, not product code: it prints its numbers. Run it by hand on an otherwise idle machine:
   pnpm exec playwright test --config apps/desktop/e2e/bench.config.ts graph

   Spike S7 (docs/adr/0006-node-graph-scale.md): a graph of BENCH_NODES nodes with live thumbnails, inside the dock
   manager, under bezel, in a real Electron window. */
import { _electron as electron, expect, test, type Page } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
const bench = resolve(__dirname, '../../../../engine/tools/Patina.Bench/bin/Release/net11.0/patina-bench.dll');
const shots = resolve(__dirname, '../../../dist/e2e-results');
const Nodes = Number(process.env['BENCH_NODES'] ?? 500);
const Seconds = Number(process.env['BENCH_SECONDS'] ?? 6);
const Rows = Math.ceil(Nodes / 25);

interface Motion {
  label: string;
  frames: number;
  seconds: number;
  fps: number;
  medianMs: number;
  p95Ms: number;
  p99Ms: number;
  worstMs: number;
  over17: number;
  over33: number;
  /** Animation frames over 50 ms, which is where a stutter comes from, and where each one's time went. */
  longFrames: string[];
}

/**
 * One step of the gesture per display frame, as real events through xUI's own handlers. Pointer capture needs a real
 * pointer, so the bench stubs it; everything after that is the path a hand takes.
 */
async function animate([label, kind, seconds]: readonly [string, 'pan' | 'zoom', number]): Promise<Motion> {
  const canvas = document.querySelector<HTMLElement>('[data-testid=graph-canvas]');
  if (!canvas) {
    throw new Error('No canvas on screen.');
  }

  Element.prototype.setPointerCapture = () => undefined;
  Element.prototype.releasePointerCapture = () => undefined;
  const box = canvas.getBoundingClientRect();
  const x = box.left + box.width / 2;
  const y = box.top + box.height / 2;
  const pointer = (type: string, clientX: number, clientY: number): void => {
    canvas.dispatchEvent(new PointerEvent(type, { bubbles: true, button: 1, clientX, clientY, pointerId: 1 }));
  };

  const longFrames: string[] = [];
  const observer = new PerformanceObserver(list => {
    for (const entry of list.getEntries() as (PerformanceEntry & {
      renderStart: number;
      styleAndLayoutStart: number;
      scripts: { duration: number }[];
    })[]) {
      const script = entry.scripts.reduce((sum, item) => sum + item.duration, 0);
      const layout = entry.startTime + entry.duration - entry.styleAndLayoutStart;
      longFrames.push(
        `${entry.duration.toFixed(0)} ms: scripts ${script.toFixed(0)}, style, layout and paint ${layout.toFixed(0)}`
      );
    }
  });
  observer.observe({ type: 'long-animation-frame', buffered: false });

  const gaps: number[] = [];
  const started = performance.now();
  let last = started;
  let phase = 0;
  if (kind === 'pan') {
    pointer('pointerdown', x, y);
  }

  await new Promise<void>(done => {
    const tick = (now: number) => {
      gaps.push(now - last);
      last = now;
      if (now - started > seconds * 1000) {
        done();
        return;
      }

      phase += 1;
      if (kind === 'pan') {
        pointer('pointermove', x + Math.sin(phase / 40) * 300, y + Math.cos(phase / 53) * 120);
      } else {
        const deltaY = Math.cos(phase / 30) * 6;
        canvas.dispatchEvent(
          new WheelEvent('wheel', { bubbles: true, cancelable: true, clientX: x, clientY: y, deltaY })
        );
      }

      requestAnimationFrame(tick);
    };
    requestAnimationFrame(tick);
  });

  if (kind === 'pan') {
    pointer('pointerup', x, y);
  }

  observer.disconnect();
  const measured = gaps.slice(1).sort((a, b) => a - b);
  const at = (share: number) => Math.round((measured[Math.floor(measured.length * share)] ?? 0) * 10) / 10;
  const total = measured.reduce((sum, gap) => sum + gap, 0) / 1000;
  return {
    label,
    frames: measured.length,
    seconds: Math.round(total * 100) / 100,
    fps: Math.round((measured.length / total) * 10) / 10,
    medianMs: at(0.5),
    p95Ms: at(0.95),
    p99Ms: at(0.99),
    worstMs: Math.round((measured.at(-1) ?? 0) * 10) / 10,
    over17: measured.filter(gap => gap > 17.5).length,
    over33: measured.filter(gap => gap > 34).length,
    longFrames
  };
}

/** Wheels the canvas until the zoom is near a target, a frame at a time, and says where it got to. */
async function zoomTo(window: Page, target: number): Promise<void> {
  const reached = await window.evaluate(async wanted => {
    const canvas = document.querySelector<HTMLElement>('[data-testid=graph-canvas]');
    if (!canvas) {
      return 0;
    }

    const box = canvas.getBoundingClientRect();
    const zoomNow = () => {
      const layer = [...canvas.querySelectorAll<HTMLElement>('*')].find(element =>
        element.style.transform.includes('scale(')
      );
      return Number(/scale\(([\d.]+)\)/.exec(layer?.style.transform ?? '')?.[1] ?? 1);
    };
    for (let step = 0; step < 200 && Math.abs(Math.log(zoomNow() / wanted)) > 0.08; step++) {
      canvas.dispatchEvent(
        new WheelEvent('wheel', {
          bubbles: true,
          cancelable: true,
          clientX: box.left + box.width / 2,
          clientY: box.top + box.height / 2,
          deltaY: zoomNow() < wanted ? -20 : 20
        })
      );
      await new Promise(done => requestAnimationFrame(done));
    }

    return zoomNow();
  }, target);
  console.log(`zoom ${reached.toFixed(3)}`);
  await window.waitForTimeout(500);
}

interface TraceEvent {
  name: string;
  ph: string;
  args?: Record<string, Record<string, string | boolean>>;
}

/** What the compositor presented during a run, from Chromium's own frame reporting. rAF cannot see a dropped draw. */
async function traced<T>(window: Page, run: () => Promise<T>): Promise<T & { compositor: unknown }> {
  const session = await window.context().newCDPSession(window);
  const events: TraceEvent[] = [];
  session.on('Tracing.dataCollected', ({ value }) => events.push(...(value as unknown as TraceEvent[])));
  await session.send('Tracing.start', {
    transferMode: 'ReportEvents',
    traceConfig: { includedCategories: ['cc', 'benchmark'], recordMode: 'recordContinuously' }
  });
  const result = await run();
  const finished = new Promise(done => session.once('Tracing.tracingComplete', done));
  await session.send('Tracing.end');
  await finished;
  await session.detach();
  const states: Record<string, number> = {};
  let checkerboarded = 0;
  for (const event of events) {
    const reporter = event.args?.['frame_reporter'];
    if (event.name === 'PipelineReporter' && event.ph === 'b' && reporter) {
      const state = String(reporter['state']);
      states[state] = (states[state] ?? 0) + 1;
      checkerboarded += reporter['has_missing_content'] ? 1 : 0;
    }
  }

  return { ...result, compositor: { ...states, checkerboarded } };
}

test('spike S7: a graph of 500 nodes with live thumbnails', async () => {
  const scratch = mkdtempSync(join(tmpdir(), 'patina-bench-graph-'));
  const library = join(scratch, 'Generated.ptlib');
  try {
    execFileSync('dotnet', [bench, 'generate', library, '10'], { stdio: 'inherit' });
    execFileSync('dotnet', [bench, 'graph', library, String(Nodes), 'Scale Graph'], { stdio: 'inherit' });
    const app = await electron.launch({
      args: [main],
      env: { ...process.env, PATINA_USER_DATA: join(scratch, 'user-data'), PATINA_LIBRARY: library }
    });
    const window = await app.firstWindow();
    await app.evaluate(({ BrowserWindow }) => {
      BrowserWindow.getAllWindows()[0]?.setSize(1440, 900);
    });
    console.log(`device pixel ratio ${String(await window.evaluate(() => devicePixelRatio))}`);
    await window.getByRole('radio', { name: 'Materials' }).click();
    await window.getByTestId('library-search').fill('Scale Graph');
    await expect(window.getByTestId('library-row')).toHaveCount(1, { timeout: 60_000 });
    await window.getByTestId('library-row').click();

    const opened = Date.now();
    await window.getByTestId('open-graph').click();
    await expect(window.getByTestId('graph-node')).toHaveCount(Nodes, { timeout: 120_000 });
    console.log(`open to ${String(Nodes)} nodes in the DOM: ${String(Date.now() - opened)} ms`);
    await expect(window.locator('ptn-graph-thumbnail[data-hash]')).toHaveCount(Nodes, { timeout: 180_000 });
    console.log(`open to every thumbnail shown: ${String(Date.now() - opened)} ms`);
    console.log(`first evaluation: ${(await window.getByTestId('graph-evaluation').textContent()) ?? ''}`);
    console.log(
      `DOM elements: ${String(await window.evaluate(() => document.querySelectorAll('*').length))}, wires: ${String(await window.locator('xui-node-graph svg path').count())}`
    );
    await window.waitForTimeout(1_000);
    await window.screenshot({ path: join(shots, 'graph-500-fit.png') });

    const pane = window.getByTestId('graph-canvas');
    const size = await pane.boundingBox();
    console.log(`canvas: ${String(size?.width)} x ${String(size?.height)} CSS pixels`);

    for (const [label, kind] of [
      ['fit, all nodes on screen: pan', 'pan'],
      ['fit, all nodes on screen: zoom', 'zoom']
    ] as const) {
      console.log(
        JSON.stringify(await traced(window, () => window.evaluate(animate, [label, kind, Seconds] as const)))
      );
    }

    await zoomTo(window, 1);
    await window.screenshot({ path: join(shots, 'graph-500-near.png') });
    for (const [label, kind] of [
      ['1:1, a few nodes on screen: pan', 'pan'],
      ['1:1, a few nodes on screen: zoom', 'zoom']
    ] as const) {
      console.log(
        JSON.stringify(await traced(window, () => window.evaluate(animate, [label, kind, Seconds] as const)))
      );
    }

    // The largest the canvas gets: the pane maximized in the window, with everything on screen.
    await window.getByRole('button', { name: 'Maximize Graph' }).click();
    await expect(window.getByTestId('graph-node')).toHaveCount(Nodes);
    await window.getByRole('button', { name: 'Fit view' }).click();
    await window.waitForTimeout(800);
    const full = await pane.boundingBox();
    console.log(`maximized canvas: ${String(full?.width)} x ${String(full?.height)} CSS pixels`);
    await window.screenshot({ path: join(shots, 'graph-500-maximized.png') });
    for (const [label, kind] of [
      ['maximized, fit: pan', 'pan'],
      ['maximized, fit: zoom', 'zoom']
    ] as const) {
      console.log(
        JSON.stringify(await traced(window, () => window.evaluate(animate, [label, kind, Seconds] as const)))
      );
    }

    await zoomTo(window, 0.5);
    console.log(
      JSON.stringify(
        await traced(window, () => window.evaluate(animate, ['maximized, 50%: pan', 'pan', Seconds] as const))
      )
    );
    await window.getByRole('button', { name: 'Restore Graph' }).click();

    // Live thumbnails: every noise changes, so every node in the graph has a new picture, while the canvas pans.
    const { baseUrl, token } = await window.evaluate(() =>
      (
        globalThis as unknown as { patina: { connection(): Promise<{ baseUrl: string; token: string }> } }
      ).patina.connection()
    );
    const headers = { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' };
    const [session] = (await (await fetch(`${baseUrl}/api/v1/graphs`, { headers })).json()) as { id: string }[];
    const live = { editing: true };
    let edits = 0;
    const editing = (async () => {
      while (live.editing) {
        const commands = Array.from({ length: Rows }, (_, row) => ({
          op: 'setValue',
          node: row * 25 + 1,
          port: 'Scale',
          value: [4 + (edits % 40) / 10]
        }));
        await fetch(`${baseUrl}/api/v1/graphs/${session?.id ?? ''}/commands`, {
          method: 'POST',
          headers,
          body: JSON.stringify({ commands, merge: edits > 0 })
        });
        edits++;
        await new Promise(done => setTimeout(done, 100));
      }
    })();
    for (const [zoom, label] of [
      [1, '1:1'],
      [0.1, 'far out']
    ] as const) {
      await zoomTo(window, zoom);
      console.log(
        JSON.stringify(
          await traced(window, () =>
            window.evaluate(animate, [`${label}, every thumbnail changing: pan`, 'pan', Seconds] as const)
          )
        )
      );
    }

    live.editing = false;
    await editing;
    console.log(
      `edits during the live runs: ${String(edits)}, last: ${(await window.getByTestId('graph-evaluation').textContent()) ?? ''}`
    );
    const heap = await window.evaluate(
      () => (performance as unknown as { memory: { usedJSHeapSize: number } }).memory.usedJSHeapSize
    );
    console.log(`heap: ${(heap / 1024 / 1024).toFixed(1)} MiB`);
    await app.close();
  } finally {
    rmSync(scratch, { recursive: true, force: true });
  }
});
