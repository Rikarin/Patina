import {
  _electron as electron,
  expect,
  test,
  type ElectronApplication,
  type Locator,
  type Page
} from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { leave } from './projects';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
const bench = resolve(__dirname, '../../../../engine/tools/Patina.Bench/bin/Release/net10.0/patina-bench.dll');
const shots = resolve(__dirname, '../../../dist/e2e-results');
const Name = 'Rust Plate';
/** Noise (1), Blur (2), Invert (3), HSL (4), Blur (5) and an Output (6), wired in that order. */
const Nodes = 6;

let scratch: string;
let app: ElectronApplication;
let window: Page;

test.beforeEach(async () => {
  scratch = mkdtempSync(join(tmpdir(), 'patina-e2e-authoring-'));
  const library = join(scratch, 'Generated.ptlib');
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
  await leave(app);
  rmSync(scratch, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 });
});

const nodes = () => window.getByTestId('graph-node');
const node = (id: number) => window.locator(`[data-testid=graph-node][data-node="${String(id)}"]`);
const thumbnail = (id: number) => node(id).locator('ptn-graph-thumbnail');
const connector = (id: number, port: string) => node(id).locator(`[data-xui-graph-port="${port}"]`).getByRole('button');

async function openGraph(): Promise<void> {
  await window.getByRole('radio', { name: 'Materials' }).click();
  await window.getByTestId('library-search').fill(Name);
  await expect(window.getByTestId('library-row')).toHaveCount(1);
  await window.getByTestId('library-row').click();
  await window.getByTestId('open-graph').click();
  await expect(nodes()).toHaveCount(Nodes);
}

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

/** Selects a node by its title bar, where a press does not start a wire or hit the preview. */
async function choose(id: number): Promise<void> {
  // A press on a node that is already selected keeps the selection, for dragging it, so clear it first.
  const canvas = await window.getByTestId('graph-canvas').boundingBox();
  await window.mouse.click((canvas?.x ?? 0) + 8, (canvas?.y ?? 0) + 8);
  await node(id).locator('[data-xui-graph-node-header]').click();
  await expect(node(id)).toHaveAttribute('aria-selected', 'true');
}

test('bypasses a node, which the compiler honours, and bends and relinks wires', async () => {
  await openGraph();
  const original = await hashOf(thumbnail(4));

  // Off: the HSL after it now sees the blur instead of the invert, so its picture changes.
  await choose(3);
  await window.keyboard.press('ControlOrMeta+b');
  await expect(node(3)).toHaveAttribute('data-bypassed', 'true');
  await expect(node(3).getByTestId('graph-node-bypassed')).toBeVisible();
  await expect(thumbnail(4)).not.toHaveAttribute('data-hash', original);
  const bypassed = await hashOf(thumbnail(4));
  await window.screenshot({ path: join(shots, 'authoring-bypassed.png') });

  // On again, from the node's menu: the same picture as before, from the cache.
  await node(3).locator('[data-xui-graph-node-header]').click({ button: 'right' });
  await window.getByTestId('node-bypass').click();
  await expect(node(3)).not.toHaveAttribute('data-bypassed', 'true');
  await expect(thumbnail(4)).toHaveAttribute('data-hash', original);
  expect(bypassed).not.toBe(original);

  // Alt-click the wire from the noise to the blur: a reroute dot in it, and the blur's picture is unchanged.
  const blur = await hashOf(thumbnail(2));
  const from = await centre(connector(1, 'Out'));
  const to = await centre(connector(2, 'Input'));
  await window.keyboard.down('Alt');
  await window.mouse.click((from.x + to.x) / 2, (from.y + to.y) / 2);
  await window.keyboard.up('Alt');
  const dot = window.locator('[data-testid=graph-node][data-reroute]');
  await expect(dot).toHaveCount(1);
  await expect(nodes()).toHaveCount(Nodes + 1);
  await window.waitForTimeout(500);
  await expect(thumbnail(2)).toHaveAttribute('data-hash', blur);
  await window.screenshot({ path: join(shots, 'authoring-reroute.png') });

  // Delete the invert and keep the chain joined: the HSL now reads the blur, as when the invert was off.
  await choose(3);
  await window.keyboard.press('ControlOrMeta+Backspace');
  await expect(node(3)).toHaveCount(0);
  await expect(thumbnail(4)).toHaveAttribute('data-hash', bypassed);
  await expect(window.getByTestId('app-undo')).toHaveAttribute('title', /^Undo Delete and Relink\b/);
});

test('arranges, badges a problem and colours a frame', async () => {
  await openGraph();
  const before = await node(2).boundingBox();

  // Lay the graph out: the cards move into Vixen's columns.
  await window.getByTestId('graph-arrange').click();
  await window.getByTestId('arrange-layout').click();
  await expect.poll(async () => (await node(2).boundingBox())?.x).not.toBe(before?.x);

  // Line up the noise and the blur on their left edges.
  await choose(1);
  await node(2)
    .locator('[data-xui-graph-node-header]')
    .click({ modifiers: ['Shift'] });
  await window.getByTestId('graph-arrange').click();
  await window.getByTestId('arrange-left').click();
  await expect
    .poll(async () => Math.abs(((await node(1).boundingBox())?.x ?? 0) - ((await node(2).boundingBox())?.x ?? 1)))
    .toBeLessThan(1);

  // A broken expression is an error on its node, and the node says so.
  await choose(2);
  await window.getByTestId('expression-toggle-Radius').click();
  await window.getByTestId('expression-Radius').fill('Nowhere * 2');
  await window.getByTestId('expression-Radius').press('Enter');
  const badge = node(2).getByTestId('graph-node-problem');
  await expect(badge).toHaveAttribute('data-severity', 'Error');
  await expect(badge).toHaveText('1');

  // A frame round two nodes, in a colour of its own with a description.
  await choose(4);
  await node(5)
    .locator('[data-xui-graph-node-header]')
    .click({ modifiers: ['Shift'] });
  await window.getByTestId('graph-group').click();
  await window.getByTestId('graph-settings').click();
  await window.getByTestId('group-colour').getByRole('combobox').click();
  await window.getByRole('option', { name: 'Color 3' }).click();
  await window.getByTestId('group-description').fill('Where the colour is decided');
  await window.getByTestId('group-description').press('Enter');
  await expect(window.getByTestId('graph-frame-description')).toHaveText('Where the colour is decided');
  await expect(window.getByTestId('graph-frame')).toHaveAttribute('data-color', 'var(--color-chart-3)');
  await window.screenshot({ path: join(shots, 'authoring-arranged.png') });
});

