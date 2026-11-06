import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { existsSync, mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { answerOpen as answerFile, answerSave as answerAgain } from './projects';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');
const bench = resolve(__dirname, '../../../../engine/tools/Patina.Bench/bin/Release/net10.0/patina-bench.dll');
const shots = resolve(__dirname, '../../../dist/e2e-results');
const softwareWebGl = process.platform === 'linux' ? ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] : [];

/**
 * What a person does, with nothing behind their back: the app starts with no library and no environment variable, and
 * everything after that is clicks. The system's file dialogs are the only thing answered for them.
 */
let scratch: string;
let app: ElectronApplication;
let window: Page;
let mesh: string;

async function launch(): Promise<void> {
  const env = { ...process.env, PATINA_USER_DATA: join(scratch, 'user-data'), PATINA_LIBRARY: '' };
  app = await electron.launch({ args: [...softwareWebGl, main], env });
  window = await app.firstWindow();
}

const answerSave = (path: string) => answerAgain(app, path);
const answerOpen = (path: string) => answerFile(app, path);

const row = (path: string) => window.locator(`[data-testid=tree-row][data-path="${path}"]`);

async function drag(from: string, to: string): Promise<void> {
  await row(from).dragTo(row(to));
}

test.beforeEach(() => {
  test.skip(process.platform === 'win32', 'Windows CI has no Vulkan.');
  scratch = mkdtempSync(join(tmpdir(), 'patina-journey-'));
  execFileSync('dotnet', [bench, 'bake-pair', join(scratch, 'meshes'), '0.05']);
  mesh = join(scratch, 'meshes', 'housing_low.glb');
});

test.afterEach(async () => {
  await app.close();
  rmSync(scratch, { recursive: true, force: true });
});

