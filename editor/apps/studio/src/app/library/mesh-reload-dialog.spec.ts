import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { LibraryStore } from './library-store';
import { MeshReloadDialog } from './mesh-reload-dialog';
import { ProjectStore } from './project-store';

interface Reassigning {
  vanished(): string[];
  appeared(): string[];
  choose(set: string, material: string | null): void;
  reload(): void;
}

async function render() {
  const resolveReloading = vi.fn((_reload: boolean, _reassign?: Record<string, string>) => Promise.resolve());
  const facts = { materials: [{ name: 'Body' }, { name: 'Belt' }, { name: 'Buckle' }] };
  TestBed.configureTestingModule({
    providers: [
      {
        provide: ProjectStore,
        useValue: {
          reloading: signal('/meshes/two.obj'),
          textureSets: signal([
            { name: 'Body', material: 'Body' },
            { name: 'Strap', material: 'Strap' }
          ]),
          resolveReloading
        }
      },
      {
        provide: LibraryStore,
        useValue: {
          api: () => Promise.resolve({ GET: () => Promise.resolve({ data: facts, response: { ok: true } }) })
        }
      }
    ]
  });
  const fixture = TestBed.createComponent(MeshReloadDialog);
  await fixture.whenStable();
  await new Promise(resolve => setTimeout(resolve));
  await fixture.whenStable();
  return { dialog: fixture.componentInstance as unknown as Reassigning, resolveReloading };
}

describe('MeshReloadDialog', () => {
  it('offers a set on a vanished material the new materials no set holds, and reloads with the choice (#89)', async () => {
    const { dialog, resolveReloading } = await render();

    expect(dialog.vanished()).toEqual(['Strap']);
    expect(dialog.appeared()).toEqual(['Belt', 'Buckle']);

    dialog.choose('Strap', 'Belt');
    dialog.reload();

    expect(resolveReloading).toHaveBeenCalledWith(true, { Strap: 'Belt' });
  });

  it('reloads as it always did when nothing is chosen', async () => {
    const { dialog, resolveReloading } = await render();

    dialog.reload();

    expect(resolveReloading).toHaveBeenCalledWith(true, {});
  });
});
