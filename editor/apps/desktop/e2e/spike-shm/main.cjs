// Spike S3 only: frames from a real Worker straight into an Electron renderer through shared memory, with no Server.
// It needs an unsandboxed renderer, which is one reason the app does not do this (ADR 0003).
//
//   clang -shared -undefined dynamic_lookup -I"$(dirname "$(dirname "$(which node)")")/include/node" shm.c -o shm.node
//   electron main.cjs <patina-worker.dll> <mesh>
const { app, BrowserWindow } = require('electron');
const { join } = require('node:path');

app.whenReady().then(async () => {
  const window = new BrowserWindow({
    width: 800,
    height: 500,
    webPreferences: { nodeIntegration: true, contextIsolation: false, sandbox: false }
  });
  window.webContents.on('console-message', event => console.log(event.message));
  await window.loadURL('about:blank');
  const gpu = app.getAppMetrics().find(metric => metric.type === 'GPU')?.pid ?? 0;
  const options = { worker: process.argv[2], mesh: process.argv[3], gpu, addon: join(__dirname, 'shm.node') };
  await window.webContents.executeJavaScript(`(${page.toString()})(${JSON.stringify(options)})`);
  app.quit();
});

async function page({ worker, mesh, gpu, addon }) {
  const net = require('node:net');
  const fs = require('node:fs');
  const os = require('node:os');
  const path = require('node:path');
  const { spawn, execFileSync } = require('node:child_process');
  const shm = require(addon);

  const Seconds = 6;
  const Slot = 3840 * 2160 * 4;
  const folder = fs.mkdtempSync(path.join(os.tmpdir(), 'patina-spike-'));
  const socketPath = path.join(folder, 's.sock');
  const frames = path.join(folder, 'frames');
  fs.writeFileSync(frames, '');
  fs.truncateSync(frames, Slot * 2);
  shm.open(frames, Slot * 2);
  console.log(`napi_create_external_arraybuffer status: ${shm.wrap(0, 1024)} (0 is ok)`);

  let id = 100;
  const replies = new Map();
  let link;
  const hello = new Promise(done => {
    const server = net.createServer(socket => {
      link = socket;
      let buffer = Buffer.alloc(0);
      socket.on('data', data => {
        buffer = Buffer.concat([buffer, data]);
        while (buffer.length >= 4 && buffer.length >= 4 + buffer.readInt32LE(0)) {
          const message = JSON.parse(buffer.toString('utf8', 4, 4 + buffer.readInt32LE(0)));
          buffer = buffer.subarray(4 + buffer.readInt32LE(0));
          if (message.type === 'hello') {
            write({ type: 'ok', id: message.id });
            done();
          } else if (message.type === 'ok' || message.type === 'error') {
            replies.get(message.id)?.(message);
            replies.delete(message.id);
          }
        }
      });
    });
    server.listen(socketPath);
  });
  const write = message => {
    const body = Buffer.from(JSON.stringify(message));
    const head = Buffer.alloc(4);
    head.writeInt32LE(body.length);
    link.write(Buffer.concat([head, body]));
  };
  const ask = message =>
    new Promise(done => {
      const own = ++id;
      replies.set(own, done);
      write({ ...message, id: own });
    });

  const child = spawn('dotnet', [worker, '--socket', socketPath], { stdio: 'ignore' });
  await hello;
  await ask({ type: 'viewport.open', mesh });

  const cpu = pid => {
    const parts = execFileSync('ps', ['-o', 'time=', '-p', String(pid)])
      .toString()
      .trim()
      .split(':')
      .map(Number);
    return parts.reduce((total, part) => total * 60 + part, 0);
  };

  const canvas = document.createElement('canvas');
  document.body.append(canvas);
  canvas.style.cssText = 'width:640px;height:360px';

  for (const [width, height] of [
    [2560, 1440],
    [1920, 1080],
    [1280, 720]
  ]) {
    canvas.width = width;
    canvas.height = height;
    const gl = canvas.getContext('webgl2', { alpha: false, antialias: false });
    const texture = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, texture);
    gl.texStorage2D(gl.TEXTURE_2D, 1, gl.RGBA8, width, height);
    const pixels = new Uint8Array(width * height * 4);
    const processes = [
      ['worker', child.pid],
      ['renderer', process.pid],
      ['gpu', gpu]
    ];
    const before = processes.map(([, pid]) => cpu(pid));
    const latency = [];
    const copies = [];
    let painted = 0;
    let frame = 0;
    let waiting = false;
    let ready = null;
    const started = performance.now();
    await new Promise(done => {
      const tick = () => {
        const now = performance.now();
        if (now - started > Seconds * 1000) {
          done();
          return;
        }

        if (!waiting) {
          waiting = true;
          frame++;
          const slot = frame % 2;
          const sent = now;
          ask({ type: 'viewport.camera', camera: { yaw: frame * 0.02, pitch: 0.35, distance: 2.2 } });
          ask({
            type: 'viewport.render',
            render: { width, height, sharedMemory: frames, offset: slot * Slot, capacity: Slot }
          }).then(() => {
            waiting = false;
            ready = { slot, sent };
          });
        }

        if (ready) {
          const start = performance.now();
          shm.copy(ready.slot * Slot, pixels);
          const copied = performance.now();
          gl.texSubImage2D(gl.TEXTURE_2D, 0, 0, 0, width, height, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
          gl.drawArrays(gl.TRIANGLES, 0, 3);
          latency.push(performance.now() - ready.sent);
          copies.push(copied - start);
          painted++;
          ready = null;
        }

        requestAnimationFrame(tick);
      };
      requestAnimationFrame(tick);
    });

    const usage = processes.map(
      ([name, pid], i) => `${name} ${(((cpu(pid) - before[i]) / Seconds) * 100).toFixed(0)}%`
    );
    const middle = values => [...values].sort((a, b) => a - b)[values.length >> 1] ?? 0;
    const high = values => [...values].sort((a, b) => a - b)[Math.floor(values.length * 0.95)] ?? 0;
    console.log(
      `shm webgl ${width}x${height}: ${(painted / Seconds).toFixed(1)} fps, input to paint median ${middle(latency).toFixed(1)} ms, ` +
        `p95 ${high(latency).toFixed(1)} ms, memcpy ${middle(copies).toFixed(2)} ms; cpu ${usage.join(', ')}`
    );
  }

  child.kill('SIGKILL');
  fs.rmSync(folder, { recursive: true, force: true });
}
