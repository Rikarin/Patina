import {
  _electron as electron,
  expect,
  test,
  type ElectronApplication,
  type Locator,
  type Page
} from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { ending, leave, pick } from './projects';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
const bench = resolve(__dirname, '../../../../engine/tools/Patina.Bench/bin/Release/net11.0/patina-bench.dll');
const shots = resolve(__dirname, '../../../dist/e2e-results');
const Name = 'Rust Plate';
const File = 'Rust_Plate.vxtexgraph';
const Nodes = 6;
const OnePixelPng = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4z8AAAAMBAQDJ/pLvAAAAAElFTkSuQmCC';

let scratch: string;
let library: string;
let app: ElectronApplication;
let window: Page;

test.beforeEach(async () => {
  scratch = mkdtempSync(join(tmpdir(), 'patina-e2e-graph-'));
  library = join(scratch, 'Generated.ptlib');
  execFileSync('dotnet', [bench, 'generate', library, '10'], { stdio: 'inherit' });
  execFileSync('dotnet', [bench, 'graph', library, String(Nodes), Name], { stdio: 'inherit' });
  app = await electron.launch({
    args: [main],
    env: { ...process.env, PATINA_USER_DATA: join(scratch, 'user-data'), PATINA_LIBRARY: library }
  });
  window = await app.firstWindow();
  await expect(window.getByTestId('library-count')).toHaveText(/projects?$/);
});

test.afterEach(async () => {
  // A test may leave edits unsaved, and quitting would then wait for an answer nobody gives.
  await leave(app);
  rmSync(scratch, { recursive: true, force: true, maxRetries: 20, retryDelay: 250 });
});

const nodes = () => window.getByTestId('graph-node');
const node = (id: number) => window.locator(`[data-testid=graph-node][data-node="${String(id)}"]`);
const thumbnail = (id: number) => node(id).locator('ptn-graph-thumbnail');

async function openGraph(): Promise<void> {
  await window.getByRole('radio', { name: 'Materials' }).click();
  await window.getByTestId('library-search').fill(Name);
  await expect(window.getByTestId('library-row')).toHaveCount(1);
  await window.getByTestId('library-row').click();
  await window.getByTestId('open-graph').click();
  await expect(nodes()).toHaveCount(Nodes);
}

/** A thumbnail's hash, once it has one. */
async function hashOf(element: Locator): Promise<string> {
  await expect(element).toHaveAttribute('data-hash', /^[0-9a-f]{64}$/);
  return (await element.getAttribute('data-hash')) ?? '';
}

async function centre(element: Locator): Promise<{ x: number; y: number }> {
  const box = await element.boundingBox();
  if (!box) {
    throw new Error('It is not on screen.');
  }

  return { x: box.x + box.width / 2, y: box.y + box.height / 2 };
}

/** A press on one point, a move and a release on another: what a wire or a node drag is. */
async function dragBetween(from: { x: number; y: number }, to: { x: number; y: number }): Promise<void> {
  await window.mouse.move(from.x, from.y);
  await window.mouse.down();
  await window.mouse.move((from.x + to.x) / 2, (from.y + to.y) / 2, { steps: 4 });
  await window.mouse.move(to.x, to.y, { steps: 4 });
  await window.mouse.up();
}

const connector = (id: number, port: string) => node(id).locator(`[data-xui-graph-port="${port}"]`).getByRole('button');

async function addNode(search: string): Promise<void> {
  await window.getByTestId('graph-add').click();
  await window.getByRole('combobox').fill(search);
  await expect(window.getByRole('option').first()).toBeVisible();
  await window.keyboard.press('Enter');
}

/** The Server's address and token, for a second client. */
async function server(): Promise<{ baseUrl: string; token: string }> {
  return window.evaluate(() =>
    (
      globalThis as unknown as { patina: { connection(): Promise<{ baseUrl: string; token: string }> } }
    ).patina.connection()
  );
}

test('opens a graph from the Library, edits it, and sees live thumbnails', async () => {
  await expect(window.getByTestId('start-texturing')).toBeVisible();
  await openGraph();
  await expect(window.getByTestId('graph-name')).toHaveText(Name);
  await expect(window.getByTestId('active-unsaved')).toHaveCount(0);

  // The chain's previews arrive by hash, each over a fetch with the token.
  const before = await hashOf(thumbnail(2));
  await hashOf(thumbnail(1));
  await window.screenshot({ path: join(shots, 'graph-opened.png') });

  // Add a node: it lands selected, and the Inspector follows it.
  await addNode('Grayscale');
  await expect(nodes()).toHaveCount(Nodes + 1);
  await expect(window.getByTestId('inspector-title')).toHaveText('Grayscale');
  await expect(window.getByTestId('active-unsaved')).toBeVisible();
  const added = Nodes + 1;

  // Connect it: a wire from the noise, through a port, shows up as a thumbnail of its own.
  await dragBetween(await centre(connector(1, 'Out')), await centre(connector(added, 'Input')));
  await hashOf(thumbnail(added));

  // Change a value in the Inspector, and the thumbnails downstream change.
  await node(1).locator('xui-graph-node-preview').click();
  await expect(window.getByTestId('inspector-title')).toHaveText('Noise');
  await window.getByTestId('field-Scale').getByRole('spinbutton').fill('31');
  await window.getByTestId('field-Scale').getByRole('spinbutton').press('Enter');
  await expect(thumbnail(2)).not.toHaveAttribute('data-hash', before);
  const changed = await hashOf(thumbnail(2));

  // The slider: a drag across it is one undo step, however many batches it sent.
  // A tiling noise's Scale moves in whole cells, so it takes enough steps to reach the next one.
  await window.getByTestId('field-Scale').getByRole('slider').focus();
  for (let step = 0; step < 24; step++) {
    await window.keyboard.press('ArrowRight');
  }

  await expect(thumbnail(2)).not.toHaveAttribute('data-hash', changed);
  await window.getByTestId('field-Scale').getByRole('slider').blur();

  await window.getByTestId('app-undo').click();
  await expect(thumbnail(2)).toHaveAttribute('data-hash', changed);

  // Undo takes the value back, and the old picture with it. A preview already seen is not evaluated twice.
  await window.getByTestId('app-undo').click();
  await expect(thumbnail(2)).toHaveAttribute('data-hash', before);
  await window.getByTestId('app-redo').click();
  await expect(thumbnail(2)).toHaveAttribute('data-hash', changed);

  // Saving reaches the file.
  await window.getByTestId('app-save').click();
  await expect(window.getByTestId('active-unsaved')).toHaveCount(0);
  const saved = readFileSync(join(library, 'projects/graphs', File), 'utf8');
  expect(saved).toContain('Colour/Grayscale');
  expect(saved).toContain('Scale');

  await window.screenshot({ path: join(shots, 'graph-edited.png') });
});