test('a new user makes a library, groups projects in folders, and comes back to them', async () => {
  const library = join(scratch, 'Ember.ptlib');
  await launch();

  // First launch: nothing mentions a command line.
  await expect(window.getByText('No library open').first()).toBeVisible();
  await expect(window.locator('body')).not.toContainText('--library');

  await answerSave(library);
  await window.getByTestId('library-new').click();
  await expect(window.getByRole('status', { name: 'Server status' })).toContainText('Ember.ptlib', { timeout: 30_000 });
  expect(existsSync(join(library, 'library.yaml'))).toBe(true);
  await expect(window.getByText('No projects yet')).toBeVisible();

  // A project from a mesh opens ready to paint, and no path was typed.
  await answerOpen(mesh);
  await window.getByTestId('library-new-project').click();
  await window.getByTestId('new-project-name').fill('Housing');
  await window.getByTestId('new-project-mesh').click();
  await expect(window.getByTestId('new-project-mesh-name')).toHaveText('housing_low.glb');
  await window.getByRole('button', { name: 'Create' }).click();
  await expect(window.getByTestId('paint-project')).toHaveText('Housing', { timeout: 30_000 });
  await expect(window.getByTestId('paint-mesh-name')).toHaveText('housing_low.glb');
  await expect(window.getByTestId('app-title')).toContainText('Housing');
  expect(existsSync(join(library, 'projects', 'housing', 'housing_low.glb'))).toBe(true);
  await expect(row('housing')).toBeVisible();

  // A second project, closed, so that it can move.
  await window.getByTestId('library-new-project').click();
  await window.getByTestId('new-project-name').fill('Door');
  await window.getByTestId('new-project-kind').selectOption('layered-material');
  await window.getByRole('button', { name: 'Create' }).click();
  await expect(window.getByTestId('paint-project')).toHaveText('Door', { timeout: 30_000 });

  // A folder, named as it is made, and a colour from its menu.
  await window.getByTestId('tree-row').first().click();
  await window.getByTestId('library-tree').click({ position: { x: 100, y: 200 } });
  await window.getByTestId('library-new-folder').click();
  await window.getByTestId('tree-rename').fill('Hero Props');
  await window.getByTestId('tree-rename').press('Enter');
  await expect(row('Hero Props')).toBeVisible();
  await row('Hero Props').click({ button: 'right' });
  await window.getByTestId('menu-colour-#3e63dd').click();
  await expect(row('Hero Props').getByTestId('tree-folder-icon').locator('path')).toHaveAttribute('fill', '#3e63dd');
  expect(readFileSync(join(library, 'projects', 'Hero Props', 'folder.yaml'), 'utf8')).toContain('#3e63dd');

  // A subfolder.
  await row('Hero Props').click({ button: 'right' });
  await window.getByTestId('menu-new-folder').click();
  await window.getByTestId('tree-rename').fill('Doors');
  await window.getByTestId('tree-rename').press('Enter');
  await expect(row('Hero Props/Doors')).toBeVisible();

  // Regrouping: a closed project moves by dragging, and keeps being itself.
  await drag('housing', 'Hero Props/Doors');
  await expect(row('Hero Props/Doors/housing')).toBeVisible({ timeout: 15_000 });
  await expect(row('housing')).toHaveCount(0);

  // The open one is closed for the move, and opens again from its new place.
  await drag('door', 'Hero Props');
  await expect(row('Hero Props/door')).toBeVisible({ timeout: 15_000 });
  await expect(window.getByTestId('paint-none')).toBeVisible();

  // A folder moves too, and can be renamed.
  await row('Hero Props/Doors').click({ button: 'right' });
  await window.getByTestId('menu-rename').click();
  await window.getByTestId('tree-rename').fill('Metal Doors');
  await window.getByTestId('tree-rename').press('Enter');
  await expect(row('Hero Props/Metal Doors/housing')).toBeVisible({ timeout: 15_000 });

  // Back again later: the library and the tree are as they were left.
  await app.close();
  await launch();
  await expect(window.getByRole('status', { name: 'Server status' })).toContainText('Ember.ptlib', { timeout: 30_000 });
  await expect(row('Hero Props/Metal Doors/housing')).toBeVisible({ timeout: 15_000 });

  await row('Hero Props/Metal Doors/housing').dblclick();
  await expect(window.getByTestId('paint-mesh-name')).toHaveText('housing_low.glb', { timeout: 30_000 });
  await window.screenshot({ path: join(shots, 'journey.png') });

  // Deleting a folder never deletes a project: what it holds moves up a level.
  await window.getByTestId('paint-mesh-name').waitFor();

  // A project opens to show what it holds, and a graph joins it as a document with a kind.
  await row('Hero Props/Metal Doors/housing').getByTestId('tree-toggle').click();
  await expect(row('projects/Hero Props/Metal Doors/housing/housing.vxlayers')).toContainText('Housing');

  await row('Hero Props').click({ button: 'right' });
  await window.getByTestId('menu-delete').click();
  await expect(window.getByTestId('tree-delete')).toContainText('holds 2 project');
  await window.getByTestId('tree-delete-confirm').click();
  await expect(row('Metal Doors/housing')).toBeVisible({ timeout: 15_000 });
  await expect(row('door')).toBeVisible();
  expect(existsSync(join(library, 'projects', 'Hero Props'))).toBe(false);
  expect(existsSync(join(library, 'projects', 'door', 'project.yaml'))).toBe(true);
});

test('painted work asks before the project is left', async () => {
  await launch();
  await answerSave(join(scratch, 'Ember.ptlib'));
  await window.getByTestId('library-new').click();
  await expect(window.getByRole('status', { name: 'Server status' })).toContainText('Ember.ptlib', { timeout: 30_000 });
  for (const name of ['First', 'Second']) {
    await window.getByTestId('library-new-project').click();
    await window.getByTestId('new-project-name').fill(name);
    await window.getByTestId('new-project-kind').selectOption('layered-material');
    await window.getByRole('button', { name: 'Create' }).click();
    await expect(window.getByTestId('paint-project')).toHaveText(name, { timeout: 30_000 });
  }

  const picture = window.getByTestId('paint-picture');
  await expect(picture).toBeVisible({ timeout: 30_000 });
  const box = await picture.boundingBox();
  if (!box) {
    throw new Error('The atlas has no box.');
  }

  await window.mouse.move(box.x + 40, box.y + 40);
  await window.mouse.down();
  await window.mouse.move(box.x + 80, box.y + 80, { steps: 5 });
  await window.mouse.up();
  await expect(window.getByTestId('paint-unsaved')).toBeVisible({ timeout: 15_000 });

  await row('first').dblclick();
  await expect(window.getByTestId('project-leaving')).toBeVisible();
  await window.getByTestId('project-save').click();
  await expect(window.getByTestId('paint-project')).toHaveText('First', { timeout: 30_000 });
});
