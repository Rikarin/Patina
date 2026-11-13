/* eslint-disable no-console, @typescript-eslint/no-non-null-assertion, @typescript-eslint/no-unnecessary-type-assertion --
   A measurement, not product code: it prints its numbers, and `run` executes in the page, not in this Node process. */
import { _electron as electron, test } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
const mesh = resolve(__dirname, '../../../../content/fixtures/viewport-test.glb');
const Seconds = Number(process.env['BENCH_SECONDS'] ?? 8);
const Sizes = [
  [2560, 1440],
  [1920, 1080],
  [1280, 720]
] as const;

const Modes = [
  ['websocket', 'webgl'],
  ['websocket', 'canvas2d'],
  ['stream', 'webgl'],
  ['stream', 'canvas2d']
] as const;

interface Result {
  frames: number;
  seconds: number;
  latency: number[];
  paint: number[];
}

/** One run inside the page: a camera per display frame, the newest frame painted, the wait from one to the other timed. */
async function run(
  painter: 'webgl' | 'canvas2d',
  transport: 'websocket' | 'stream',
  width: number,
  height: number,
  seconds: number
): Promise<Result> {
  const { patina } = globalThis as unknown as { patina: { connection(): Promise<{ baseUrl: string; token: string }> } };
  const connection = await patina.connection();
  const frames = transport === 'stream' ? '&frames=false' : '';
  const socket = new WebSocket(
    `${connection.baseUrl.replace('http', 'ws')}/ws/viewport?access_token=${connection.token}${frames}`
  );
  socket.binaryType = 'arraybuffer';
  await new Promise(done => (socket.onopen = done));

  const canvas = document.createElement('canvas');
  canvas.width = width;
  canvas.height = height;
  canvas.style.cssText = 'width:640px;height:360px';
  document.body.append(canvas);
  const gl = painter === 'webgl' ? canvas.getContext('webgl2', { alpha: false, antialias: false })! : null;
  const context = painter === 'canvas2d' ? canvas.getContext('2d', { alpha: false })! : null;
  gl?.bindTexture(gl.TEXTURE_2D, gl.createTexture());
  gl?.texStorage2D(gl.TEXTURE_2D, 1, gl.RGBA8, width, height);

  const sent = new Map<number, number>();
  const result: Result = { frames: 0, seconds, latency: [], paint: [] };
  let pending: ArrayBuffer | null = null;
  socket.onmessage = event => (pending = event.data as ArrayBuffer);
  const abort = new AbortController();
  if (transport === 'stream') {
    const response = await fetch(`${connection.baseUrl}/api/v1/viewport/frames`, {
      headers: { Authorization: `Bearer ${connection.token}` },
      signal: abort.signal
    });
    if (!response.ok) {
      throw new Error(`The frame stream answered ${response.status}.`);
    }

    const reader = response.body!.getReader();
    void (async () => {
      const header = new Uint8Array(32);
      let body = new Uint8Array(0);
      let at = 0;
      let need = 32;
      let inHeader = true;
      for (;;) {
        const { value, done } = await reader.read().catch(() => ({ value: undefined, done: true }));
        if (done || !value) {
          return;
        }

        let from = 0;
        while (from < value.length) {
          const take = Math.min(need - at, value.length - from);
          (inHeader ? header : body).set(value.subarray(from, from + take), inHeader ? at : at + 32);
          at += take;
          from += take;
          if (at < need) {
            break;
          }

          if (inHeader) {
            const view = new DataView(header.buffer);
            need = view.getUint32(8, true) * view.getUint32(12, true) * 4;
            body = new Uint8Array(32 + need);
            body.set(header);
            inHeader = false;
          } else {
            pending = body.buffer as ArrayBuffer;
            inHeader = true;
            need = 32;
          }

          at = 0;
        }
      }
    })();
  }

  socket.send(JSON.stringify({ width, height }));

  let input = 0;
  const started = performance.now();
  await new Promise<void>(done => {
    const tick = () => {
      const now = performance.now();
      if (now - started > seconds * 1000) {
        done();
        return;
      }

      input++;
      sent.set(input, now);
      socket.send(JSON.stringify({ camera: { yaw: input * 0.02, pitch: 0.35, distance: 2.2 }, input }));
      if (pending) {
        const message: ArrayBuffer = pending;
        pending = null;
        const header = new DataView(message, 0, 32);
        const [w, h] = [header.getUint32(8, true), header.getUint32(12, true)];
        const time = sent.get(header.getUint32(16, true));
        if (w === width && h === height) {
          const pixels = new Uint8Array(message, 32);
          const start = performance.now();
          if (gl) {
            gl.texSubImage2D(gl.TEXTURE_2D, 0, 0, 0, w, h, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
            gl.drawArrays(gl.TRIANGLES, 0, 3);
          } else {
            context!.putImageData(new ImageData(new Uint8ClampedArray(message, 32), w, h), 0, 0);
          }

          const end = performance.now();
          result.paint.push(end - start);
          if (time !== undefined) {
            result.latency.push(end - time);
          }

          result.frames++;
        }
      }

      requestAnimationFrame(tick);
    };
    requestAnimationFrame(tick);
  });
  socket.close();
  abort.abort();
  canvas.remove();
  return result;
}

function cpuSeconds(pid: number): number {
  const text = execFileSync('ps', ['-o', 'time=', '-p', String(pid)])
    .toString()
    .trim();
  const parts = text.split(':').map(Number);
  return parts.reduce((total, part) => total * 60 + part, 0);
}

function percentile(values: number[], share: number): number {
  const sorted = [...values].sort((a, b) => a - b);
  return sorted[Math.floor(sorted.length * share)] ?? 0;
}

test('frames to a canvas over the WebSocket', async () => {
  const scratch = mkdtempSync(join(tmpdir(), 'patina-bench-'));
  const app = await electron.launch({
    args: [main],
    env: { ...process.env, PATINA_USER_DATA: join(scratch, 'user-data') }
  });
  const window = await app.firstWindow();
  await window.getByRole('status', { name: 'Engine status' }).getByText('GPU ready').waitFor();
  // A blank page, so the pane's own subscriber is not part of the numbers. The preload still runs there.
  await window.goto('about:blank');
  await window.reload();
  await window.waitForFunction(() => 'patina' in globalThis);
  const pids = await window.evaluate(async path => {
    const { patina } = globalThis as unknown as {
      patina: {
        connection(): Promise<{ baseUrl: string; token: string }>;
        sidecars(): Promise<{ name: string; pid: number }[]>;
      };
    };
    const connection = await patina.connection();
    await fetch(`${connection.baseUrl}/api/v1/viewport/mesh`, {
      method: 'PUT',
      headers: { Authorization: `Bearer ${connection.token}`, 'Content-Type': 'application/json' },
      body: JSON.stringify({ path })
    });
    return patina.sidecars();
  }, mesh);
  const metrics = await app.evaluate(({ app: electronApp }) => electronApp.getAppMetrics());
  const processes = [
    ...pids.map(({ name, pid }) => ({ name, pid })),
    ...metrics
      .filter(metric => metric.type === 'Tab' || metric.type === 'GPU')
      .map(metric => ({ name: metric.type === 'Tab' ? 'renderer' : 'gpu', pid: metric.pid }))
  ];

  for (const [transport, painter] of Modes) {
    for (const [width, height] of Sizes) {
      if (painter === 'canvas2d' && width !== Sizes[0][0]) {
        continue;
      }

      const before = processes.map(process => cpuSeconds(process.pid));
      const result = (await window.evaluate(
        `(${run.toString()})('${painter}', '${transport}', ${width}, ${height}, ${Seconds})`
      )) as Result;
      const cpu = processes.map(
        (process, i) => `${process.name} ${(((cpuSeconds(process.pid) - before[i]!) / Seconds) * 100).toFixed(0)}%`
      );
      const megabytes = (result.frames * width * height * 4) / Seconds / 1e6;
      console.log(
        `${transport} ${painter} ${width}x${height}: ${(result.frames / Seconds).toFixed(1)} fps, ${megabytes.toFixed(0)} MB/s, ` +
          `input to paint median ${percentile(result.latency, 0.5).toFixed(1)} ms, p95 ${percentile(result.latency, 0.95).toFixed(1)} ms, ` +
          `paint call ${percentile(result.paint, 0.5).toFixed(2)} ms; cpu ${cpu.join(', ')}`
      );
    }
  }

  await app.close();
  rmSync(scratch, { recursive: true, force: true });
});

/** The product, not a harness: the pane under a real drag, with the render scale free to act. */
test('the pane during a sustained orbit', async () => {
  const scratch = mkdtempSync(join(tmpdir(), 'patina-bench-'));
  const app = await electron.launch({
    args: [main],
    env: { ...process.env, PATINA_USER_DATA: join(scratch, 'user-data') }
  });
  const window = await app.firstWindow();
  await app.evaluate(({ BrowserWindow, dialog }, path) => {
    dialog.showOpenDialog = (() => Promise.resolve({ canceled: false, filePaths: [path] })) as never;
    BrowserWindow.getAllWindows()[0]?.setSize(2100, 1000);
  }, mesh);
  await window.getByRole('button', { name: 'Open mesh…' }).click();
  const canvas = window.getByRole('application', { name: 'Viewport' });
  await canvas.waitFor();
  await window.waitForFunction(() => Number(document.querySelector('canvas')?.dataset['frames']) > 0);
  const box = (await canvas.boundingBox())!;
  await window.mouse.move(box.x + 200, box.y + 200);
  await window.mouse.down();
  const sidecars = await window.evaluate(() =>
    (globalThis as unknown as { patina: { sidecars(): Promise<{ name: string; pid: number }[]> } }).patina.sidecars()
  );
  const others = (await app.evaluate(({ app: electronApp }) => electronApp.getAppMetrics()))
    .filter(metric => metric.type === 'Tab' || metric.type === 'GPU')
    .map(metric => ({ name: metric.type === 'Tab' ? 'renderer' : 'gpu', pid: metric.pid }));
  const processes = [...sidecars, ...others];
  const before = processes.map(process => cpuSeconds(process.pid));
  const started = Date.now();
  let step = 0;
  while (Date.now() - started < Seconds * 1000) {
    await window.mouse.move(box.x + 200 + (step++ % 400), box.y + 200);
    if (step % 60 === 0) {
      console.log(await window.getByTestId('viewport-stats').textContent());
    }
  }

  const usage = processes.map(
    (process, i) => `${process.name} ${(((cpuSeconds(process.pid) - before[i]!) / Seconds) * 100).toFixed(0)}%`
  );
  console.log(`cpu during the drag: ${usage.join(', ')}`);
  await window.mouse.up();
  console.log(`after the drag: ${await window.getByTestId('viewport-stats').textContent()}`);
  const idle = processes.map(process => cpuSeconds(process.pid));
  await new Promise(done => setTimeout(done, 4000));
  const resting = processes.map(
    (process, i) => `${process.name} ${(((cpuSeconds(process.pid) - idle[i]!) / 4) * 100).toFixed(0)}%`
  );
  console.log(`cpu at rest: ${resting.join(', ')}`);
  await app.close();
  rmSync(scratch, { recursive: true, force: true });
});
