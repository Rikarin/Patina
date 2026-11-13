import { _electron as electron, expect, test, type ElectronApplication, type Page } from '@playwright/test';
import { mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { leave, newLibrary, newProject } from './projects';

const main = resolve(__dirname, '../../../dist/apps/desktop/main.js');

let scratch: string;
let app: ElectronApplication;
let window: Page;

test.beforeEach(async () => {
  scratch = mkdtempSync(join(tmpdir(), 'patina-e2e-'));
  app = await electron.launch({ args: [main], env: { ...process.env, PATINA_USER_DATA: join(scratch, 'user-data') } });
  window = await app.firstWindow();
});

test.afterEach(async () => {
  await leave(app);
  rmSync(scratch, { recursive: true, force: true, maxRetries: 5 });
});

/** The native menu bar's labels, and whether an item is on. */
const bar = () => app.evaluate(({ Menu }) => Menu.getApplicationMenu()?.items.map(menu => menu.label) ?? []);
const enabled = (id: string) =>
  app.evaluate(({ Menu }, item) => Menu.getApplicationMenu()?.getMenuItemById(item)?.enabled ?? null, id);
const command = (name: string) =>
  app.evaluate(({ BrowserWindow }, sent) => {
    BrowserWindow.getAllWindows()[0]?.webContents.send('patina:command', sent);
  }, name);

test('the menus follow what is open, and an item with nothing to act on is off', async () => {
  await expect(window.getByRole('status', { name: 'Engine status' })).toContainText('GPU ready', { timeout: 60_000 });
  await expect.poll(bar).toContain('Help');
  expect(await bar()).not.toContain('Layer');
  for (const id of ['saveAs', 'reloadMesh', 'projectSettings', 'cut', 'closeDocument']) {
    expect(await enabled(id), id).toBe(false);
  }

  await newLibrary(app, window, join(scratch, 'Menus.ptlib'));
  await newProject(app, window, 'Menus');
  await expect.poll(bar).toEqual(expect.arrayContaining(['Layer', 'Tools', 'Viewport']));
  expect(await bar()).not.toContain('Graph');
  await expect.poll(() => enabled('saveAs')).toBe(true);

  // Delete is the stack's with focus in it, and off with the viewport focused.
  await window.locator('[data-layer]').first().click();
  await expect.poll(() => enabled('delete')).toBe(true);
  await window.getByRole('application', { name: 'Viewport' }).focus();
  await expect.poll(() => enabled('delete')).toBe(false);
});

test('keys keep their modifiers, stay out of dialogs and fields, refuse a clash and are kept in a file', async () => {
  await newLibrary(app, window, join(scratch, 'Keys.ptlib'));
  await newProject(app, window, 'Keys');
  const tool = (id: string) => window.getByTestId(`paint-tool-${id}`);
  await expect(tool('brush')).toBeVisible({ timeout: 30_000 });

  await command('shortcuts');
  const row = (id: string) => window.locator(`[data-command=${id}] [data-testid=shortcut-key]`);
  await row('lineTool').focus();
  await window.keyboard.press('l'); // only focused, not listening: in a dialog L is the dialog's
  await row('brushTool').click();
  await window.keyboard.press('ControlOrMeta+x');
  await expect(window.getByTestId('shortcuts-taken')).toContainText('is already Cut');
  // The app menu has Cmd+Q, and takes it before the window sees it (#224).
  await row('brushTool').click();
  await window.keyboard.press('ControlOrMeta+q');
  await expect(window.getByTestId('shortcuts-taken')).toContainText('is already Quit Patina, in the app menu');
  await expect(window.getByTestId('shortcuts-swap')).toHaveCount(0);
  await row('brushTool').click();
  await window.keyboard.press('ControlOrMeta+j');
  await expect(row('brushTool')).not.toHaveText(/^\s*J\s*$/u);
  await expect(row('brushTool')).toContainText('J');
  await window.keyboard.press('Escape');
  await expect(window.getByTestId('shortcuts')).toBeHidden();
  await expect(tool('line')).not.toHaveClass(/shadow-elevation-1/u);

  await window.locator('body').press('l');
  await expect(tool('line')).toHaveClass(/shadow-elevation-1/u);
  await window.locator('body').press('j'); // J alone is not ⌘J
  await expect(tool('line')).toHaveClass(/shadow-elevation-1/u);
  await expect(tool('brush')).toHaveAttribute('title', /J/u);

  const keys = JSON.parse(readFileSync(join(scratch, 'user-data', 'keys.json'), 'utf8')) as { keys: object };
  expect(keys.keys).toEqual({ brushTool: ['Mod+J'] });
});

test('the History pane lists the undo steps and goes back to one', async () => {
  await newLibrary(app, window, join(scratch, 'History.ptlib'));
  await newProject(app, window, 'History');
  const layers = window.locator('[data-layer]');
  await expect(layers.first()).toBeVisible({ timeout: 30_000 });
  const count = await layers.count();
  await window.getByTestId('paint-add-fill').click();
  await expect(layers).toHaveCount(count + 1);
  await window.getByTestId('paint-add').click();
  await expect(layers).toHaveCount(count + 2);
  await expect(window.getByTestId('app-undo')).toHaveAttribute('title', /^Undo Add Paint/u);

  await command('showHistory');
  const steps = window.getByTestId('history-step');
  await expect(steps).toHaveText(['Opened', 'Add Fill', 'Add Paint']);
  await steps.nth(1).click();
  await expect(layers).toHaveCount(count + 1);
  await expect(steps.nth(2)).toHaveClass(/italic/u);
  await steps.nth(0).click();
  await expect(layers).toHaveCount(count);
  await steps.nth(2).click();
  await expect(layers).toHaveCount(count + 2);
});

test('Help has the guide, the keys, a problem report, and About with the versions and whose work is whose', async () => {
  await expect(window.getByRole('status', { name: 'Engine status' })).toContainText('GPU ready', { timeout: 60_000 });
  const ids = () =>
    app.evaluate(({ Menu }) =>
      (Menu.getApplicationMenu()?.items ?? []).flatMap(menu => (menu.submenu?.items ?? []).map(item => item.id))
    );
  await expect.poll(ids).toEqual(expect.arrayContaining(['documentation', 'shortcuts', 'reportProblem', 'about']));

  // A link opens in the browser, never in the app's own window.
  await app.evaluate(({ shell }) => {
    Object.assign(globalThis, { opened: [] as string[] });
    Object.assign(shell, {
      openExternal: (url: string) => {
        (globalThis as unknown as { opened: string[] }).opened.push(url);
        return Promise.resolve();
      }
    });
  });
  await command('documentation');
  await expect
    .poll(() => app.evaluate(() => (globalThis as unknown as { opened: string[] }).opened))
    .toEqual(['https://github.com/Rikarin/Patina/blob/master/docs/03-guide.md']);

  await command('about');
  const about = window.getByTestId('about');
  await expect(about).toBeVisible();
  await expect(window.getByTestId('about-ownership')).toContainText('Your work is yours');
  await expect(window.getByTestId('about-version-patina')).not.toBeEmpty();
  await expect(window.getByTestId('about-version-electron')).toHaveText(/^\d+\./u);
  await expect(about).toContainText('GNU General Public License');
});
