import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { existsSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { leave, newLibrary, newProject } from './projects';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
const shots = resolve(__dirname, '../../../dist/e2e-results');
const softwareWebGl = process.platform === 'linux' ? ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] : [];

/** Looking after a library (#16, #145): pictures in a grid, names and tags in files, and a bin to restore from. */
let scratch: string;
let library: string;
let app: ElectronApplication;
let window: Page;

test.beforeEach(async () => {
  scratch = mkdtempSync(join(tmpdir(), 'patina-e2e-care-'));
  library = join(scratch, 'Game.ptlib');
  app = await electron.launch({
    args: [...softwareWebGl, main],
    env: {
      ...process.env,
      PATINA_USER_DATA: join(scratch, 'user-data'),
      Patina__PreferencesFile: join(scratch, 'engine-preferences.json')
    }
  });
  window = await app.firstWindow();
});

test.afterEach(async () => {
  await leave(app);
  rmSync(scratch, { recursive: true, force: true, maxRetries: 20, retryDelay: 250 });
});

/** A graph with a noise wired to its Output, so it has a picture. */
const noiseGraph = (name: string, scale: number) => `name: ${name}
nodes:
  - { id: 1, type: Source/Noise, values: { Scale: [${String(scale)}] } }
  - { id: 2, type: Output/Output, texts: { Usage: baseColor } }
edges:
  - { fromNode: 1, fromPort: Out, toNode: 2, toPort: Input }
groups: []
comments: []
interface: []
settings: { baseWidth: '256', baseHeight: '256', seed: '1' }
parameters: []
`;

const tile = (name: string) => window.getByTestId('library-row').filter({ hasText: name });

/** Shows the Library as a grid or a list, whichever it was last. */
async function layOut(as: 'grid' | 'list'): Promise<void> {
  const toggle = window.getByTestId('library-layout');
  if ((await toggle.getAttribute('data-layout')) !== as) {
    await toggle.click();
  }

  await expect(toggle).toHaveAttribute('data-layout', as);
}
const bin = () => readdirSync(join(library, '.trash')).filter(entry => !entry.startsWith('.'));

test('the grid shows pictures, and its menu renames, tags and deletes to a bin that restores', async () => {
  test.skip(process.platform === 'win32' && !!process.env['CI'], 'Windows CI has no Vulkan.');
  // Two materials are evaluated for their pictures, and a project's index catches up after each change.
  test.setTimeout(240_000);

  await newLibrary(app, window, library);
  await newProject(app, window, 'Rust', undefined, 'graph');
  await newProject(app, window, 'Moss', undefined, 'graph');
  // A new graph has nothing wired to its Output, and so nothing to show: these have a noise each.
  await window.getByTestId('document-tab-close').last().click();
  await window.getByTestId('document-tab-close').last().click();
  writeFileSync(join(library, 'projects', 'rust', 'rust.vxtexgraph'), noiseGraph('Rust', 3));
  writeFileSync(join(library, 'projects', 'moss', 'moss.vxtexgraph'), noiseGraph('Moss', 9));

  await layOut('grid');
  await expect(tile('Rust')).toBeVisible({ timeout: 30_000 });
  // The Server draws a project as its graph, into the cache, and the grid shows it by hash.
  await expect(tile('Rust').getByTestId('library-picture')).toBeVisible({ timeout: 90_000 });
  await expect(tile('Moss').getByTestId('library-picture')).toBeVisible({ timeout: 90_000 });
  await window.screenshot({ path: join(shots, 'library-grid.png') });

  // Rename and tag, from the menu; both are written to the project's file.
  await tile('Rust').click({ button: 'right' });
  await window.getByTestId('item-rename').click();
  await window.getByTestId('library-rename-name').fill('Old Rust');
  await window.getByTestId('library-rename-save').click();
  await expect(tile('Old Rust')).toBeVisible({ timeout: 30_000 });
  await tile('Old Rust').click({ button: 'right' });
  await window.getByTestId('item-tags').click();
  await window.getByTestId('library-tags-input').locator('input').fill('metal');
  await window.getByTestId('library-tags-input').locator('input').press('Enter');
  await window.getByTestId('library-tags-save').click();
  await expect.poll(() => readFileSync(join(library, 'projects', 'rust', 'project.yaml'), 'utf8')).toContain('metal');

  // Double-click opens any kind: here, a project at its graph.
  await tile('Moss').dblclick();
  await expect(window.getByTestId('document-tab-name').filter({ hasText: 'Moss' })).toBeVisible();

  // Delete goes to the bin, with a question first; the bin brings it back where it was.
  await tile('Old Rust').click({ button: 'right' });
  await window.getByTestId('item-delete').click();
  await window.getByTestId('library-delete-confirm').click();
  await expect(tile('Old Rust')).toHaveCount(0, { timeout: 30_000 });
  expect(existsSync(join(library, 'projects', 'rust'))).toBe(false);
  expect(bin()).toHaveLength(1);

  await window.getByTestId('library-bin').click();
  await expect(window.getByTestId('bin-item')).toHaveCount(1);
  await expect(window.getByTestId('bin-item')).toContainText('Old Rust');
  await window.screenshot({ path: join(shots, 'library-bin.png') });
  await window.getByTestId('bin-restore').click();
  await expect(window.getByTestId('bin')).toContainText('The bin is empty');
  await window.keyboard.press('Escape');
  await expect(tile('Old Rust')).toBeVisible({ timeout: 30_000 });
  expect(existsSync(join(library, 'projects', 'rust', 'project.yaml'))).toBe(true);
});

test('a document deleted from the tree waits in the bin, and Empty Bin deletes it for good', async () => {
  test.setTimeout(120_000);
  await newLibrary(app, window, library);
  await newProject(app, window, 'Crate', undefined, 'graph');
  await window.getByTestId('document-tab-close').last().click();
  await layOut('list');

  // A second document, made from the project's menu.
  const project = window.locator('[data-testid=tree-row][data-kind=project]').filter({ hasText: 'Crate' });
  await project.click({ button: 'right' });
  await window.getByTestId('menu-new-document').click();
  await window.getByTestId('new-document-name').fill('Planks');
  await window.getByTestId('new-document-kind').selectOption('graph');
  await window.getByTestId('new-document-create').click();
  await expect(window.getByTestId('document-tab-name').filter({ hasText: 'Planks' })).toBeVisible({ timeout: 30_000 });
  // Closed at once, while its graph may still be opening: the close ends the session the open gets (#232).
  await window.getByTestId('document-tab-close').last().click();
  await expect(window.getByTestId('document-tab-name').filter({ hasText: 'Planks' })).toHaveCount(0, { timeout: 30_000 });

  if ((await project.getAttribute('aria-expanded')) !== 'true') {
    await project.getByTestId('tree-toggle').click();
  }

  const planks = window.locator('[data-testid=tree-row][data-kind=document]').filter({ hasText: 'Planks' });
  await planks.click({ button: 'right' });
  await window.getByTestId('menu-delete-document').click();
  await window.getByTestId('library-delete-confirm').click();
  await expect(planks).toHaveCount(0, { timeout: 30_000 });
  expect(existsSync(join(library, 'projects', 'crate', 'planks.vxtexgraph'))).toBe(false);

  await window.getByTestId('library-bin').click();
  await expect(window.getByTestId('bin-item')).toContainText('Document from crate');
  await window.getByTestId('bin-empty').click();
  await window.getByTestId('bin-empty-confirm').click();
  await expect(window.getByTestId('bin')).toContainText('The bin is empty');
  expect(bin()).toHaveLength(0);
});