test('describes parameters, hides one by another, and keeps presets', async () => {
  await openGraph();
  await window.getByTestId('graph-settings').click();

  // Two parameters: a flag shown as a toggle, and an angle shown only while the flag is on.
  await window.getByTestId('parameter-add').click();
  await window.getByTestId('parameter-add').click();
  const flag = window.getByTestId('parameter-Parameter1');

  await flag.getByRole('combobox').first().click();
  await window.getByRole('option', { name: 'On or Off' }).click();
  await window.getByTestId('parameter-describe-Parameter1').click();
  await window.getByTestId('parameter-widget-Parameter1').getByRole('combobox').click();
  await window.getByRole('option', { name: 'Toggle' }).click();
  // xUI gap: a button that is not active says nothing, where a toggle should say aria-pressed=false.
  await expect(flag.getByRole('button', { name: 'Parameter1', exact: true })).not.toHaveAttribute(
    'aria-pressed',
    'true'
  );

  await window.getByTestId('parameter-describe-Parameter2').click();
  await window.getByTestId('parameter-label-Parameter2').fill('Scratch angle');
  await window.getByTestId('parameter-label-Parameter2').press('Enter');
  await window.getByTestId('parameter-group-Parameter2').fill('Scratches');
  await window.getByTestId('parameter-group-Parameter2').press('Enter');
  await expect(window.getByTestId('parameter-group')).toHaveText('Scratches');
  // The group went out straight after the label, and it must not have taken the label back.
  await expect(window.getByTestId('parameter-label-Parameter2')).toHaveValue('Scratch angle');
  await window.getByTestId('parameter-widget-Parameter2').getByRole('combobox').click();
  await window.getByRole('option', { name: 'Angle' }).click();
  await expect(window.getByTestId('parameter-angle-Parameter2')).toBeVisible();
  await window.getByTestId('parameter-visible-Parameter2').getByRole('combobox').first().click();
  await window.getByRole('option', { name: 'Parameter1' }).click();

  // The flag is off, so the angle is hidden from whoever uses the graph: its card greys here.
  await expect(window.getByTestId('parameter-Parameter2')).toHaveClass(/opacity-60/);
  await flag.getByRole('button', { name: 'Parameter1', exact: true }).click();
  await expect(window.getByTestId('parameter-Parameter2')).not.toHaveClass(/opacity-60/);

  // A preset of things as they are, then a change, then the preset back.
  await window.getByTestId('preset-name').fill('Scratched');
  await window.getByTestId('preset-save').click();
  await expect(window.getByTestId('preset-Scratched')).toBeVisible();
  await window.screenshot({ path: join(shots, 'authoring-parameters.png') });
  await flag.getByRole('button', { name: 'Parameter1', exact: true }).click();
  await expect(window.getByTestId('parameter-Parameter2')).toHaveClass(/opacity-60/);
  await window.getByTestId('preset-apply-Scratched').click();
  await expect(window.getByTestId('parameter-Parameter2')).not.toHaveClass(/opacity-60/);
  await expect(window.getByTestId('app-undo')).toHaveAttribute('title', /^Undo Preset Scratched\b/);
});

test('says what the graph is about, and publishes it as a package with what it reads', async () => {
  test.setTimeout(120_000);
  await openGraph();
  await window.getByTestId('graph-settings').click();

  await window.getByTestId('about-description').fill('A plate gone to rust.');
  await window.getByTestId('about-description').press('Tab');
  await window.getByTestId('about-tags').fill('metal, rust, Metal');
  await window.getByTestId('about-tags').press('Enter');
  await expect(window.getByTestId('about-tags')).toHaveValue('metal, rust');
  await expect(window.getByTestId('app-undo')).toHaveAttribute('title', /^Undo Describe Graph\b/);

  // The graph has unsaved changes, so the dialog saves it before it says what a package would hold.
  await window.getByTestId('about-publish').click();
  const dialog = window.getByTestId('publish-package');
  await expect(window.getByTestId('publish-package-path')).toHaveText(`packages/${Name}.ptpkg`);
  await expect(dialog.getByTestId('publish-package-problem')).toHaveCount(0);
  await window.screenshot({ path: join(shots, 'publish-package.png') });
  await window.getByTestId('publish-package-publish').click();
  await expect(dialog).toBeHidden();

  const library = join(scratch, 'Generated.ptlib');
  const folder = join(library, 'packages', `${Name}.ptpkg`);
  expect(readFileSync(join(folder, 'package.yaml'), 'utf8')).toContain(`name: ${Name}`);
  const graph = readFileSync(join(folder, `${Name}.vxtexgraph`), 'utf8');
  expect(graph).toContain('description: A plate gone to rust.');
  expect(graph).toMatch(/tags: \[metal, rust\]/);
});
