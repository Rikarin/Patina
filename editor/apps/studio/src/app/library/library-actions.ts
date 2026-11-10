import { Injectable, computed, inject, signal } from '@angular/core';
import { type AssetSummary, type BinList, type ProjectContents, type ProjectSummary, unwrap } from '@patina/api-client';
import { messageOf } from '../core/messages';
import { LibrarySelection } from './library-selection';
import { LibraryStore } from './library-store';
import { LibraryTreeStore } from './library-tree-store';
import { ProjectStore } from './project-store';

/** A project or a document, as every list of the Library names it, so one menu and one dialog serve them all. */
export type LibraryTarget =
  | { kind: 'project'; path: string; name: string; tags: readonly string[] }
  | {
      kind: 'document';
      /** Its project, below `projects/`. */
      project: string;
      id: string;
      name: string;
      /** Its file below the library. */
      path: string;
      tags: readonly string[];
      /** What it is: its document kind, or a graph's role, as the index has it. */
      documentKind: string;
    };

/** A project as a target. */
export function projectTarget(project: ProjectSummary | ProjectContents): LibraryTarget {
  return { kind: 'project', path: project.path, name: project.name, tags: project.tags };
}

/** A material of the index as a target: its project is the folder its file is in. */
export function assetTarget(asset: AssetSummary): LibraryTarget {
  const below = asset.path.replace(/^projects\//, '');
  return {
    kind: 'document',
    project: below.slice(0, Math.max(0, below.lastIndexOf('/'))),
    id: asset.id,
    name: asset.name,
    path: asset.path,
    tags: asset.tags,
    documentKind: asset.kind
  };
}

/** The picture a target is asked for by: a document's file, or a project's folder below the library. */
export function thumbnailSource(target: LibraryTarget): string {
  return target.kind === 'project' ? `projects/${target.path}` : target.path;
}

/**
 * What can be done to what the Library lists, from any of its lists: the tree, the list, the grid and the Inspector.
 * Files are the truth, so each change goes to the Server, which writes them and asks the index to catch up. Deleting
 * goes to the library's bin, never straight away. Which dialog is open is here too, so one set of dialogs serves every
 * list.
 */
@Injectable({ providedIn: 'root' })
export class LibraryActions {
  private readonly library = inject(LibraryStore);
  private readonly tree = inject(LibraryTreeStore);
  private readonly project = inject(ProjectStore);
  private readonly selection = inject(LibrarySelection);

  /** What is being renamed, tagged or deleted, waiting for its dialog's answer. */
  readonly renaming = signal<LibraryTarget | null>(null);
  readonly tagging = signal<LibraryTarget | null>(null);
  readonly deleting = signal<LibraryTarget | null>(null);
  /** The project a new document is being made in. */
  readonly adding = signal<string | null>(null);
  readonly binOpen = signal(false);
  /** The last thing that went wrong, which the dialog or list it came from shows. */
  readonly failure = signal<string | null>(null);
  /** Whether a dialog has been asked for: they load the first time one is. */
  readonly asking = computed(
    () => this.renaming() !== null || this.tagging() !== null || this.deleting() !== null || this.adding() !== null
  );

  /** Opens a project, or a document of one in its workspace. */
  open(target: LibraryTarget): void {
    if (target.kind === 'project') {
      void this.project.open(target.path);
    } else {
      void this.project.openDocument(target.project, target.path);
    }
  }

  async rename(target: LibraryTarget, name: string): Promise<boolean> {
    return this.attempt(async () => {
      const client = await this.library.api();
      if (target.kind === 'project') {
        unwrap(await client.PATCH('/api/v1/library/project', { body: { path: target.path, name } }));
      } else {
        const body = { project: target.project, document: target.id, name };
        this.project.adopt(unwrap(await client.PATCH('/api/v1/library/project/document/name', { body })));
      }
    });
  }

  async setTags(target: LibraryTarget, tags: readonly string[]): Promise<boolean> {
    return this.attempt(async () => {
      const client = await this.library.api();
      if (target.kind === 'project') {
        const body = { path: target.path, tags: [...tags] };
        this.project.adopt(unwrap(await client.PATCH('/api/v1/library/project/tags', { body })));
      } else {
        const body = { project: target.project, document: target.id, tags: [...tags] };
        this.project.adopt(unwrap(await client.PATCH('/api/v1/library/project/document/tags', { body })));
      }
    });
  }

  /** Moves to the bin. What is open closes first, unless it has unsaved work, which stops it. */
  async delete(target: LibraryTarget): Promise<boolean> {
    const released =
      target.kind === 'project'
        ? await this.project.release([target.path])
        : await this.project.releaseDocument(target.path);
    if (!released) {
      this.failure.set(this.project.failure());
      return false;
    }

    const done = await this.attempt(async () => {
      const client = await this.library.api();
      if (target.kind === 'project') {
        unwrap(await client.DELETE('/api/v1/library/project', { params: { query: { path: target.path } } }));
        this.tree.clearChoice();
      } else {
        const query = { project: target.project, document: target.id };
        unwrap(await client.DELETE('/api/v1/library/project/document', { params: { query } }));
        await this.project.refreshContents(target.project);
      }
    });
    // What the Inspector showed is in the bin now.
    const shown = this.selection.item();
    const id = target.kind === 'project' ? null : target.id;
    if (
      done &&
      shown !== null &&
      ((shown.kind === 'project' && target.kind === 'project' && shown.project.path === target.path) ||
        (shown.kind === 'asset' && shown.asset.id === id))
    ) {
      this.selection.clear();
    }

    return done;
  }

  /** Makes a document of a kind in a project. */
  async addDocument(
    project: string,
    kind: string,
    name: string,
    extra: { mesh?: string; image?: string }
  ): Promise<boolean> {
    return this.attempt(async () => {
      const client = await this.library.api();
      const body = { project, kind, name, mesh: extra.mesh ?? null, image: extra.image ?? null };
      const contents = unwrap(await client.POST('/api/v1/library/project/documents', { body }));
      this.project.adopt(contents);
      const made = contents.documents.at(-1);
      if (made) {
        void this.project.openDocument(project, made.path);
      }
    });
  }

  /** Pins a project's reference to an asset at what it is now, or lets it follow the asset again. */
  async pin(project: string, asset: string, pinned: boolean): Promise<boolean> {
    return this.attempt(async () => {
      const client = await this.library.api();
      unwrap(await client.PATCH('/api/v1/library/project/reference', { body: { project, asset, pinned } }));
    });
  }

  async bin(): Promise<BinList> {
    const client = await this.library.api();
    return unwrap(await client.GET('/api/v1/library/bin'));
  }

  async restore(id: string): Promise<boolean> {
    return this.attempt(async () => {
      const client = await this.library.api();
      unwrap(await client.POST('/api/v1/library/bin/restore', { body: { id } }));
    });
  }

  async deleteForGood(id: string): Promise<boolean> {
    return this.attempt(async () => {
      const client = await this.library.api();
      unwrap(await client.DELETE('/api/v1/library/bin/{id}', { params: { path: { id } } }));
    });
  }

  async emptyBin(): Promise<boolean> {
    return this.attempt(async () => {
      const client = await this.library.api();
      unwrap(await client.DELETE('/api/v1/library/bin'));
    });
  }

  /** Runs a change, and when it went through, has every list read the library again. */
  private async attempt(change: () => Promise<void>): Promise<boolean> {
    this.failure.set(null);
    try {
      await change();
    } catch (failure) {
      this.failure.set(messageOf(failure));
      return false;
    }

    this.library.refreshSoon();
    await this.tree.reload();
    return true;
  }
}