test('moves, deletes and shows a second client without a reload', async () => {
  await openGraph();

  // A drag is one undo step, and the Server has the node where it was dropped.
  const start = await centre(node(3).locator('xui-graph-node-header, [data-xui-graph-node-header]').first());
  await dragBetween(start, { x: start.x + 60, y: start.y + 90 });
  await expect(window.getByTestId('app-undo')).toHaveAttribute('title', /Move/);
  const moved = await centre(node(3));
  await window.getByTestId('app-undo').click();
  await expect.poll(async () => (await centre(node(3))).y).toBeLessThan(moved.y);

  // Delete takes the node and its wires.
  await node(4).locator('xui-graph-node-preview').click();
  await expect(window.getByTestId('inspector-title')).toHaveText('HSL');
  await window.keyboard.press('Delete');
  await expect(nodes()).toHaveCount(Nodes - 1);
  await window.getByTestId('app-undo').click();
  await expect(nodes()).toHaveCount(Nodes);

  // Another client's edit arrives as a delta.
  const { baseUrl, token } = await server();
  const headers = { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' };
  const open = (await (await fetch(`${baseUrl}/api/v1/graphs`, { headers })).json()) as { id: string }[];
  const reply = await fetch(`${baseUrl}/api/v1/graphs/${open[0]?.id ?? ''}/commands`, {
    method: 'POST',
    headers,
    body: JSON.stringify({ commands: [{ op: 'addNode', type: 'Colour/Invert', x: 0, y: 400 }], merge: false })
  });
  expect(reply.ok).toBe(true);
  await expect(nodes()).toHaveCount(Nodes + 1);
});

test('the Library selection takes the Inspector back from a node', async () => {
  await openGraph();
  await node(1).locator('xui-graph-node-preview').click();
  await expect(window.getByTestId('inspector-title')).toHaveText('Noise');
  await expect(window.getByTestId('texture-image')).toBeVisible();

  await window.getByTestId('library-search').fill('');
  await expect(window.getByTestId('library-row').nth(1)).toBeVisible();
  await window.getByTestId('library-row').first().click();
  await expect(window.getByTestId('inspector-title')).not.toHaveText('Noise');
});

test('a killed Worker costs nothing: the next one is sent the document', async () => {
  // A new Worker starts and is sent the document: well past 120 s on CI.
  test.setTimeout(240_000);
  await openGraph();
  await hashOf(thumbnail(2));
  const { baseUrl, token } = await server();
  const statuses = await window.evaluate(() =>
    (
      globalThis as unknown as { patina: { sidecars(): Promise<{ name: string; pid: number | null }[]> } }
    ).patina.sidecars()
  );
  const worker = statuses.find(status => status.name === 'worker')?.pid ?? 0;
  expect(worker).toBeGreaterThan(0);

  process.kill(worker, 'SIGKILL');
  await expect
    .poll(async () => {
      const reply = await fetch(`${baseUrl}/api/v1/system`, { headers: { Authorization: `Bearer ${token}` } });
      return ((await reply.json()) as { worker: { connected: boolean } }).worker.connected;
    })
    .toBe(true);
  await expect(nodes()).toHaveCount(Nodes);

  // The new Worker has nothing, and is sent the document.
  await node(1).locator('xui-graph-node-preview').click();
  const before = await hashOf(thumbnail(2));
  await window.getByTestId('field-Scale').getByRole('spinbutton').fill('5');
  await window.getByTestId('field-Scale').getByRole('spinbutton').press('Enter');
  await expect(thumbnail(2)).not.toHaveAttribute('data-hash', before, { timeout: 30_000 });
});

/** Opens the palette at a place on the canvas, so that a new node does not land on the last. */
async function addNodeAt(search: string, x: number, y: number): Promise<void> {
  await window.getByTestId('graph-canvas').click({ button: 'right', position: { x, y } });
  await window.locator('input[role=combobox]').fill(search);
  await expect(window.getByRole('option').first()).toBeVisible();
  await window.keyboard.press('Enter');
  // Its backdrop would take the next press.
  await expect(window.locator('input[role=combobox]')).toHaveCount(0);
  // It lands selected once the Server has it, and the next node is wired to what is selected then: one added sooner
  // takes the node before this one.
  await expect(window.getByTestId('inspector-title')).toHaveText(search.split('/').at(-1) ?? search);
}

async function newGraph(name: string, template?: string): Promise<void> {
  await window.getByRole('radio', { name: 'Materials' }).click();
  await window.getByTestId('library-new-graph').click();
  await window.getByTestId('new-graph-name').fill(name);
  if (template) {
    await window.getByTestId('new-graph-template').getByRole('combobox').click();
    await expect(window.getByRole('option', { name: template })).toBeVisible();
    await window.screenshot({ path: join(shots, 'graph-new-templates.png') });
    await window.getByRole('option', { name: template }).click();
  }

  await window.getByTestId('new-graph-create').click();
  await expect(window.getByTestId('graph-name')).toHaveText(name);
}

const inspect = (id: number) => node(id).locator('xui-graph-node-preview').click();

test('builds a tiling material from a new graph, down to its channels and full size', async () => {
  await newGraph('Tiling Rust');
  await expect(nodes()).toHaveCount(0);

  // The file is written and the scan finds it, without opening anything by hand.
  await window.getByRole('radio', { name: 'Materials' }).click();
  await window.getByTestId('library-search').fill('Tiling Rust');
  await expect(window.getByText('Tiling Rust').first()).toBeVisible();

  // Settings: smaller, and a seed of its own.
  await window.getByTestId('graph-settings').click();
  await expect(window.getByTestId('inspector-title')).toHaveText('Tiling Rust');
  await window.getByTestId('setting-size').getByRole('combobox').click();
  await window.getByRole('option', { name: '512' }).click();
  await window.screenshot({ path: join(shots, 'graph-settings-size.png') });
  await window.getByTestId('setting-seed').getByRole('spinbutton').fill('11');
  await window.getByTestId('setting-seed').getByRole('spinbutton').press('Enter');
  await expect(window.getByTestId('active-unsaved')).toBeVisible();

  // Tiling is the graph's now, and a new one tiles.
  await expect(window.getByTestId('setting-tilingX').getByRole('switch')).toBeChecked();
  await expect(window.getByTestId('setting-tilingY').getByRole('switch')).toBeChecked();

  await addNodeAt('Source/Noise', 20, 20);
  await addNodeAt('Colour/Curve', 220, 20);
  await addNodeAt('Colour/Gradient Map', 420, 20);
  await addNodeAt('Output/Output', 620, 20);
  await expect(nodes()).toHaveCount(4);

  // Each node lands selected, so the next one takes it: the chain is wired as it is made, and nothing is left unwired.
  const unwired = window
    .getByTestId('graph-diagnostics')
    .getByRole('listitem')
    .filter({ hasText: 'has no image connected' });
  await expect.poll(wiresNow).toEqual(['1>2', '2>3', '3>4']);
  await expect(unwired).toHaveCount(0);

  const plain = await hashOf(thumbnail(3));

  // The curve: a press in the middle bends it, and the picture after it changes.
  await inspect(2);
  const curve = window.getByTestId('curve-editor');
  const box = await curve.boundingBox();
  if (!box) {
    throw new Error('The curve is not on screen.');
  }

  await dragBetween(
    { x: box.x + box.width * 0.5, y: box.y + box.height * 0.5 },
    { x: box.x + box.width * 0.5, y: box.y + box.height * 0.2 }
  );
  await expect(thumbnail(3)).not.toHaveAttribute('data-hash', plain);
  const curved = await hashOf(thumbnail(3));

  // The ramp: a stop in the middle, in a rust colour.
  await inspect(3);
  const ramp = window.getByTestId('gradient-ramp');
  await ramp.click({ position: { x: 60, y: 8 } });
  // The press makes Stop 2 and selects it, and the position field shows it on the next render. A fill before that
  // selects the old stop's text, the render replaces it, and the typing lands after it: 0.2130.45, which is no number.
  const added = window.getByRole('slider', { name: 'Stop 2' });
  await expect(added).toHaveClass(/border-foreground/);
  await expect(window.getByTestId('field-Ramp').getByRole('spinbutton')).toHaveValue(
    (await added.getAttribute('aria-valuenow')) ?? ''
  );
  await window.getByTestId('field-Ramp').getByRole('spinbutton').fill('0.45');
  await window.getByTestId('field-Ramp').getByRole('spinbutton').press('Enter');
  await expect(window.getByRole('slider', { name: 'Stop 2' })).toHaveAttribute('aria-valuenow', '0.45');
  await window.getByTestId('field-Ramp').getByRole('button', { name: /^#/ }).click();
  await window.getByLabel('Hex color').fill('#A0522DFF');
  await window.getByLabel('Hex color').press('Enter');
  await window.keyboard.press('Escape');
  await expect(window.getByRole('slider', { name: 'Stop 2' })).toHaveAttribute('aria-valuenow', '0.45');
  await expect(thumbnail(3)).not.toHaveAttribute('data-hash', curved);
  await expect(window.getByTestId('graph-evaluation')).toHaveText(/Updated in \d+ ms|Up to date/);

  // The Texture pane: the engine's histogram of the full picture, a channel, and the real size without asking for it.
  await expect(window.getByTestId('texture-histogram-label')).toHaveText(
    'Histogram of the 512 × 512 picture, counted on the GPU'
  );
  await expect(window.getByTestId('texture-size')).toHaveText('512 × 512');
  await window.getByRole('radio', { name: 'R', exact: true }).click();
  await expect(window.getByTestId('texture-channel')).toBeVisible();
  await window.screenshot({ path: join(shots, 'graph-channel.png') });
  await window.getByRole('radio', { name: 'RGBA' }).click();
  await window.screenshot({ path: join(shots, 'graph-tiling-material.png') });

  await window.getByTestId('app-save').click();
  await expect(window.getByTestId('active-unsaved')).toHaveCount(0);

  // The file is the truth: everything above is in it.
  const file = readFileSync(join(library, 'projects', 'tiling-rust', 'tiling-rust.vxtexgraph'), 'utf8');
  expect(file).toContain('ramp:0 000000ff,0.45 a0522dff,1 ffffffff');
  expect(file).toMatch(/curve:r=0 0,0\.5 \d\.\d+,1 1;/);
  expect(file).toContain("size: '512'");
  expect(file).toContain("baseWidth: '1024'");
});

test('exposes a parameter, drives a number from it and edits its default', async () => {
  await openGraph();
  const before = await hashOf(thumbnail(1));
  await window.getByTestId('graph-settings').click();
  await window.getByTestId('parameter-add').click();
  const parameter = window.getByTestId('parameter-Parameter1');
  await expect(parameter).toBeVisible();

  await inspect(1);
  await window.getByTestId('expression-toggle-Scale').click();
  await window.getByTestId('expression-Scale').fill('Parameter1 * 20 + 1');
  await window.getByTestId('expression-Scale').press('Enter');
  await expect(thumbnail(1)).not.toHaveAttribute('data-hash', before);
  const driven = await hashOf(thumbnail(1));

  await window.getByTestId('graph-settings').click();
  await parameter.getByRole('spinbutton').fill('0.9');
  await parameter.getByRole('spinbutton').press('Enter');
  await expect(thumbnail(1)).not.toHaveAttribute('data-hash', driven);

  await window.getByTestId('app-undo').click();
  await expect(thumbnail(1)).toHaveAttribute('data-hash', driven);
});

/** The wires the Server holds, as `from>to`. */
async function wiresNow(): Promise<string[]> {
  const { baseUrl, token } = await server();
  const headers = { Authorization: `Bearer ${token}` };
  const open = (await (await fetch(`${baseUrl}/api/v1/graphs`, { headers })).json()) as { id: string }[];
  const reply = await fetch(`${baseUrl}/api/v1/graphs/${open[0]?.id ?? ''}`, { headers });
  const state = (await reply.json()) as { edges: { fromNode: number; toNode: number }[] };
  return state.edges.map(edge => `${String(edge.fromNode)}>${String(edge.toNode)}`).sort();
}

test('the palette adds what was typed, wired to the selection, into a wire, or from a dropped wire', async () => {
  await openGraph();

  // Enter straight after typing adds Levels, not Auto Levels, and it takes the selected Noise.
  await node(1).locator('xui-graph-node-preview').click();
  await window.getByTestId('graph-add').click();
  await window.locator('input[role=combobox]').fill('Levels');
  await window.keyboard.press('Enter');
  await expect(nodes()).toHaveCount(Nodes + 1);
  await expect(window.getByTestId('inspector-title')).toHaveText('Levels');
  await expect.poll(wiresNow).toContain(`1>${String(Nodes + 1)}`);
  // Beside the Noise, and off the HSL that already sits there.
  const levels = (await node(Nodes + 1).boundingBox()) ?? { x: 0, y: 0, height: 0 };
  const hsl = (await node(2).boundingBox()) ?? { x: 0, y: 0, height: 0 };
  expect(levels.y >= hsl.y + hsl.height || levels.y + levels.height <= hsl.y).toBe(true);

  // A selected wire takes a node into its middle, and Space opens the palette at the pointer without the canvas
  // having focus.
  const wire = {
    x: ((await centre(connector(2, 'Out'))).x + (await centre(connector(3, 'Input'))).x) / 2,
    y: ((await centre(connector(2, 'Out'))).y + (await centre(connector(3, 'Input'))).y) / 2
  };
  await window.mouse.click(wire.x, wire.y);
  await window.getByTestId('graph-name').click();
  await window.mouse.move(wire.x, wire.y + 40);
  await window.keyboard.press(' ');
  await window.locator('input[role=combobox]').fill('Invert');
  await window.keyboard.press('Enter');
  await expect(nodes()).toHaveCount(Nodes + 2);
  const inserted = String(Nodes + 2);
  await expect.poll(wiresNow).toEqual(expect.arrayContaining([`2>${inserted}`, `${inserted}>3`]));
  expect(await wiresNow()).not.toContain('2>3');
  const landed = await centre(node(Nodes + 2));
  expect(Math.abs(landed.y - (wire.y + 40))).toBeLessThan(120);

  // A wire dropped on nothing offers what can take it, and is wired to what is chosen.
  const out = await centre(connector(5, 'Out'));
  await dragBetween(out, { x: out.x + 40, y: out.y + 200 });
  await window.locator('input[role=combobox]').fill('Grayscale');
  await window.keyboard.press('Enter');
  await expect(nodes()).toHaveCount(Nodes + 3);
  await expect.poll(wiresNow).toContain(`5>${String(Nodes + 3)}`);
  await window.screenshot({ path: join(shots, 'graph-palette-wired.png') });
});

test('the 2D view shows the node on view at full size, tiles it and pins it, and the viewport shows it alone', async () => {
  test.setTimeout(120_000);
  await openGraph();
  await node(3).locator('xui-graph-node-preview').click();
  await expect(window.getByTestId('texture-size')).toHaveText('256 × 256');
  const inverted = await hashOf(thumbnail(3));
  await expect(window.getByTestId('texture-image')).toHaveAttribute('data-hash', inverted);

  // Three by three, with the texel under the pointer read out.
  await window.getByTestId('texture-tile').click();
  await expect(window.getByTestId('texture-image')).toHaveCount(9);
  await window.getByTestId('texture-view').hover();
  await expect(window.getByTestId('texture-texel')).toHaveText(/^\d+, \d+ {2}(\d\.\d{3} ?){4}$/);
  await window.screenshot({ path: join(shots, 'graph-2d-tiled.png') });
  await window.getByTestId('texture-tile').click();

  // Pinned, it stays on the Invert while the Noise is chosen, and follows the selection again once let go.
  await window.getByTestId('texture-pin').click();
  await node(1).locator('xui-graph-node-preview').click();
  await expect(window.getByTestId('inspector-title')).toHaveText('Noise');
  await expect(window.getByTestId('texture-image')).toHaveAttribute('data-hash', inverted);
  await window.getByTestId('texture-pin').click();
  await expect(window.getByTestId('texture-image')).toHaveAttribute('data-hash', await hashOf(thumbnail(1)));

  // The viewport shows the node alone on a sphere, then on a plane, and the material again.
  await window.getByRole('tab', { name: 'Preview' }).click();
  await window.getByTestId('viewport-show-node').click();
  await expect(window.getByTestId('viewport-mesh')).toHaveText('Sphere.glb', { timeout: 30_000 });
  await expect(window.getByTestId('viewport-view').getByRole('combobox')).toBeDisabled();
  // The Server lays the node on the shape and the frames say so, so a frame drawn after the plane is chosen is one
  // of the plane: the count is read before it is chosen.
  const viewport = window.getByRole('application', { name: 'Viewport' });
  const drawn = Number(await viewport.getAttribute('data-frames'));
  await window.getByTestId('viewport-display-toggle').click();
  await pick(window, 'viewport-shape', 'Plane');
  await expect(window.getByTestId('viewport-mesh')).toHaveText('Plane.glb');
  await window.getByTestId('viewport-display-toggle').click();
  await expect(window.getByTestId('viewport-display')).toHaveCount(0);
  await expect
    .poll(async () => Number(await viewport.getAttribute('data-frames')), { timeout: 30_000 })
    .toBeGreaterThan(drawn);
  await window.screenshot({ path: join(shots, 'graph-node-on-plane.png') });
  await window.getByTestId('viewport-show-node').click();
  await expect(window.getByTestId('viewport-view').getByRole('combobox')).toBeEnabled();
});

test('shows a second graph beside the first, and the panes follow the one touched last (#40)', async () => {
  test.setTimeout(120_000);
  await openGraph();
  // A new graph can start from an Output node for each channel (#183).
  await newGraph('Beside Rust', 'Material Outputs');
  const Outputs = 6;
  await expect(nodes()).toHaveCount(Outputs);

  // The fourth layout puts a second graph beside the one in front, which joins an open tab's session.
  await window.keyboard.press('ControlOrMeta+4');
  const beside = window.getByTestId('graph-beside');
  await expect(beside.getByTestId('graph-beside-empty')).toBeVisible();
  await pick(window, 'graph-beside-choice', Name);
  await expect(beside.getByTestId('graph-node')).toHaveCount(Nodes);
  await expect(nodes()).toHaveCount(Outputs + Nodes);
  const besideNode = (id: number) => beside.locator(`[data-testid=graph-node][data-node="${String(id)}"]`);

  // Touching it puts it in front of the Inspector, and its edits are its tab's, behind.
  await besideNode(1).locator('xui-graph-node-preview').click();
  await expect(window.getByTestId('inspector-title')).toHaveText('Noise');
  await expect(beside.getByTestId('graph-beside-front')).toBeVisible();
  await window.getByTestId('field-Scale').getByRole('spinbutton').fill('31');
  await window.getByTestId('field-Scale').getByRole('spinbutton').press('Enter');
  await expect(window.getByTestId('tab-unsaved')).toBeVisible();
  await expect(window.getByTestId('active-unsaved')).toHaveCount(0);

  // Undo is the graph's touched last, and so is Copy; the graph in front, touched, takes the paste.
  await besideNode(1).locator('xui-graph-node-preview').click();
  await window.keyboard.press('ControlOrMeta+z');
  await expect(window.getByTestId('tab-unsaved')).toHaveCount(0);
  await window.keyboard.press('ControlOrMeta+c');
  await window.getByTestId('graph-name').first().click();
  await expect(beside.getByTestId('graph-beside-front')).toHaveCount(0);
  await window.keyboard.press('ControlOrMeta+v');
  await expect(nodes()).toHaveCount(Outputs + Nodes + 1);
  await expect(beside.getByTestId('graph-node')).toHaveCount(Nodes);
  await expect(window.getByTestId('inspector-title')).toHaveText('Noise');
  await expect(window.getByTestId('active-unsaved')).toBeVisible();
  await window.screenshot({ path: join(shots, 'graph-two-side-by-side.png') });
});

test('finds a node by name, stars a node for the palette, and adds one from several', async () => {
  await openGraph();

  // Find Node selects the node and shows it.
  await window.getByTestId('graph-name').click();
  await window.keyboard.press('ControlOrMeta+f');
  await window.locator('input[role=combobox]').fill('Inv');
  await expect(window.getByRole('dialog').getByRole('option').first()).toHaveText('Invert');
  await window.keyboard.press('Enter');
  await expect(window.getByTestId('inspector-title')).toHaveText('Invert');

  // A star in the Nodes list puts Blend first in the palette, under Favourites.
  await window.getByRole('tab', { name: 'Nodes' }).click();
  await window.getByTestId('node-library-search').fill('Blend');
  const blend = window.getByTestId('node-library-item').filter({ hasText: /^Blend$/ });
  await blend.hover();
  await blend.getByTestId('node-library-star').click();
  await window.getByTestId('node-library-search').fill('');
  await expect(window.getByTestId('node-library-group-Favourites')).toBeVisible();
  await expect(window.getByTestId('node-library-item').first()).toHaveText('Blend');

  // With the Noise and the Invert selected, the starred Blend takes them both, top first.
  await node(1).locator('xui-graph-node-preview').click();
  await node(3)
    .locator('xui-graph-node-preview')
    .click({ modifiers: ['Shift'] });
  await window.getByTestId('graph-add').click();
  await expect(window.getByRole('dialog').getByRole('option').first()).toHaveText('Blend');
  await window.keyboard.press('Enter');
  await expect(nodes()).toHaveCount(Nodes + 1);
  const added = String(Nodes + 1);
  await expect.poll(wiresNow).toEqual(expect.arrayContaining([`1>${added}`, `3>${added}`]));
  await window.screenshot({ path: join(shots, 'graph-added-from-several.png') });
});

test('the node library ranks what was typed, as the palette does, and shows each node drawn', async () => {
  await openGraph();

  await window.getByRole('tab', { name: 'Nodes' }).click();
  await window.getByTestId('node-library-search').fill('Levels');
  await expect(window.getByTestId('node-library-item').first()).toHaveText('Levels');
  await window.getByTestId('node-library-search').fill('Output');
  await expect(window.getByTestId('node-library-item').first()).toHaveText('Output');

  // Each node with a picture shows it, drawn by the engine (#183); an Output has none to show.
  await window.getByTestId('node-library-search').fill('Levels');
  await expect(window.getByTestId('node-library-item').first().getByTestId('node-library-picture')).toHaveJSProperty(
    'naturalWidth',
    64
  );
  await window.getByTestId('node-library-search').fill('Output');
  await expect(window.getByTestId('node-library-item').first().getByTestId('node-library-picture')).toHaveCount(0);
});

test('the node library adds a node, and a number becomes a parameter in one step', async () => {
  await openGraph();

  await window.getByRole('tab', { name: 'Nodes' }).click();
  await window.getByTestId('node-library-search').fill('Levels');
  await window.getByTestId('node-library-item').first().dblclick();
  await expect(nodes()).toHaveCount(Nodes + 1);

  await node(1).click();
  await window.getByTestId('expose-Scale').click();
  await expect(window.getByTestId('expression-Scale')).toHaveValue('Scale');
  await window.getByTestId('graph-settings').click();
  await expect(window.getByTestId('parameter-Scale')).toBeVisible();
});

test('groups nodes, keeps notes, and both survive a save and a reopen', async () => {
  await openGraph();
  await inspect(1);
  await window.getByTestId('graph-group').click();
  await expect(window.getByTestId('graph-frame')).toHaveCount(1);

  await window.getByTestId('graph-note').click();
  await expect(window.getByTestId('graph-note-card')).toHaveCount(1);
  await expect(window.getByTestId('inspector-title')).toHaveText('Note');
  await window.getByTestId('note-text').fill('Rust gathers in the seams');
  await window.getByTestId('note-text').press('Tab');
  await expect(window.getByTestId('graph-note-card')).toContainText('Rust gathers in the seams');

  await window.getByTestId('graph-settings').click();
  await window.getByTestId('group-row').getByLabel('Group title').fill('Base');
  await window.getByTestId('group-row').getByLabel('Group title').press('Tab');
  await expect(window.getByTestId('graph-frame')).toContainText('Base');

  await window.getByTestId('app-save').click();
  await expect(window.getByTestId('active-unsaved')).toHaveCount(0);
  await window.getByTestId('document-tab-close').last().click();
  await expect(window.getByTestId('start-texturing')).toBeVisible();
  await window.getByTestId('open-graph').click();
  await expect(window.getByTestId('graph-frame')).toContainText('Base');
  await expect(window.getByTestId('graph-note-card')).toContainText('Rust gathers in the seams');
});

test('instances a compound, opens it, and comes back along the breadcrumb', async () => {
  await openGraph();
  await addNodeAt('Patterns/Cells', 60, 60);
  const added = Nodes + 1;
  await hashOf(thumbnail(added));
  await expect(node(added)).toHaveAttribute('data-compound', 'Patterns/Cells');
  await expect(window.getByTestId('active-unsaved')).toBeVisible();

  await window.getByTestId('graph-open-compound').click();
  await expect(window.getByTestId('graph-name')).not.toHaveText(Name);
  await expect(window.getByTestId('graph-read-only')).toBeVisible();
  await expect(window.getByTestId('graph-add')).toBeDisabled();
  await expect(window.getByTestId('graph-crumb')).toHaveText(Name);
  await window.screenshot({ path: join(shots, 'graph-compound.png') });

  // Back: the outer graph is as it was left, edits and undo included.
  await window.getByTestId('graph-crumb').click();
  await expect(window.getByTestId('graph-name')).toHaveText(Name);
  await expect(window.getByTestId('graph-crumb')).toHaveCount(0);
  await expect(nodes()).toHaveCount(Nodes + 1);
  await expect(window.getByTestId('active-unsaved')).toBeVisible();
});

test('reads a bitmap from beside the graph, and says so when it cannot', async () => {
  await openGraph();
  writeFileSync(join(library, 'projects', 'graphs', 'dot.png'), Buffer.from(OnePixelPng, 'base64'));
  await addNodeAt('Source/Bitmap', 60, 60);
  const added = Nodes + 1;
  await window.getByTestId('field-Source').getByRole('textbox').fill('dot.png');
  await window.getByTestId('field-Source').getByRole('textbox').press('Enter');
  await hashOf(thumbnail(added));
  await expect(window.getByText("reads 'dot.png'")).toHaveCount(0);

  await window.getByTestId('field-Source').getByRole('textbox').fill('gone.png');
  await window.getByTestId('field-Source').getByRole('textbox').press('Enter');
  // A person reads what is wrong, and the code waits behind Details for whoever reports it.
  await expect(window.getByTestId('graph-diagnostics')).toContainText("reads 'gone.png'");
  await expect(window.getByText('PT0001')).toHaveCount(0);
  await window.getByTestId('graph-diagnostics-details').click();
  await expect(window.getByText('PT0001')).toBeVisible();
  await window.screenshot({ path: join(shots, 'graph-diagnostics-details.png') });
});

test('the minimap leaves a small canvas, and steps aside for a wire on a large one', async () => {
  await app.evaluate(({ BrowserWindow }) => {
    BrowserWindow.getAllWindows()[0]?.setContentSize(1000, 460);
  });
  await newGraph('Under The Minimap');
  await expect(window.getByTestId('graph-canvas')).toBeVisible();
  await expect(window.getByTestId('graph-minimap')).toBeHidden();

  await app.evaluate(({ BrowserWindow }) => {
    BrowserWindow.getAllWindows()[0]?.setContentSize(1600, 1300);
  });
  await expect(window.getByTestId('graph-minimap')).toBeVisible();
  const box = await window.getByTestId('graph-canvas').boundingBox();
  if (!box) {
    throw new Error('The canvas is not on screen.');
  }

  // A node's input sits below where it was asked for, which puts the Output's under the minimap's corner. The Noise
  // stands higher: with its preview it is taller than 150 pixels, and asked for there its output port was below the
  // canvas, where no press can start a wire.
  // Nothing selected before the Output, or it would wire itself to the Noise and the drop below would prove nothing.
  await addNodeAt('Source/Noise', box.width - 500, box.height - 450);
  await window.getByTestId('graph-canvas').click({ position: { x: 40, y: 40 } });
  await addNodeAt('Output/Output', box.width - 100, box.height - 150);
  await expect(nodes()).toHaveCount(2);
  await expect(window.getByTestId('graph-diagnostics').getByText(/has no image connected/u)).toHaveCount(1);
  const target = await centre(connector(2, 'Input'));

  // The instrument: the minimap really is over the port, so the drop below proves something.
  const covered = await window.evaluate(
    ({ x, y }) => document.elementFromPoint(x, y)?.closest('[data-testid=graph-minimap]') !== null,
    target
  );
  expect(covered).toBe(true);

  await dragBetween(await centre(connector(1, 'Out')), target);
  await expect(window.getByTestId('graph-diagnostics').getByText(/has no image connected/u)).toHaveCount(0);
  await expect(window.getByTestId('graph-minimap')).toBeVisible();
});

test('saves a selection as a compound, opens it, authors its ports, and undoes the save', async () => {
  await openGraph();
  const after = await hashOf(thumbnail(Nodes - 1));
  await inspect(2);
  await node(3)
    .locator('xui-graph-node-preview')
    .click({ modifiers: ['Shift'] });
  await expect(window.getByTestId('inspector-title')).toHaveText('2 nodes');

  await window.getByTestId('graph-save-compound').click();
  await window.getByTestId('save-compound-name').fill('bad:name');
  await window.getByTestId('save-compound-save').click();
  await expect(window.getByTestId('save-compound-refused')).toBeVisible();
  await window.getByTestId('save-compound-name').fill('Wear/Tinted');
  await window.getByTestId('save-compound-save').click();

  // Two nodes became one, which is selected, and the picture after it is the same picture.
  await expect(nodes()).toHaveCount(Nodes - 1);
  const held = window.locator('[data-testid=graph-node][data-compound="Wear/Tinted"]');
  await expect(held).toHaveCount(1);
  await expect(thumbnail(Nodes - 1)).toHaveAttribute('data-hash', after);
  const file = join(library, 'projects', 'graphs', 'Compounds', 'Wear', 'Tinted.vxtexgraph');
  expect(readFileSync(file, 'utf8')).toContain('Sub-graph/Input');

  await window.getByTestId('graph-open-compound').click();
  await expect(window.getByTestId('graph-name')).toHaveText('Tinted');
  await hashOf(thumbnail(2)); // its author sees it, though nothing feeds its input here
  await window.getByTestId('graph-settings').click();
  await window.getByTestId('port-add-input').click();
  await expect(window.getByTestId('port-Input2')).toBeVisible();
  await window.getByTestId('port-Input2').getByLabel('Port name').fill('Amount');
  await window.getByTestId('port-Input2').getByLabel('Port name').press('Enter');
  await expect(window.getByTestId('port-Amount')).toBeVisible();
  await expect(window.locator('[data-xui-graph-port="Amount"]')).toHaveCount(1);
  await window.getByTestId('app-save').click();
  // The tab keeps its mark: the graph outside, which the save as a compound changed, is unsaved still.
  await expect.poll(() => readFileSync(file, 'utf8')).toContain('name: Amount');
  await window.screenshot({ path: join(shots, 'graph-compound-interface.png') });

  // Undoing the save brings the nodes back, and the file stays for whoever else holds it.
  await window.getByTestId('graph-crumb').click();
  await expect(window.getByTestId('graph-name')).toHaveText(Name);
  await window.getByTestId('app-undo').click();
  await expect(nodes()).toHaveCount(Nodes);
  await expect(thumbnail(Nodes - 1)).toHaveAttribute('data-hash', after);
  expect(existsSync(file)).toBe(true);
});

test('copies a shipped compound beside the graph so that it can be edited', async () => {
  await openGraph();
  await addNodeAt('Patterns/Cells', 60, 60);
  await hashOf(thumbnail(Nodes + 1));
  await window.getByTestId('graph-open-compound').click();
  await expect(window.getByTestId('graph-read-only')).toBeVisible();

  await window.getByTestId('graph-copy-compound').click();
  await expect(window.getByTestId('graph-read-only')).toHaveCount(0);
  await expect(window.getByTestId('graph-name')).toHaveText('Cells Copy');
  await expect(window.getByTestId('graph-add')).toBeEnabled();
  expect(existsSync(join(library, 'projects', 'graphs', 'Compounds', 'Patterns', 'Cells Copy.vxtexgraph'))).toBe(true);
});

test('draws the number wires of a compound, and unmakes and remakes one', async () => {
  await openGraph();
  await addNodeAt('Patterns/Brick', 60, 60);
  await hashOf(thumbnail(Nodes + 1));
  await window.getByTestId('graph-open-compound').click();
  await window.getByTestId('graph-copy-compound').click();
  await expect(window.getByTestId('graph-name')).toHaveText('Brick Copy');

  // Columns and Rows reach the Tile's repeats, and a wired number has a port and no control.
  const tile = 2;
  const inputs = 4;
  await expect(connector(tile, 'Repeat X')).toBeVisible();
  await expect(connector(tile, 'Repeat Y')).toBeVisible();

  // Drawn means seen: four wires, each with a colour, since a wire in an unset variable has a stroke of none.
  const wires = window.locator('xui-node-graph svg path[stroke^="var(--color-chart"]');
  await expect(wires).toHaveCount(4);
  const strokes = await wires.evaluateAll(paths => paths.map(path => getComputedStyle(path).stroke));
  expect(strokes.filter(stroke => stroke === 'none')).toEqual([]);
  expect(new Set(strokes).size).toBe(2); // images and integers
  await window.screenshot({ path: join(shots, 'graph-number-wires.png') });
  await node(tile).locator('xui-graph-node-preview').click();
  await expect(window.getByTestId('wired-Repeat X')).toContainText('Columns');

  // Unmake it from the Inspector: the port goes with the wire, and a number has its control back.
  await window.getByTestId('wired-Repeat X').getByRole('button').click();
  await expect(connector(tile, 'Repeat X')).toHaveCount(0);
  await expect(window.getByTestId('field-Repeat X').getByRole('spinbutton')).toBeVisible();

  // Remake it from the port the Inspector shows.
  await window.getByTestId('port-Repeat X').click();
  await dragBetween(await centre(connector(inputs, 'Columns')), await centre(connector(tile, 'Repeat X')));
  await expect(window.getByTestId('wired-Repeat X')).toContainText('Columns');
  await window.screenshot({ path: join(shots, 'graph-number-wire-remade.png') });
});

test('a graph computes at 32 bits from its settings, and the file says so', async () => {
  await openGraph();
  await hashOf(thumbnail(1));
  await window.getByTestId('graph-settings').click();
  await window.getByTestId('setting-bitDepth').getByRole('combobox').click();
  await window.getByRole('option', { name: '32' }).click();
  await expect(window.getByTestId('active-unsaved')).toBeVisible();
  await expect(window.getByTestId('graph-evaluation')).toHaveText(/Updated in \d+ ms|Up to date/);
  await hashOf(thumbnail(1));
  await window.getByTestId('app-save').click();
  await expect(window.getByTestId('active-unsaved')).toHaveCount(0);
  expect(readFileSync(join(library, 'projects', 'graphs', File), 'utf8')).toContain("bitDepth: '32'");
});

test('Undo, Redo and Save work from the Inspector, and several nodes say so', async () => {
  await openGraph();
  await inspect(2);
  const title = await window.getByTestId('inspector-title').textContent();
  const slider = window.locator('[data-testid^=field-]').getByRole('slider').first();
  await slider.focus();
  await window.keyboard.press('ArrowRight');
  await expect(window.getByTestId('active-unsaved')).toBeVisible();

  await window.keyboard.press('ControlOrMeta+s');
  await expect(window.getByTestId('active-unsaved')).toHaveCount(0);
  await window.keyboard.press('ArrowRight');
  await expect(window.getByTestId('active-unsaved')).toBeVisible();
  await window.keyboard.press('ControlOrMeta+z');
  await expect(window.getByTestId('app-redo')).toBeEnabled();
  await expect(window.getByTestId('active-unsaved')).toHaveCount(0);

  // Pressed together, each before the last has come back: a key waits for the edit in flight, not dropped (#223).
  // Back past the save, then forward twice, ends somewhere none of them passed through: unsaved, nothing to redo.
  await window.keyboard.press('ControlOrMeta+z');
  await window.keyboard.press('ControlOrMeta+Shift+z');
  await window.keyboard.press('ControlOrMeta+Shift+z');
  await expect(window.getByTestId('app-redo')).toBeDisabled();
  await expect(window.getByTestId('active-unsaved')).toBeVisible();
  expect(title).not.toBe('');

  await inspect(1);
  await node(3)
    .locator('xui-graph-node-preview')
    .click({ modifiers: ['Shift'] });
  await expect(window.getByTestId('inspector-title')).toHaveText('2 nodes');
  await expect(window.getByTestId('selection-list').getByRole('listitem')).toHaveCount(2);
});

test('asks before quitting with unsaved edits', async () => {
  await openGraph();
  await addNode('Grayscale');
  await expect(window.getByTestId('active-unsaved')).toBeVisible();

  void app.evaluate(({ app: electronApp }) => {
    electronApp.quit();
  });
  await expect(window.getByTestId('app-quitting')).toBeVisible();
  await window.getByRole('button', { name: 'Cancel' }).click();
  await expect(window.getByTestId('app-quitting')).toHaveCount(0);
  await expect(window.getByTestId('graph-name')).toHaveText(Name);

  const closed = ending(app);
  void app.evaluate(({ app: electronApp }) => {
    electronApp.quit();
  });
  await expect(window.getByTestId('app-quitting')).toBeVisible();
  await window.getByRole('button', { name: "Don't Save" }).click();
  await closed;
});
