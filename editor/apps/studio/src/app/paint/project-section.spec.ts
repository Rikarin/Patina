import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { SystemStatus } from '../core/system-status';
import { FileChooser } from '../library/file-chooser';
import { LibraryStore } from '../library/library-store';
import { ProjectStore } from '../library/project-store';
import { ProjectSection } from './project-section';

async function render() {
  const unwrap = {
    resolution: 2048,
    margin: 4,
    quality: 'outlines',
    rotations: 4,
    texelDensity: 0,
    overflow: 'shrink'
  };
  const current = signal({
    path: 'crate',
    name: 'Crate',
    autoUnwrap: false,
    preservePaint: true,
    unwrap,
    meshes: [{ id: 'm1', path: 'projects/crate/crate_low.glb' }],
    pictures: [],
    bakes: [],
    documents: [{ mesh: 'm1' }]
  });
  const applySettings = vi.fn((_settings: unknown) => Promise.resolve(true));
  const project = {
    current,
    meshName: signal('crate_low.glb'),
    unwrapping: signal(false),
    settingsOpen: signal(false),
    settingsFailure: signal<string | null>(null),
    movesMesh: () => false,
    openInside: () => [],
    applySettings
  };
  const facts = vi.fn(() => Promise.resolve({ data: undefined, response: { ok: false } }));
  TestBed.configureTestingModule({
    providers: [
      { provide: ProjectStore, useValue: project },
      { provide: FileChooser, useValue: { path: () => Promise.resolve('D:\\Meshes\\crate_v2.glb') } },
      { provide: SystemStatus, useValue: { textureSides: signal([1024, 2048]) } },
      { provide: LibraryStore, useValue: { api: () => Promise.resolve({ GET: facts }) } }
    ]
  });
  const fixture = TestBed.createComponent(ProjectSection);
  await fixture.whenStable();
  return { element: fixture.nativeElement as HTMLElement, fixture, applySettings };
}

describe('ProjectSection', () => {
  it('sets the project in Properties as Project Settings does, and starts again once applied (#201)', async () => {
    const { element, fixture, applySettings } = await render();
    const find = (id: string) => element.querySelector<HTMLButtonElement>(`[data-testid=${id}]`);

    expect(find('project-settings-mesh-name')?.textContent.trim()).toBe('crate_low.glb');
    expect(find('paint-project-apply')?.disabled).toBe(true);

    find('project-settings-mesh')?.click();
    await fixture.whenStable();
    expect(find('project-settings-mesh-name')?.textContent.trim()).toBe('crate_v2.glb');
    expect(find('paint-project-apply')?.disabled).toBe(false);

    find('paint-project-apply')?.click();
    await fixture.whenStable();

    expect(applySettings).toHaveBeenCalledWith({ mesh: 'D:\\Meshes\\crate_v2.glb', preservePaint: true });
    expect(find('project-settings-mesh-name')?.textContent.trim()).toBe('crate_low.glb');
    expect(find('paint-project-apply')?.disabled).toBe(true);
  });

  it('puts back what the project has on Revert', async () => {
    const { element, fixture, applySettings } = await render();
    const find = (id: string) => element.querySelector<HTMLButtonElement>(`[data-testid=${id}]`);

    find('project-settings-mesh')?.click();
    await fixture.whenStable();
    find('paint-project-revert')?.click();
    await fixture.whenStable();

    expect(find('project-settings-mesh-name')?.textContent.trim()).toBe('crate_low.glb');
    expect(applySettings).not.toHaveBeenCalled();
  });
});
