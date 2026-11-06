import { Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { type ProjectContents, type ProjectDocumentInfo, unwrap } from '@patina/api-client';
import { BakeStore } from '../bake/bake-store';
import { desktop } from '../core/desktop';
import { UnsavedWork } from '../core/unsaved-work';
import type { WorkspaceName } from '../core/workspaces';
import { GraphStore } from '../graph/graph-store';
import { PaintStore } from '../paint/paint-store';
import { LibraryStore } from './library-store';

/** An open document. Its session stays on the Server while another tab is in front, with all its edits and history. */
export interface DocumentTab {
  path: string;
  name: string;
  kind: string;
  /** The project it belongs to, or null for a shared graph. */
  project: string | null;
  dirty: boolean;
  /** Its session on the Server, once it has been in front. */
  session: string | null;
}

/** What opening a project means: its texturing, or else whatever it has. */
function firstOf(documents: readonly ProjectDocumentInfo[]): ProjectDocumentInfo | undefined {
  return documents.find(document => document.kind === 'texturing') ?? documents[0];
}

function messageOf(failure: unknown): string {
  return failure instanceof Error ? failure.message : String(failure);
}

/**
 * What the app is working on: the open documents and the one in front (ADR 0012). The Server owns every document, so a
 * tab is little more than a path: going to it joins its session again, and PaintStore and GraphStore show whichever
 * one is in front.
 */
@Injectable({ providedIn: 'root' })
export class ProjectStore {
  private readonly library = inject(LibraryStore);
  private readonly paint = inject(PaintStore);
  private readonly graph = inject(GraphStore);
  private readonly bakes = inject(BakeStore);
  private readonly unsaved = inject(UnsavedWork);
  private readonly mutableTabs = signal<readonly DocumentTab[]>([]);
  private readonly mutableActive = signal<string | null>(null);
  private readonly mutableContents = signal<ReadonlyMap<string, ProjectContents>>(new Map());
  private readonly mutableFailure = signal<string | null>(null);
  private readonly mutableClosing = signal<string | null>(null);
  private readonly mutableQuitting = signal(false);
  private readonly mutableNewKind = signal<{ kind: string } | null>(null);
  private switching = false;

  readonly tabs = this.mutableTabs.asReadonly();
  readonly failure = this.mutableFailure.asReadonly();
  /** The tab waiting on what to do with its unsaved edits. */
  readonly closing = this.mutableClosing.asReadonly();
  /** The app wants to quit and something is not saved. */
  readonly quitting = this.mutableQuitting.asReadonly();
  /** A kind of document someone asked to start a project with. */
  readonly newKind = this.mutableNewKind.asReadonly();

  readonly active = computed(() => this.mutableTabs().find(tab => tab.path === this.mutableActive()) ?? null);
  readonly workspace = computed<WorkspaceName>(() => (this.active()?.kind ?? 'start') as WorkspaceName);
  /** The project of the document in front. */
  readonly current = computed(() => {
    const project = this.active()?.project;
    return project ? (this.mutableContents().get(project) ?? null) : null;
  });
  /** The document in front, as its project lists it. */
  readonly document = computed(() => {
    const path = this.mutableActive();
    return this.current()?.documents.find(document => document.path === path) ?? null;
  });
  /** The mesh the document in front is on. */
  readonly mesh = computed(() => {
    const document = this.document();
    return this.current()?.meshes.find(mesh => mesh.id === document?.mesh) ?? null;
  });
  /** The mesh's file name. */
  readonly meshName = computed(() => this.mesh()?.path.split('/').pop() ?? null);
  /** The tabs with edits that are not saved. */
  readonly unsavedTabs = computed(() => this.mutableTabs().filter(tab => tab.dirty));

  constructor() {
    // What is in front reports its own edits, and a tab behind keeps what it last reported.
    effect(() => {
      const tab = this.active();
      const dirty = tab?.kind === 'graph' ? this.graph.unsaved() : this.paint.state()?.history.dirty === true;
      if (tab && !this.switching && tab.dirty !== dirty) {
        untracked(() => this.update(tab.path, { dirty }));
      }
    });
    effect(() => {
      this.unsaved.report('documents', this.unsavedTabs().length > 0);
    });

    // A graph can open from anywhere, and it comes here as a tab.
    effect(() => {
      const document = this.graph.document();
      if (this.switching) {
        return;
      }

      if (document) {
        untracked(() => this.adoptGraph(document.path, document.name, document.id));
      } else if (this.graph.session().kind === 'none') {
        untracked(() => this.graphClosed());
      }
    });

    desktop()?.onQuitRequested(() => {
      this.mutableQuitting.set(true);
    });
  }

  /** Opens a project at the document most likely wanted. */
  async open(path: string): Promise<void> {
    await this.openDocument(path, null);
  }

  /** Opens one document of a project, by its path below the library, or goes to it if it is open. */
  async openDocument(project: string, wanted: string | null): Promise<void> {
    this.mutableFailure.set(null);
    try {
      const contents = await this.read(project);
      const document = contents.documents.find(held => held.path === wanted) ?? firstOf(contents.documents);
      if (!document) {
        return;
      }

      const open = this.mutableTabs().find(tab => tab.path === document.path);
      if (!open) {
        this.mutableTabs.update(tabs => [...tabs, tabOf(document, project)]);
      }

      await this.activate(document.path);
    } catch (failure) {
      this.mutableFailure.set(messageOf(failure));
    }
  }

  /** Brings a tab to the front. The one behind stays open on the Server. */
  async activate(path: string): Promise<void> {
    const tab = this.mutableTabs().find(held => held.path === path);
    if (!tab || (this.mutableActive() === path && this.isShowing(tab))) {
      return;
    }

    this.switching = true;
    try {
      this.detach();
      await this.clearViewport();
      this.mutableActive.set(path);
      if (tab.kind === 'graph') {
        await this.graph.open(path);
        this.update(path, { session: this.graph.document()?.id ?? null });
      } else {
        await this.paint.open(path, false);
        this.update(path, { session: this.paint.state()?.id ?? null });
      }
    } catch (failure) {
      this.mutableFailure.set(messageOf(failure));
    } finally {
      this.switching = false;
    }
  }

  /** Closes a tab, asking first if it has edits that are not saved. */
  async closeTab(path: string): Promise<void> {
    const tab = this.mutableTabs().find(held => held.path === path);
    if (tab?.dirty) {
      this.mutableClosing.set(path);
      return;
    }

    await this.remove(path, false);
  }

  /** Carries out what was waiting on unsaved edits. */
  async resolveClosing(choice: 'save' | 'discard' | 'cancel'): Promise<void> {
    const path = this.mutableClosing();
    this.mutableClosing.set(null);
    if (path === null || choice === 'cancel') {
      return;
    }

    if (choice === 'save') {
      await this.activate(path);
      await this.saveActive();
    }

    await this.remove(path, choice === 'discard');
  }

  /** Carries out the answer to quitting with unsaved edits. */
  async resolveQuitting(choice: 'save' | 'discard' | 'cancel'): Promise<void> {
    this.mutableQuitting.set(false);
    if (choice === 'cancel') {
      return;
    }

    if (choice === 'save') {
      for (const tab of this.unsavedTabs()) {
        await this.activate(tab.path);
        await this.saveActive();
      }
    }

    desktop()?.quit();
  }

  /** Saves what is in front. */
  async saveActive(): Promise<void> {
    await (this.active()?.kind === 'graph' ? this.graph.save() : this.paint.act('save'));
  }

  /** Asks to start a project with this kind of document. */
  startProject(kind: string): void {
    this.mutableNewKind.set({ kind });
  }

  /** Puts the texturing document in front on a mesh, from a file the Server can read. */
  async chooseMesh(mesh: string): Promise<void> {
    const project = this.current();
    const document = this.document()?.id;
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
      this.mutableContents.update(all => new Map(all).set(project.path, contents));
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

  /**
   * Closes every document below a folder or project, which cannot move while something has it open. False when one has
   * unsaved edits, which only its owner can decide about.
   */
  async release(paths: readonly string[]): Promise<boolean> {
    const inside = this.mutableTabs().filter(tab =>
      paths.some(path => tab.project === path || tab.project?.startsWith(`${path}/`))
    );
    const dirty = inside.find(tab => tab.dirty);
    if (dirty) {
      this.mutableFailure.set(`${dirty.name} has unsaved changes. Save it first.`);
      return false;
    }

    for (const tab of inside) {
      await this.remove(tab.path, false);
    }

    return true;
  }

  private isShowing(tab: DocumentTab): boolean {
    return tab.kind === 'graph' ? this.graph.document() !== null : this.paint.state() !== null;
  }

  /** Lets go of what is in front, whose session stays open on the Server. */
  private detach(): void {
    this.paint.detach();
    this.graph.detach();
  }

  private adoptGraph(path: string, name: string, session: string): void {
    const relative = path.includes('/projects/') ? path.slice(path.indexOf('/projects/') + 1) : path;
    const known = this.mutableTabs().find(tab => tab.path === path || tab.path === relative);
    if (known) {
      this.mutableActive.set(known.path);
      this.update(known.path, { session });
      return;
    }

    this.paint.detach();
    const tab = { path, name, kind: 'graph', project: null, dirty: false, session };
    this.mutableTabs.update(tabs => [...tabs, tab]);
    this.mutableActive.set(path);
  }

  /** The graph's own Close ended its session, so only its tab is left to go. */
  private graphClosed(): void {
    const tab = this.active();
    if (tab?.kind === 'graph') {
      this.mutableTabs.update(tabs => tabs.filter(held => held.path !== tab.path));
      this.mutableActive.set(null);
      const next = this.mutableTabs().at(-1);
      if (next) {
        void this.activate(next.path);
      }
    }
  }

  private async remove(path: string, discard: boolean): Promise<void> {
    const tab = this.mutableTabs().find(held => held.path === path);
    if (!tab) {
      return;
    }

    this.switching = true;
    try {
      if (this.mutableActive() === path) {
        this.detach();
        this.mutableActive.set(null);
        await this.clearViewport();
      }

      await this.closeSession(tab, discard);
    } catch (failure) {
      this.mutableFailure.set(messageOf(failure));
    } finally {
      this.switching = false;
    }

    this.mutableTabs.update(tabs => tabs.filter(held => held.path !== path));
    const next = this.mutableTabs().at(-1);
    if (next && this.mutableActive() === null) {
      await this.activate(next.path);
    }
  }

  private async closeSession(tab: DocumentTab, discard: boolean): Promise<void> {
    if (tab.session === null) {
      return;
    }

    const client = await this.library.api();
    const id = tab.session;
    await (tab.kind === 'graph'
      ? client.DELETE('/api/v1/graphs/{id}', { params: { path: { id }, query: { discard } } })
      : client.DELETE('/api/v1/stacks/{id}', { params: { path: { id } } }));
  }

  private update(path: string, change: Partial<DocumentTab>): void {
    this.mutableTabs.update(tabs => tabs.map(tab => (tab.path === path ? { ...tab, ...change } : tab)));
  }

  private async read(path: string): Promise<ProjectContents> {
    const client = await this.library.api();
    const contents = unwrap(await client.GET('/api/v1/library/project', { params: { query: { path } } }));
    this.mutableContents.update(all => new Map(all).set(path, contents));
    return contents;
  }

  /** A document's mesh leaves with it, or it would stay on screen and look open. */
  private async clearViewport(): Promise<void> {
    const client = await this.library.api();
    unwrap(await client.DELETE('/api/v1/viewport/mesh'));
    this.bakes.viewportChanged.update(count => count + 1);
  }
}

function tabOf(document: ProjectDocumentInfo, project: string): DocumentTab {
  return { path: document.path, name: document.name, kind: document.kind, project, dirty: false, session: null };
}
