import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { chooseIn } from '../core/select-testing';
import { SystemStatus } from '../core/system-status';
import { FileChooser } from './file-chooser';
import { LibraryStore } from './library-store';
import { LibraryTreeStore } from './library-tree-store';
import { NewProjectDialog } from './new-project-dialog';
import { ProjectStore } from './project-store';

async function render() {
  const createProject = vi.fn((_request: unknown) => Promise.resolve({ path: 'strip' }));
  const choose = vi.fn((_request: unknown) => Promise.resolve('C:\\Art\\Game.ptlib/projects/crate/strip.glb'));
  TestBed.configureTestingModule({
    providers: [
      { provide: LibraryStore, useValue: { createProject } },
      { provide: LibraryTreeStore, useValue: { reload: vi.fn(), choose: vi.fn() } },
      { provide: ProjectStore, useValue: { open: vi.fn(), startSettings: vi.fn() } },
      { provide: FileChooser, useValue: { path: choose } },
      {
        provide: SystemStatus,
        useValue: { textureSides: signal([512, 1024, 2048]), defaultTextureSide: signal(2048) }
      }
    ]
  });
  const fixture = TestBed.createComponent(NewProjectDialog);
  fixture.componentRef.setInput('open', true);
  fixture.detectChanges();
  await fixture.whenStable();
  return { fixture, createProject, choose };
}

const byId = <T extends HTMLElement>(id: string) => document.querySelector<T>(`[data-testid="${id}"]`);

describe('NewProjectDialog', () => {
  it('starts a project wider than it is tall, from a mesh chosen as every file is (#144, #4)', async () => {
    const { fixture, createProject, choose } = await render();
    const name = byId<HTMLInputElement>('new-project-name');
    if (!name) {
      throw new Error('New Project shows no name.');
    }

    name.value = 'Strip';
    name.dispatchEvent(new Event('input'));
    byId('new-project-mesh')?.click();
    await fixture.whenStable();
    expect(choose).toHaveBeenCalledWith({ kind: 'mesh', title: 'Choose a Mesh', project: null });
    expect(byId('new-project-mesh-name')?.textContent.trim()).toBe('strip.glb');

    await chooseIn(fixture, document.body, 'new-project-rows', '1024 texels');
    expect(byId('new-project-resolution')?.textContent).toContain('2048 × 1024');

    document.querySelector<HTMLButtonElement>('button[type="submit"]')?.click();
    await fixture.whenStable();
    expect(createProject).toHaveBeenCalledWith(
      expect.objectContaining({
        name: 'Strip',
        mesh: 'C:\\Art\\Game.ptlib/projects/crate/strip.glb',
        resolution: 2048,
        rows: 1024
      })
    );
  });

  it('keeps a graph square', async () => {
    const { fixture } = await render();
    expect(byId('new-project-rows')).not.toBeNull();

    await chooseIn(fixture, document.body, 'new-project-kind', 'Graph: nodes');

    expect(byId('new-project-rows')).toBeNull();
  });
});
