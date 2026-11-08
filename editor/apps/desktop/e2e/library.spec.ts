import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { existsSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
const bench = resolve(__dirname, '../../../../engine/tools/Patina.Bench/bin/Release/net10.0/patina-bench.dll');
const shots = resolve(__dirname, '../../../dist/e2e-results');
const Generated = 300;
/** What the generator asks for, and the two projects it keeps its materials in. */
const Projects = Generated + 2;

let scratch: string;
let library: string;
let app: ElectronApplication;
let window: Page;

test.beforeAll(() => {
  scratch = mkdtempSync(join(tmpdir(), 'patina-e2e-library-'));
  library = join(scratch, 'Generated.ptlib');
  execFileSync('dotnet', [bench, 'generate', library, String(Generated)], { stdio: 'inherit' });
  if (!existsSync(join(library, 'library.yaml'))) {
    throw new Error('patina-bench wrote no library.');
  }
});

test.afterAll(() => {
  rmSync(scratch, { recursive: true, force: true });
});

test.beforeEach(async () => {
  app = await electron.launch({
    args: [main],
    env: { ...process.env, PATINA_USER_DATA: join(scratch, 'user-data'), PATINA_LIBRARY: library }
  });
  window = await app.firstWindow();
  await expect(count()).toHaveText(`${Projects} projects`);
});

test.afterEach(async () => {
  await app.close();
});

const count = () => window.getByTestId('library-count');
const rows = () => window.getByTestId('library-row');

test('browses, searches and shows the impact of a material', async () => {
  await expect(window.getByTestId('tree-row').first()).toBeVisible();

  await window.getByTestId('library-search').fill('crate');
  await expect(count()).not.toHaveText(`${Projects} projects`);
  await expect(rows().first()).toContainText(/crate/i);
  await window.getByTestId('library-search').fill('');
  await expect(count()).toHaveText(`${Projects} projects`);

  await window.getByTestId('library-tags').locator('input').click();
  await window.getByRole('option', { name: 'hero' }).click();
  await expect(count()).not.toHaveText(`${Projects} projects`);
  await window.keyboard.press('Escape');
  await window.getByTestId('library-tags').getByRole('button').first().click();
  await expect(count()).toHaveText(`${Projects} projects`);

  // A row is a placeholder until its page comes, and clicking one selects nothing, so it waits for the search's rows.
  await window.getByTestId('library-search').fill('a');
  await expect(rows().first()).toContainText(/a/i);
  await rows().first().click();
  await expect(rows().first()).toHaveAttribute('aria-selected', 'true');
  await expect(window.getByTestId('inspector-title')).not.toBeEmpty();
  await window.getByTestId('library-list').press('ArrowDown');
  await expect(rows().nth(1)).toHaveAttribute('aria-selected', 'true');
  await window.getByTestId('library-search').fill('');

  await window.getByRole('radio', { name: 'Materials' }).click();
  await expect(count()).toHaveText(/materials?$/);
  await rows().first().click();
  await expect(window.getByTestId('impact-summary')).toContainText(/Affects [\d,]+ projects?/);
  await window.screenshot({ path: join(shots, 'library-assets.png') });
});

test('the tree menu opens from the keyboard, on the chosen row', async () => {
  const tree = window.getByTestId('library-tree');
  await window.getByTestId('tree-row').first().click();
  await tree.press('ArrowDown');
  await tree.press('Shift+F10');

  const menu = window.getByTestId('tree-menu');
  await expect(menu).toBeVisible();
  await expect(menu.getByRole('menuitem').first()).toBeFocused();
  await window.keyboard.press('ArrowDown');
  await expect(menu.getByRole('menuitem').nth(1)).toBeFocused();
  await window.screenshot({ path: join(shots, 'tree-menu-keys.png') });
  await window.keyboard.press('Escape');
  await expect(menu).toHaveCount(0);
  await expect(tree).toBeFocused();
});

test('a new project reaches the list through the index', async () => {
  await window.getByTestId('library-new-project').click();
  await expect(window.getByTestId('new-project-name')).toBeFocused();
  await window.getByTestId('new-project-name').fill('Zebra Crate');
  await window.getByTestId('new-project-kind').selectOption('layered-material');
  await window.locator('xui-tag-input input').fill('striped');
  await window.locator('xui-tag-input input').press('Enter');
  await expect(window.getByTestId('new-project-name')).toBeVisible(); // Enter in the tag field does not submit
  await window.getByRole('button', { name: 'Create' }).click();

  await expect(window.getByTestId('document-tab-name')).toHaveText('Zebra Crate');
  await expect(count()).toHaveText(`${Projects + 1} projects`);
  await window.getByTestId('library-search').fill('zebra');
  await expect(rows().first()).toContainText('zebra-crate');
  await window.getByTestId('library-search').fill('');
  await expect(window.getByTestId('paint-project')).toHaveText('Zebra Crate');
  expect(existsSync(join(library, 'projects/zebra-crate/project.yaml'))).toBe(true);
  rmSync(join(library, 'projects/zebra-crate'), { recursive: true, force: true });
});

test('the Server says why a new project is refused', async () => {
  await window.getByTestId('library-new-project').click();
  await window.getByTestId('new-project-name').fill('---');
  await window.getByTestId('new-project-kind').selectOption('graph');
  await window.getByRole('button', { name: 'Create' }).click();

  await expect(window.getByTestId('new-project-refused')).toContainText('a letter or a digit');
  await window.screenshot({ path: join(shots, 'library-new-project.png') });
});
