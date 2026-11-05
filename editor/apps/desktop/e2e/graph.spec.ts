import { _electron as electron, expect, test, type ElectronApplication, type Locator, type Page } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
const bench = resolve(__dirname, '../../../../engine/tools/Patina.Bench/bin/Release/net10.0/patina-bench.dll');
const shots = resolve(__dirname, '../../../dist/e2e-results');
const Name = 'Rust Plate';
const File = 'Rust_Plate.vxtexgraph';
const Nodes = 6;

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
  await app.close();
  rmSync(scratch, { recursive: true, force: true });
});

const nodes = () => window.getByTestId('graph-node');
const node = (id: number) => window.locator(`[data-testid=graph-node][data-node="${String(id)}"]`);
const thumbnail = (id: number) => node(id).locator('ptn-graph-thumbnail');

async function openGraph(): Promise<void> {
  await window.getByRole('radio', { name: 'Shared Assets' }).click();
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

const connector = (id: number, port: string) =>
  node(id).locator(`[data-xui-graph-port="${port}"]`).getByRole('button');

async function addNode(search: string): Promise<void> {
  await window.getByTestId('graph-add').click();
  await window.getByRole('combobox').fill(search);
  await expect(window.getByRole('option').first()).toBeVisible();
  await window.keyboard.press('Enter');
}

/** The Server's address and token, for a second client. */
async function server(): Promise<{ baseUrl: string; token: string }> {
  return window.evaluate(() =>
    (globalThis as unknown as { patina: { connection(): Promise<{ baseUrl: string; token: string }> } }).patina.connection()
  );
}

test('opens a graph from the Library, edits it, and sees live thumbnails', async () => {
  await expect(window.getByTestId('graph-none')).toBeVisible();
  await openGraph();
  await expect(window.getByTestId('graph-name')).toHaveText(Name);
  await expect(window.getByTestId('graph-unsaved')).toHaveCount(0);

  // The chain's previews arrive by hash, each over a fetch with the token.
  const before = await hashOf(thumbnail(2));
  await hashOf(thumbnail(1));
  await window.screenshot({ path: join(shots, 'graph-opened.png') });

  // Add a node: it lands selected, and the Inspector follows it.
  await addNode('Grayscale');
  await expect(nodes()).toHaveCount(Nodes + 1);
  await expect(window.getByTestId('inspector-title')).toHaveText('Grayscale');
  await expect(window.getByTestId('graph-unsaved')).toBeVisible();
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
  await window.getByTestId('field-Scale').getByRole('slider').focus();
  await window.keyboard.press('ArrowRight');
  await window.keyboard.press('ArrowRight');
  await window.keyboard.press('ArrowRight');
  await expect(thumbnail(2)).not.toHaveAttribute('data-hash', changed);
  await window.getByTestId('field-Scale').getByRole('slider').blur();

  await window.getByTestId('graph-undo').click();
  await expect(thumbnail(2)).toHaveAttribute('data-hash', changed);

  // Undo takes the value back, and the old picture with it. A preview already seen is not evaluated twice.
  await window.getByTestId('graph-undo').click();
  await expect(thumbnail(2)).toHaveAttribute('data-hash', before);
  await window.getByTestId('graph-redo').click();
  await expect(thumbnail(2)).toHaveAttribute('data-hash', changed);

  // Saving reaches the file.
  await window.getByTestId('graph-save').click();
  await expect(window.getByTestId('graph-unsaved')).toHaveCount(0);
  const saved = readFileSync(join(library, 'shared/graphs', File), 'utf8');
  expect(saved).toContain('Colour/Grayscale');
  expect(saved).toContain('Scale');

  await window.screenshot({ path: join(shots, 'graph-edited.png') });
});

test('moves, deletes and shows a second client without a reload', async () => {
  await openGraph();

  // A drag is one undo step, and the Server has the node where it was dropped.
  const start = await centre(node(3).locator('xui-graph-node-header, [data-xui-graph-node-header]').first());
  await dragBetween(start, { x: start.x + 60, y: start.y + 90 });
  await expect(window.getByTestId('graph-undo')).toHaveAttribute('title', /Move/);
  const moved = await centre(node(3));
  await window.getByTestId('graph-undo').click();
  await expect.poll(async () => (await centre(node(3))).y).toBeLessThan(moved.y);

  // Delete takes the node and its wires.
  await node(4).locator('xui-graph-node-preview').click();
  await expect(window.getByTestId('inspector-title')).toHaveText('HSL');
  await window.keyboard.press('Delete');
  await expect(nodes()).toHaveCount(Nodes - 1);
  await window.getByTestId('graph-undo').click();
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
  await openGraph();
  await hashOf(thumbnail(2));
  const { baseUrl, token } = await server();
  const statuses = await window.evaluate(() =>
    (globalThis as unknown as { patina: { sidecars(): Promise<{ name: string; pid: number | null }[]> } }).patina.sidecars()
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
