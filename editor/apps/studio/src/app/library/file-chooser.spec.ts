import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FilePicker } from '../core/file-picker';
import { SystemStatus } from '../core/system-status';
import { FileChooser, pathOnServer } from './file-chooser';
import { LibraryStore } from './library-store';

const library = 'C:\\Art\\Game.ptlib';

function chooser(files: readonly { path: string; name: string; project: string }[]) {
  const listed = vi.fn((_path: string, _init: unknown) => Promise.resolve({ data: { files, more: false } }));
  TestBed.configureTestingModule({
    providers: [
      { provide: LibraryStore, useValue: { api: () => Promise.resolve({ GET: listed }) } },
      {
        provide: SystemStatus,
        useValue: {
          state: signal({ kind: 'online', system: { library } }),
          meshes: signal(['glb']),
          pictures: signal(['png'])
        }
      }
    ]
  });
  return { chooser: TestBed.inject(FileChooser), picker: TestBed.inject(FilePicker), listed };
}

afterEach(() => {
  delete (globalThis as { patina?: unknown }).patina;
});

describe('FileChooser', () => {
  it('gives a file the library holds as its path on the Server, the way a bake reads it (#4)', async () => {
    const { chooser: files, picker } = chooser([
      { path: 'projects/crate/crate_high.glb', name: 'crate_high.glb', project: 'crate' }
    ]);

    const chosen = files.path({ kind: 'mesh', title: 'Choose a Mesh', project: 'crate' });
    await vi.waitFor(() => expect(picker.request()?.kind).toBe('mesh'));
    picker.answer({ library: 'projects/crate/crate_high.glb' });

    expect(await chosen).toBe('C:\\Art\\Game.ptlib/projects/crate/crate_high.glb');
  });

  it('opens the system dialog at once on the desktop when the library has none of the kind', async () => {
    const openMesh = vi.fn(() => Promise.resolve('D:\\Meshes\\crate.glb'));
    Object.assign(globalThis, { patina: { openMesh, openImage: vi.fn() } });
    const { chooser: files, picker, listed } = chooser([]);

    expect(await files.path({ kind: 'mesh', title: 'Choose a Mesh', project: null })).toBe('D:\\Meshes\\crate.glb');
    expect(openMesh).toHaveBeenCalledWith(['glb']);
    expect(listed).toHaveBeenCalledWith('/api/v1/library/files', { params: { query: { kind: 'mesh' } } });
    expect(picker.request()).toBeNull();
  });

  it('lists the library outside the desktop app, where no dialog of this machine could see the Server', async () => {
    const { chooser: files, picker, listed } = chooser([]);

    const chosen = files.path({ kind: 'picture', title: 'Choose a Picture', project: null });
    await vi.waitFor(() => expect(picker.request()?.kind).toBe('picture'));
    picker.answer(null);

    expect(await chosen).toBeNull();
    expect(listed).not.toHaveBeenCalled();
  });

  it('joins a library path to the library once, whatever the library path ends in', () => {
    expect(pathOnServer('/srv/Game.ptlib/', 'projects/a/a.glb')).toBe('/srv/Game.ptlib/projects/a/a.glb');
    expect(pathOnServer('C:\\Game.ptlib\\', 'projects/a/a.glb')).toBe('C:\\Game.ptlib/projects/a/a.glb');
  });
});
