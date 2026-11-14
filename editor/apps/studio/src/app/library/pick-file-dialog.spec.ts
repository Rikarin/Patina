import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FilePicker } from '../core/file-picker';
import { SystemStatus } from '../core/system-status';
import { FileChooser } from './file-chooser';
import { LibraryStore } from './library-store';
import { PickFileDialog } from './pick-file-dialog';

describe('PickFileDialog outside the desktop app (#4)', () => {
  function dialog() {
    const send = vi.fn((_file: Blob, name: string) => Promise.resolve(`/srv/Game.ptlib/uploads/ab12/${name}`));
    TestBed.configureTestingModule({
      providers: [
        {
          provide: LibraryStore,
          useValue: {
            api: () =>
              Promise.resolve({
                GET: () => Promise.resolve({ data: { files: [], more: false }, response: { ok: true } })
              })
          }
        },
        {
          provide: SystemStatus,
          useValue: {
            state: signal({ kind: 'online', system: { library: '/srv/Game.ptlib' } }),
            meshes: signal(['.glb', '.fbx']),
            pictures: signal(['.png', '.jpg'])
          }
        },
        { provide: FileChooser, useValue: { send } }
      ]
    });
    const fixture = TestBed.createComponent(PickFileDialog);
    fixture.detectChanges();
    return { fixture, picker: TestBed.inject(FilePicker), send };
  }

  const sender = () => document.querySelector<HTMLElement>('[data-testid=pick-file-send]');

  it('sends a mesh chosen on this computer, and answers with its path on the Server', async () => {
    const { fixture, picker, send } = dialog();
    const answer = picker.pick({ kind: 'mesh', title: 'Choose a Mesh', project: null });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const input = sender()?.querySelector<HTMLInputElement>('input[type=file]');
    expect(input?.accept).toBe('.glb,.fbx');
    const file = new File(['mesh'], 'crate.glb');
    Object.defineProperty(input, 'files', { value: [file] });
    input?.dispatchEvent(new Event('change', { bubbles: true }));

    expect(await answer).toEqual({ machine: '/srv/Game.ptlib/uploads/ab12/crate.glb' });
    expect(send).toHaveBeenCalledWith(file, 'crate.glb');
  });

  it('sends nothing for a folder, which no file of this computer can be', async () => {
    const { fixture, picker } = dialog();
    void picker.pick({ kind: 'folder', title: 'Choose a Folder', project: null });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(document.querySelector('[data-testid=pick-file]')).not.toBeNull();
    expect(sender()).toBeNull();
    picker.answer(null);
  });
});
