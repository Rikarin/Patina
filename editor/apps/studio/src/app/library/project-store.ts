import { Injectable, computed, inject, signal } from '@angular/core';
import { type ProjectContents, type ProjectDocumentInfo, unwrap } from '@patina/api-client';
import { BakeStore } from '../bake/bake-store';
import { desktop } from '../core/desktop';
import { GraphStore } from '../graph/graph-store';
import { PaintStore } from '../paint/paint-store';
import { LibraryStore } from './library-store';

/** A project waiting on what to do with unsaved paint. */
interface Leaving {
  path: string;
  document: string | null;
}

/** What the app is working on: a project and one of its documents (ADR 0012). */
@Injectable({ providedIn: 'root' })
export class ProjectStore {
  private readonly library = inject(LibraryStore);
  private readonly paint = inject(PaintStore);
  private readonly graph = inject(GraphStore);
  private readonly bakes = inject(BakeStore);
  private readonly mutableCurrent = signal<ProjectContents | null>(null);
  private readonly mutableDocument = signal<ProjectDocumentInfo | null>(null);
  private readonly mutableFailure = signal<string | null>(null);
  private readonly mutableLeaving = signal<Leaving | null>(null);
  private readonly mutableOpened = signal(0);
  private quitting = false;

  readonly current = this.mutableCurrent.asReadonly();
  /** The document being worked on. */
  readonly document = this.mutableDocument.asReadonly();
  readonly failure = this.mutableFailure.asReadonly();
  /** What is waiting on unsaved paint, or the open project when the app is quitting. */
  readonly leaving = this.mutableLeaving.asReadonly();
  /** Goes up each time a document opens, for what shows it. */
  readonly opened = this.mutableOpened.asReadonly();
  /** The mesh the open document is on. */
  readonly mesh = computed(() => {
    const document = this.mutableDocument();
    return this.mutableCurrent()?.meshes.find(mesh => mesh.id === document?.mesh) ?? null;
  });
  /** The mesh's file name. */
  readonly meshName = computed(() => this.mesh()?.path.split('/').pop() ?? null);

  constructor() {
    desktop()?.onQuitRequested(() => {
      if (this.paint.state()?.history.dirty) {
        this.quitting = true;
        this.mutableLeaving.set({ path: this.mutableCurrent()?.path ?? '', document: null });
      }
    });
  }

  /** Opens a project at the document most likely wanted. With unsaved paint open, it asks first. */
  async open(path: string): Promise<void> {
    await this.openDocument(path, null);
  }

  /** Opens one document of a project, by its path below the library. */
  async openDocument(path: string, document: string | null): Promise<void> {
    const same = this.mutableCurrent()?.path === path;
    if (same && (document === null || this.mutableDocument()?.path === document)) {
      this.mutableOpened.update(count => count + 1);
      return;
    }

    if (this.paint.state()?.history.dirty) {
      this.mutableLeaving.set({ path, document });
      return;
    }

    await this.go(path, document);
  }

  /** Carries out what was waiting: saving first, dropping the edits, or staying. */
  async resolveLeaving(choice: 'save' | 'discard' | 'cancel'): Promise<void> {
    const leaving = this.mutableLeaving();
    const quitting = this.quitting;
    this.mutableLeaving.set(null);
    this.quitting = false;
    if (leaving === null || choice === 'cancel') {
      return;
    }

    if (choice === 'save') {
      await this.paint.act('save');
    }

    if (quitting) {
      this.graph.askToQuit();
    } else {
      await this.go(leaving.path, leaving.document);
    }
  }

  /** Puts the open texturing document on a mesh, from a file the Server can read. */
  async chooseMesh(mesh: string): Promise<void> {
    const project = this.mutableCurrent();
    const document = this.mutableDocument()?.id;
    if (!project || !document) {
      return;
    }

    this.mutableFailure.set(null);
    try {
      const client = await this.library.api();
      const body = { project: project.path, mesh };
      const added = unwrap(await client.POST('/api/v1/library/project/meshes', { body }));
      const name = mesh.split(/[\\/]/u).pop() ?? mesh;
      const id = added.meshes.find(held => held.path.endsWith(`/${name}`))?.id;
      if (!id) {
        return;
      }

      const contents = unwrap(
        await client.PATCH('/api/v1/library/project/document', {
          body: { project: project.path, document, mesh: id }
        })
      );
      this.mutableCurrent.set(contents);
      this.mutableDocument.set(contents.documents.find(held => held.id === document) ?? null);
      await this.paint.bindMesh(name);
    } catch (failure) {
      this.mutableFailure.set(messageOf(failure));
    }
  }

  /** Asks the user for a mesh and uses it. */
  async pickMesh(): Promise<void> {
    const mesh = await desktop()?.openMesh();
    if (mesh) {
      await this.chooseMesh(mesh);
    }
  }

  /** Forgets a project that has moved or gone, so that nothing keeps its old path. */
  async close(): Promise<void> {
    await this.paint.close();
    await this.clearViewport();
    this.mutableCurrent.set(null);
    this.mutableDocument.set(null);
  }

  /** A project's mesh leaves with it, or it would stay on screen and look open. */
  private async clearViewport(): Promise<void> {
    const client = await this.library.api();
    unwrap(await client.DELETE('/api/v1/viewport/mesh'));
    this.bakes.viewportChanged.update(count => count + 1);
  }

  private async go(path: string, wanted: string | null): Promise<void> {
    this.mutableFailure.set(null);
    try {
      const client = await this.library.api();
      const contents = unwrap(await client.GET('/api/v1/library/project', { params: { query: { path } } }));
      const document = contents.documents.find(held => held.path === wanted) ?? firstOf(contents.documents);
      await this.paint.close();
      await this.clearViewport();
      if (document?.kind === 'graph') {
        await this.graph.open(document.path);
      } else if (document) {
        await this.paint.open(document.path, false);
      }

      this.mutableCurrent.set(contents);
      this.mutableDocument.set(document ?? null);
      this.mutableOpened.update(count => count + 1);
    } catch (failure) {
      this.mutableFailure.set(messageOf(failure));
    }
  }
}

/** What opening a project means: its texturing, or else whatever it has. */
function firstOf(documents: readonly ProjectDocumentInfo[]): ProjectDocumentInfo | undefined {
  return documents.find(document => document.kind === 'texturing') ?? documents[0];
}

function messageOf(failure: unknown): string {
  return failure instanceof Error ? failure.message : String(failure);
}
