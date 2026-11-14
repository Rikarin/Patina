import { Injectable, inject } from '@angular/core';
import { desktop } from '../core/desktop';
import { type FileChoice, FilePicker, type PickRequest } from '../core/file-picker';
import { SystemStatus } from '../core/system-status';
import { LibraryStore } from './library-store';

/** A library file's path on the Server's machine, from the library's own path there and the file's below it. */
export function pathOnServer(library: string, below: string): string {
  return `${library.replace(/[\\/]+$/u, '')}/${below}`;
}

/**
 * Choosing a picture or a mesh the way every field that reads one does (#4, ADR 0033): from what the library's
 * projects hold, the project in front's first, or From a File… on the desktop. With nothing of the kind in the
 * library, the desktop's own dialog opens at once, as there is nothing to pick from.
 */
@Injectable({ providedIn: 'root' })
export class FileChooser {
  private readonly picker = inject(FilePicker);
  private readonly library = inject(LibraryStore);
  private readonly status = inject(SystemStatus);

  /** A file of the library, by its path below it, or one of this machine; null when none was chosen. */
  async choose(request: PickRequest): Promise<FileChoice | null> {
    const bridge = desktop();
    if (bridge && !(await this.libraryHas(request))) {
      const file = await (request.kind === 'mesh'
        ? bridge.openMesh(this.status.meshes())
        : request.kind === 'folder'
          ? bridge.openFolder()
          : bridge.openImage(this.status.pictures()));
      return file ? { machine: file } : null;
    }

    return this.picker.pick(request);
  }

  /**
   * A file chosen, as a path on the Server's machine: what a bake or a project's mesh reads, which the Server copies in
   * where it keeps one. Null when none was chosen.
   */
  async path(request: PickRequest): Promise<string | null> {
    return this.onServer(await this.choose(request));
  }

  /**
   * A folder to write into, such as an export's or a bake's, as a path on the Server's machine: this machine's own
   * dialog on the desktop, and the library's folders elsewhere, where no dialog could see the Server's disk (#4). Null
   * when none was chosen.
   */
  async folder(title: string, project: string | null = null): Promise<string | null> {
    const bridge = desktop();
    if (bridge) {
      return bridge.openFolder();
    }

    return this.onServer(await this.picker.pick({ kind: 'folder', title, project }));
  }

  private onServer(choice: FileChoice | null): string | null {
    if (!choice) {
      return null;
    }

    if ('machine' in choice) {
      return choice.machine;
    }

    const state = this.status.state();
    return state.kind === 'online' && state.system.library ? pathOnServer(state.system.library, choice.library) : null;
  }

  private async libraryHas(request: PickRequest): Promise<boolean> {
    const client = await this.library.api();
    const query = { kind: request.kind, ...(request.project ? { project: request.project } : {}) };
    const listed = await client.GET('/api/v1/library/files', { params: { query } }).catch(() => null);
    return (listed?.data?.files.length ?? 0) > 0;
  }
}
