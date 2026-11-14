import { Injectable, signal } from '@angular/core';

/** What a picker lists: the pictures, the meshes or the folders the library's projects hold. */
export type PickKind = 'picture' | 'mesh' | 'folder';

/** What a person is asked to pick. */
export interface PickRequest {
  kind: PickKind;
  /** The dialog's title, which says what the file is for. */
  title: string;
  /** The project whose own files come first, below `projects/`. */
  project: string | null;
}

/** A file picked: one the library holds, by its path below it, or one of this machine, by its full path. */
export type FileChoice = { library: string } | { machine: string };

/**
 * Picking a file from the library rather than typing a path (#4). The library's own files are listed by the Server,
 * so a client of a Server on another machine can pick too; this machine's files come from its dialog, where there is
 * one. The dialog that answers is deferred, and comes the first time a pick is asked for.
 */
@Injectable({ providedIn: 'root' })
export class FilePicker {
  private readonly asked = signal<PickRequest | null>(null);
  private resolve: ((choice: FileChoice | null) => void) | null = null;

  /** What is being picked, while the dialog is open. */
  readonly request = this.asked.asReadonly();

  /** Asks for a file, and answers with it, or with null when the person picked none. */
  pick(request: PickRequest): Promise<FileChoice | null> {
    this.answer(null); // a pick still open is one nobody finished
    this.asked.set(request);
    return new Promise(resolve => {
      this.resolve = resolve;
    });
  }

  /** The dialog's answer, which closes it. */
  answer(choice: FileChoice | null): void {
    const resolve = this.resolve;
    this.resolve = null;
    this.asked.set(null);
    resolve?.(choice);
  }
}
