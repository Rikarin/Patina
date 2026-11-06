import { Injectable, computed, inject, signal } from '@angular/core';
import { type ProjectContents, unwrap } from '@patina/api-client';
import { desktop } from '../core/desktop';
import { BakeStore } from '../bake/bake-store';
import { GraphStore } from '../graph/graph-store';
import { PaintStore } from '../paint/paint-store';
import { LibraryStore } from './library-store';

/** The project the app is working on: what ties a mesh, its stack and its bakes together. */
@Injectable({ providedIn: 'root' })
export class ProjectStore {
  private readonly library = inject(LibraryStore);
  private readonly paint = inject(PaintStore);
  private readonly mutableCurrent = signal<ProjectContents | null>(null);
  private readonly mutableFailure = signal<string | null>(null);
  private readonly mutableLeaving = signal<string | null>(null);
  private readonly graph = inject(GraphStore);
  private quitting = false;
  private readonly mutableOpened = signal(0);
  private readonly bakes = inject(BakeStore);

  readonly current = this.mutableCurrent.asReadonly();
  readonly failure = this.mutableFailure.asReadonly();
  constructor() {
    desktop()?.onQuitRequested(() => {
      if (this.paint.state()?.history.dirty) {
        this.quitting = true;
        this.mutableLeaving.set(this.mutableCurrent()?.path ?? '');
      }
    });
  }

  /** The project waiting on what to do with unsaved paint, or the open one when the app is quitting. */
  readonly leaving = this.mutableLeaving.asReadonly();
  /** Goes up each time a project opens, for what shows it. */
  readonly opened = this.mutableOpened.asReadonly();
  /** The mesh's file name. */
  readonly meshName = computed(() => this.mutableCurrent()?.mesh?.split('/').pop() ?? null);

  /** Opens a project, below `projects/`. With unsaved paint in the one that is open, it asks first. */
  async open(path: string): Promise<void> {
    if (this.mutableCurrent()?.path === path) {
      this.mutableOpened.update(count => count + 1);
      return;
    }

    if (this.paint.state()?.history.dirty) {
      this.mutableLeaving.set(path);
      return;
    }

    await this.go(path);
  }

  /** Carries out what was waiting: saving first, dropping the edits, or staying. */
  async resolveLeaving(choice: 'save' | 'discard' | 'cancel'): Promise<void> {
    const path = this.mutableLeaving();
    const quitting = this.quitting;
    this.mutableLeaving.set(null);
    this.quitting = false;
    if (path === null || choice === 'cancel') {
      return;
    }

    if (choice === 'save') {
      await this.paint.act('save');
    }

    if (quitting) {
      this.graph.askToQuit();
    } else {
      await this.go(path);
    }
  }

  /** Gives the open project a mesh, from a file the Server can read, and puts its stack on it. */
  async chooseMesh(mesh: string): Promise<void> {
    const project = this.mutableCurrent();
    if (!project) {
      return;
    }

    this.mutableFailure.set(null);
    try {
      const client = await this.library.api();
      const contents = unwrap(
        await client.PUT('/api/v1/library/project', { body: { path: project.path, mesh } })
      );
      this.mutableCurrent.set(contents);
      await this.paint.bindMesh(this.meshName() ?? mesh);
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
  }

  /** A project's mesh leaves with it, or it would stay on screen and look open. */
  private async clearViewport(): Promise<void> {
    const client = await this.library.api();
    unwrap(await client.DELETE('/api/v1/viewport/mesh'));
    this.bakes.viewportChanged.update(count => count + 1);
  }

  private async go(path: string): Promise<void> {
    this.mutableFailure.set(null);
    try {
      const client = await this.library.api();
      let contents = unwrap(await client.GET('/api/v1/library/project', { params: { query: { path } } }));
      if (contents.stack === null || contents.stack === undefined) {
        contents = unwrap(await client.PUT('/api/v1/library/project', { body: { path } }));
      }

      await this.paint.close();
      await this.clearViewport();
      await this.paint.open(contents.stack ?? '', false);
      this.mutableCurrent.set(contents);
      this.mutableOpened.update(count => count + 1);
    } catch (failure) {
      this.mutableFailure.set(messageOf(failure));
    }
  }
}

function messageOf(failure: unknown): string {
  return failure instanceof Error ? failure.message : String(failure);
}
