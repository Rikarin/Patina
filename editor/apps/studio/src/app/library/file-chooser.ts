import { Injectable, inject } from '@angular/core';
import { unwrap } from '@patina/api-client';
import { type DesktopBridge, desktop } from '../core/desktop';
import { type FileChoice, FilePicker, type PickKind, type PickRequest } from '../core/file-picker';
import { SystemStatus } from '../core/system-status';
import { LibraryStore } from './library-store';

/** A library file's path on the Server's machine, from the library's own path there and the file's below it. */
export function pathOnServer(library: string, below: string): string {
  return `${library.replace(/[\\/]+$/u, '')}/${below}`;
}

/** This machine's dialog for a kind of file, offering what the Server reads of it: its path, or null for none. */
export function openOnMachine(bridge: DesktopBridge, kind: PickKind, status: SystemStatus): Promise<string | null> {
  switch (kind) {
    case 'mesh':
      return bridge.openMesh(status.meshes());
    case 'folder':
      return bridge.openFolder();
    case 'font':
      return bridge.openImage(status.fonts(), 'Fonts');
    case 'vector':
      return bridge.openImage(status.vectors(), 'SVG Drawings');
    default:
      return bridge.openImage(status.pictures());
  }
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
      const file = await openOnMachine(bridge, request.kind, this.status);
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

  /**
   * Sends a file of this computer to the library (#4), for a client of a Server it cannot reach the disk of, and answers
   * with the file's path on the Server's machine, which every field that reads a file takes.
   */
  async send(file: Blob, name: string): Promise<string> {
    const client = await this.library.api();
    return unwrap(
      await client.POST('/api/v1/library/uploads', {
        params: { query: { name } },
        body: file as unknown as string,
        bodySerializer: (body: unknown) => body as BodyInit,
        headers: { 'Content-Type': 'application/octet-stream' }
      })
    ).path;
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
