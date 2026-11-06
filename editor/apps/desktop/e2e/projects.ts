import { expect, type ElectronApplication, type Page } from '@playwright/test';

/** Answers the system's next save dialog, which a test cannot click. */
export const answerSave = (app: ElectronApplication, path: string) =>
  app.evaluate(({ dialog }, filePath) => {
    Object.assign(dialog, { showSaveDialog: () => Promise.resolve({ canceled: false, filePath }) });
  }, path);

/** Answers the system's next open dialog. */
export const answerOpen = (app: ElectronApplication, path: string) =>
  app.evaluate(({ dialog }, filePath) => {
    Object.assign(dialog, { showOpenDialog: () => Promise.resolve({ canceled: false, filePaths: [filePath] }) });
  }, path);

/** Makes a library the way a person does. */
export async function newLibrary(app: ElectronApplication, window: Page, path: string): Promise<void> {
  await answerSave(app, path);
  await window.getByTestId('library-new').click();
  await expect(window.getByRole('status', { name: 'Server status' })).toContainText(path.split('/').pop() ?? path, {
    timeout: 30_000
  });
  await expect(window.getByRole('status', { name: 'Server status' })).toContainText('Worker connected');
}

/** Makes a project the way a person does, which also opens it. */
export async function newProject(
  app: ElectronApplication,
  window: Page,
  name: string,
  mesh?: string,
  kind = mesh ? 'texturing' : 'layered-material'
): Promise<void> {
  await window.getByTestId('library-new-project').click();
  await window.getByTestId('new-project-name').fill(name);
  await window.getByTestId('new-project-kind').selectOption(kind);
  if (mesh) {
    await answerOpen(app, mesh);
    await window.getByTestId('new-project-mesh').click();
    await expect(window.getByTestId('new-project-mesh-name')).toHaveText(mesh.split('/').pop() ?? mesh);
  }

  await window.getByRole('button', { name: 'Create' }).click();
  await expect(window.getByTestId('document-tab-name').filter({ hasText: name })).toBeVisible({ timeout: 60_000 });
}

/** Ends the app with no one to answer its question about unsaved paint. */
export async function leave(app: ElectronApplication): Promise<void> {
  await app.evaluate(({ app: electron }) => {
    electron.exit(0);
  }).catch(() => undefined); // the app can be gone before the answer is
}
