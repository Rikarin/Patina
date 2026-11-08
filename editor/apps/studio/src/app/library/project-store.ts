import { Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import {
  ApiError,
  type ProjectContents,
  type ProjectBakeSettings,
  type ProjectDocumentInfo,
  type UnwrapOptions,
  type ViewportDisplay,
  unwrap
} from '@patina/api-client';
import { BakeStore } from '../bake/bake-store';
import { desktop } from '../core/desktop';
import { SystemStatus } from '../core/system-status';
import { UnsavedWork } from '../core/unsaved-work';
import type { WorkspaceName } from '../core/workspaces';
import { GraphStore } from '../graph/graph-store';
import { PaintStore } from '../paint/paint-store';
import { LibraryStore } from './library-store';
import { messageOf } from '../core/messages';

/** An open document. Its session stays on the Server while another tab is in front, with all its edits and history. */
export interface DocumentTab {
  path: string;
  name: string;
  kind: string;
  /** The project it belongs to. */
  project: string | null;
  dirty: boolean;
  /** Its session on the Server, once it has been in front. */
  session: string | null;
}

/** Whether a tab is a graph underneath, which an image material is. */
function isGraph(tab: DocumentTab | null | undefined): boolean {
  return tab?.kind === 'graph' || tab?.kind === 'image-material';
}

/** What opening a project means: its texturing, or else whatever it has. */
function firstOf(documents: readonly ProjectDocumentInfo[]): ProjectDocumentInfo | undefined {
  return documents.find(document => document.kind === 'texturing') ?? documents[0];
}

/**
 * What the app is working on: the open documents and the one in front (ADR 0012). The Server owns every document, so a
 * tab is little more than a path: going to it joins its session again, and PaintStore and GraphStore show whichever
 * one is in front.
 */
const LastDocumentKey = 'patina.library.last.';

function remembered(key: string): { project: string; path: string } | null {
  try {
    const saved = globalThis.localStorage.getItem(key);
    return saved ? (JSON.parse(saved) as { project: string; path: string }) : null;
  } catch {
    return null;
  }
}

function remember(key: string, value: string | null): void {
  try {
    if (value === null) {
      globalThis.localStorage.removeItem(key);
    } else {
      globalThis.localStorage.setItem(key, value);
    }
  } catch {
    // A window with no storage just starts empty next time.
  }
}

@Injectable({ providedIn: 'root' })
export class ProjectStore {
  private readonly library = inject(LibraryStore);
  private readonly paint = inject(PaintStore);
  private readonly graph = inject(GraphStore);
  private readonly bakes = inject(BakeStore);
  private readonly unsaved = inject(UnsavedWork);
  private readonly status = inject(SystemStatus);
  private readonly mutableTabs = signal<readonly DocumentTab[]>([]);
  private readonly mutableActive = signal<string | null>(null);
  private readonly mutableContents = signal<ReadonlyMap<string, ProjectContents>>(new Map());
  private readonly mutableFailure = signal<string | null>(null);
  private readonly mutableNotice = signal<string | null>(null);
  private readonly mutableReloading = signal<string | null>(null);
  private readonly mutableClosing = signal<string | null>(null);
  private readonly mutableQuitting = signal(false);
  private readonly mutableNewKind = signal<{ kind: string } | null>(null);
  /** The Project Settings dialog is up. */
  readonly settingsOpen = signal(false);
  /** Why Project Settings could not apply. It is said in the dialog, which covers everything else. */
  readonly settingsFailure = signal<string | null>(null);
  private readonly mutableUnwrapping = signal(false);
  /** A mesh is being given a UV layout, which can take a while on a large one. */
  readonly unwrapping = this.mutableUnwrapping.asReadonly();
  private readonly mutableBakeAsked = signal(0);
  private readonly linkedBakes = new Set<string>();
  private switching = false;
  /**
   * The tab on its way to the front, done when it is there. A new tab shows before it is in front, while the viewport
   * lets go of what was, and something sent in between would otherwise find no project open.
   */
  private bringing: Promise<void> = Promise.resolve();
  private restored: string | null = null;

  readonly tabs = this.mutableTabs.asReadonly();
  /** Goes up each time something asks for the Bake dialog. */
  readonly bakeAsked = this.mutableBakeAsked.asReadonly();
  readonly failure = this.mutableFailure.asReadonly();
  /** The mesh file waiting on whether to reload it, which costs open documents their undo. */
  readonly reloading = this.mutableReloading.asReadonly();
  /** What just worked, when nothing on screen would show it. */
  readonly notice = this.mutableNotice.asReadonly();
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
  /** The model's texture sets, one document per material. A model with one material has none to choose. */
  readonly textureSets = computed(() => (this.current()?.documents ?? []).filter(document => document.material));
  /** The bake layers read was made on a mesh the project no longer has. */
  readonly bakeOutOfDate = computed(() => this.current()?.bakes.at(-1)?.stale === true);
  /** The mesh's file name. */
  readonly meshName = computed(() => this.mesh()?.path.split('/').pop() ?? null);
  /** The tabs with edits that are not saved. */
  readonly unsavedTabs = computed(() => this.mutableTabs().filter(tab => tab.dirty));

  constructor() {
    // What is in front reports its own edits, and a tab behind keeps what it last reported.
    effect(() => {
      const tab = this.active();
      const dirty = isGraph(tab) ? this.graph.unsaved() : this.paint.state()?.history.dirty === true;
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

    // A bake of what is open is what its layers were waiting for, the same folder again included.
    // A bake of several texture sets is a job each, and each links its own set's layers as it finishes.
    effect(() => {
      for (const job of this.bakes.batch()) {
        const folder = job.bake?.output;
        if (job.state === 'done' && folder && !this.linkedBakes.has(job.id)) {
          this.linkedBakes.add(job.id);
          untracked(() => void this.linkBake(folder, job.bake?.material ?? null));
        }
      }
    });

    // A library reopens on what was last in front, and closing everything starts it empty next time.
    effect(() => {
      const state = this.library.state();
      const tab = this.active();
      if (state.kind !== 'ready') {
        return;
      }

      const key = LastDocumentKey + state.info.id;
      if (this.restored !== key) {
        this.restored = key;
        untracked(() => void this.reopen(key));
      } else if (tab) {
        remember(key, JSON.stringify({ project: tab.project, path: tab.path }));
      } else {
        remember(key, null);
      }
    });

    desktop()?.onQuitRequested(() => {
      this.mutableQuitting.set(true);
    });

    this.paint.onAction = () => {
      this.mutableFailure.set(null);
      this.mutableNotice.set(null);
    };
  }

  /** A project moved or deleted since is no failure of anything being done now, so it is forgotten quietly. */
  private async reopen(key: string): Promise<void> {
    const saved = remembered(key);
    if (!saved) {
      return;
    }

    await this.openDocument(saved.project, saved.path);
    if (!this.active()) {
      this.mutableFailure.set(null);
      remember(key, null);
    }
  }

  /** Opens a project at the document most likely wanted. */
  async open(path: string): Promise<void> {
    await this.openDocument(path, null);
  }

  /** Opens one document of a project, by its path below the library, or goes to it if it is open. */
  async openDocument(project: string, wanted: string | null): Promise<void> {
    this.startAction();
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
      await this.offerRecovery(document.path);
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

    this.bringing = this.bringToFront(tab);
    await this.bringing;
  }

  private async bringToFront(tab: DocumentTab): Promise<void> {
    const path = tab.path;
    this.switching = true;
    try {
      this.detach();
      await this.clearViewport();
      this.mutableActive.set(path);
      if (isGraph(tab)) {
        await this.graph.open(path);
        this.update(path, { session: this.graph.document()?.id ?? null });
      } else {
        await this.refreshMaterials(tab.project);
        await this.paint.open(path, false);
        this.update(path, { session: this.paint.state()?.id ?? null });
        if (tab.kind === 'layered-material') {
          await this.paint.wear();
          this.bakes.viewportChanged.update(count => count + 1);
        }
      }
    } catch (failure) {
      this.mutableFailure.set(messageOf(failure));
    } finally {
      this.switching = false;
    }
  }

  /** Closes a tab, asking first if it has edits that are not saved. */
  async closeTab(path: string): Promise<void> {
    this.startAction();
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

    this.startAction();
    if (choice === 'save') {
      await this.activate(path);
      if (!(await this.saveActive())) {
        return; // it stays open, and what stopped the save says why
      }
    }

    await this.remove(path, choice === 'discard');
  }

  /** Carries out the answer to quitting with unsaved edits. */
  async resolveQuitting(choice: 'save' | 'discard' | 'cancel'): Promise<void> {
    this.mutableQuitting.set(false);
    if (choice === 'cancel') {
      return;
    }

    // Quitting now would lose what could not be saved, so the app stays and says why.
    if (choice === 'save' && !(await this.saveAll('so Patina stayed open'))) {
      return;
    }

    desktop()?.quit();
  }

  /**
   * Saves every tab with unsaved edits, each brought to the front in turn. False at the first that could not be, which
   * is named in the failure.
   * @param after What failing means, said after the document's name.
   */
  async saveAll(after = ''): Promise<boolean> {
    return this.saveTabs(this.unsavedTabs(), after);
  }

  /**
   * Saves these tabs, each brought to the front in turn, and brings back the one that was in front. False at the first
   * that could not be, which is named in the failure.
   * @param after What failing means, said after the document's name.
   */
  async saveTabs(tabs: readonly { path: string; name: string }[], after = ''): Promise<boolean> {
    const front = this.mutableActive();
    try {
      for (const tab of tabs) {
        await this.activate(tab.path);
        if (!(await this.saveActive())) {
          this.mutableFailure.set(`${tab.name} could not be saved${after ? `, ${after}` : ''}.`);
          return false;
        }
      }
    } catch (failure) {
      this.mutableFailure.set(messageOf(failure));
      return false;
    }

    if (front && this.mutableActive() !== front) {
      await this.activate(front);
    }

    return true;
  }

  /** Shows an image material as the graph it is, for going further than its maps. */
  openAsGraph(): void {
    const tab = this.active();
    if (tab) {
      this.update(tab.path, { kind: 'graph' });
    }
  }

  /** Saves what is in front. False if it did not happen, and its store has said why. */
  async saveActive(): Promise<boolean> {
    this.startAction();
    return isGraph(this.active()) ? this.graph.save() : this.paint.act('save');
  }

  askToBake(): void {
    this.mutableBakeAsked.update(count => count + 1);
  }

  /** Asks to start a project with this kind of document. */
  startProject(kind: string): void {
    this.mutableNewKind.set({ kind });
  }

  /**
   * Sets what the whole project shares. A mesh rewrites what its documents stand on, so they close first and the one in
   * front opens again on it. That costs them their undo, and a document with unsaved edits has to be saved.
   */
  async applySettings(settings: {
    mesh?: string | null;
    high?: string | null;
    autoUnwrap?: boolean;
    preservePaint?: boolean;
    unwrap?: UnwrapOptions;
  }): Promise<boolean> {
    const project = this.current();
    if (!project) {
      return false;
    }

    const front = this.mutableActive();
    const moving = this.movesMesh(settings);
    this.startAction();
    this.settingsFailure.set(null);
    this.mutableUnwrapping.set(true);
    try {
      if (moving && !(await this.release([project.path]))) {
        return false;
      }

      const client = await this.library.api();
      const contents = unwrap(
        await client.PATCH('/api/v1/library/project/settings', {
          body: {
            project: project.path,
            mesh: settings.mesh ?? null,
            high: settings.high ?? null,
            autoUnwrap: settings.autoUnwrap ?? null,
            preservePaint: settings.preservePaint ?? null,
            unwrap: settings.unwrap ?? null
          }
        })
      );
      this.mutableContents.update(all => new Map(all).set(project.path, contents));
      this.paint.failure.set(null);
      if (moving) {
        await this.openDocument(project.path, front);
      }

      return true;
    } catch (failure) {
      if (moving) {
        await this.openDocument(project.path, front);
      }

      this.fail(messageOf(failure));
      return false;
    } finally {
      this.mutableUnwrapping.set(false);
    }
  }

  /** Whether settings would rewrite what the documents stand on, and so close them and lose their undo. */
  movesMesh(settings: { mesh?: string | null; autoUnwrap?: boolean; unwrap?: UnwrapOptions }): boolean {
    // New unwrap options lay out again a mesh Auto Unwrap wrote, which moves its paint like a new mesh.
    const relaid = !!settings.unwrap && (this.current()?.meshes ?? []).some(mesh => !!mesh.unwrapped);
    return !!settings.mesh || settings.autoUnwrap === true || relaid;
  }

  /**
   * Keeps how the project bakes, in its own file, so the next Bake, the next session and another client start from it.
   * Throws the Server's refusal.
   */
  async setBake(project: string, bake: ProjectBakeSettings): Promise<void> {
    const client = await this.library.api();
    const contents = unwrap(await client.PUT('/api/v1/library/project/bake', { body: { project, bake } }));
    this.mutableContents.update(all => new Map(all).set(project, contents));
  }

  /** Opens a project if it needs it, then its settings. */
  async openSettings(path: string): Promise<void> {
    if (this.current()?.path !== path) {
      await this.open(path);
    }

    this.settingsOpen.set(this.current()?.path === path);
  }

  /** Reads the mesh again from where it came from, as after an export from a modelling tool. */
  async reloadMesh(): Promise<void> {
    const project = this.current();
    const source = this.mesh()?.source;
    if (!source) {
      this.settingsOpen.set(project !== null); // it came in before Patina kept this, so it needs choosing once
      return;
    }

    this.startAction();
    try {
      const client = await this.library.api();
      const query = { project: project?.path ?? '', mesh: source };
      const { changed } = unwrap(await client.GET('/api/v1/library/project/mesh-change', { params: { query } }));
      if (changed) {
        this.mutableReloading.set(source);
      } else {
        this.mutableNotice.set('The mesh has not changed.');
      }
    } catch (failure) {
      this.mutableFailure.set(messageOf(failure));
    }
  }

  /** Carries out the answer to reloading a mesh that changed. */
  async resolveReloading(reload: boolean): Promise<void> {
    const source = this.mutableReloading();
    this.mutableReloading.set(null);
    if (reload && source !== null) {
      await this.applySettings({ mesh: source });
    }
  }

  /** What a new project starts with, before anything opens it. */
  async startSettings(path: string, autoUnwrap: boolean): Promise<void> {
    const client = await this.library.api();
    unwrap(
      await client.PATCH('/api/v1/library/project/settings', {
        body: { project: path, mesh: null, high: null, autoUnwrap, preservePaint: null }
      })
    );
  }

  /** Brings files into the open project. The Server says what each is, and refuses what nothing could read. */
  async importFiles(paths: readonly string[]): Promise<void> {
    await this.bringing; // a project made or opened a moment ago is the one meant
    const project = this.current();
    if (!project) {
      this.startAction();
      this.mutableFailure.set('Open a project to bring files into it.');
      return;
    }

    this.startAction();
    const imported: string[] = [];
    const refused: string[] = [];
    const client = await this.library.api();
    for (const path of paths) {
      try {
        const body = { project: project.path, file: path };
        const contents = unwrap(await client.POST('/api/v1/library/project/files', { body }));
        this.mutableContents.update(all => new Map(all).set(project.path, contents));
        imported.push(path.split(/[\\/]/u).pop() ?? path);
      } catch (failure) {
        refused.push(messageOf(failure));
      }
    }

    if (imported.length > 0) {
      const what = imported.length === 1 ? imported[0] : `${imported.length} files`;
      this.mutableNotice.set(`Imported ${what} into ${project.name}`);
      this.library.refreshSoon();
    }

    if (refused.length > 0) {
      this.mutableFailure.set(refused.join(' '));
    }
  }

  /** A name for Save As is being asked for. */
  readonly savingAs = signal(false);

  clearFailure(): void {
    this.mutableFailure.set(null);
  }

  /** Autosaved work for the document in front, which is offered until it is taken or refused. */
  readonly recoverable = signal<{ path: string; saved: string } | null>(null);

  /** Saves what is in front, then carries on in a copy under a new name. */
  async saveAs(name: string): Promise<void> {
    const tab = this.active();
    const project = tab?.project;
    const document = this.documentOf(tab);
    if (!project || !document?.id) {
      return;
    }

    this.startAction();
    try {
      if (!(await this.saveActive())) {
        return;
      }

      const client = await this.library.api();
      const body = { project, document: document.id, name };
      const contents = unwrap(await client.POST('/api/v1/library/project/document/copy', { body }));
      this.mutableContents.update(all => new Map(all).set(project, contents));
      const copy = contents.documents.at(-1);
      this.library.refreshSoon();
      if (copy) {
        await this.openDocument(project, copy.path);
      }
    } catch (failure) {
      this.mutableFailure.set(messageOf(failure));
    }
  }

  private documentOf(tab: DocumentTab | undefined | null): ProjectDocumentInfo | undefined {
    return tab?.project
      ? this.mutableContents()
          .get(tab.project)
          ?.documents.find(held => held.path === tab.path)
      : undefined;
  }

  /** Whether the document in front has work a crash left behind. */
  private async offerRecovery(path: string): Promise<void> {
    const client = await this.library.api();
    const { data } = await client.GET('/api/v1/library/recovery', { params: { query: { path } } });
    if (data?.saved) {
      this.recoverable.set({ path, saved: data.saved });
    }
  }

  /** Puts autosaved work back, or lets it go. The Server wants the document closed for either, so it opens again after. */
  async resolveRecovery(restore: boolean): Promise<void> {
    const offer = this.recoverable();
    this.recoverable.set(null);
    if (!offer) {
      return;
    }

    const project = this.mutableTabs().find(tab => tab.path === offer.path)?.project;
    this.startAction();
    let failed: string | null = null;
    try {
      // Not a discard: that is for edits, and it would take the autosave with them.
      if (!(await this.remove(offer.path, false))) {
        return;
      }

      const client = await this.library.api();
      const body = { path: offer.path, restore };
      unwrap(await client.POST('/api/v1/library/recovery', { body }));
    } catch (failure) {
      failed = messageOf(failure);
    }

    if (project) {
      await this.openDocument(project, offer.path);
    }

    if (failed !== null) {
      this.mutableFailure.set(failed);
    }
  }

  /** Puts the question off: nothing is decided, so the work stays and the next open asks again. */
  dismissRecovery(): void {
    this.recoverable.set(null);
  }

  /** Says what a graph is for, which decides where the shelf puts it. A graph of the wrong shape is refused. */
  async setRole(project: string, document: ProjectDocumentInfo, role: string): Promise<void> {
    if (!document.id) {
      return;
    }

    this.startAction();
    try {
      const client = await this.library.api();
      const body = { project, document: document.id, role };
      const contents = unwrap(await client.PATCH('/api/v1/library/project/document/role', { body }));
      this.mutableContents.update(all => new Map(all).set(project, contents));
      this.library.refreshSoon();
    } catch (failure) {
      this.mutableFailure.set(messageOf(failure));
    }
  }

  /** Asks the user for a picture and brings it into the project, for a layer or a mask to read. */
  async pickPicture(): Promise<string | null> {
    const project = this.current();
    const picture = await desktop()?.openImage(this.status.pictures());
    if (!project || !picture) {
      return null;
    }

    this.startAction();
    try {
      const client = await this.library.api();
      const body = { project: project.path, picture };
      const added = unwrap(await client.POST('/api/v1/library/project/pictures', { body })).path;
      await this.read(project.path);
      return added;
    } catch (failure) {
      this.mutableFailure.set(messageOf(failure));
      return null;
    }
  }

  /**
   * Closes every document below a folder or project, which cannot move while something has it open. False when one has
   * unsaved edits, which only its owner can decide about.
   */
  async release(paths: readonly string[]): Promise<boolean> {
    const inside = this.openInside(paths);
    const dirty = inside.find(tab => tab.dirty);
    if (dirty) {
      this.fail(`${dirty.name} has unsaved changes. Save it first.`);
      return false;
    }

    for (const tab of inside) {
      await this.remove(tab.path, false);
    }

    return true;
  }

  /** The open documents below these folders or projects, which moving them would close. */
  openInside(paths: readonly string[]): readonly DocumentTab[] {
    return this.mutableTabs().filter(tab =>
      paths.some(path => tab.project === path || tab.project?.startsWith(`${path}/`))
    );
  }

  /** What went wrong before has nothing to do with what is starting now. */
  private startAction(): void {
    this.mutableFailure.set(null);
    this.mutableNotice.set(null);
    this.paint.failure.set(null);
  }

  /** Says what went wrong where it can be read: nothing shows through the Project Settings dialog. */
  private fail(reason: string): void {
    (this.settingsOpen() ? this.settingsFailure : this.mutableFailure).set(reason);
  }

  private isShowing(tab: DocumentTab): boolean {
    return isGraph(tab) ? this.graph.document() !== null : this.paint.state() !== null;
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
    if (isGraph(tab)) {
      this.mutableTabs.update(tabs => tabs.filter(held => held.path !== tab?.path));
      this.mutableActive.set(null);
      const next = this.mutableTabs().at(-1);
      if (next) {
        void this.activate(next.path);
      }
    }
  }

  /** Closes a tab. False if its session stayed, which keeps the tab: the edits there would be out of reach without it. */
  private async remove(path: string, discard: boolean): Promise<boolean> {
    const tab = this.mutableTabs().find(held => held.path === path);
    if (!tab) {
      return true;
    }

    let closed = true;
    this.switching = true;
    try {
      if (this.mutableActive() === path) {
        this.detach();
        this.mutableActive.set(null);
        await this.clearViewport();
      }

      await this.closeSession(tab, discard);
    } catch (failure) {
      closed = false;
      this.mutableFailure.set(messageOf(failure));
    } finally {
      this.switching = false;
    }

    if (closed) {
      this.mutableTabs.update(tabs => tabs.filter(held => held.path !== path));
    }

    const next = closed ? this.mutableTabs().at(-1) : tab;
    if (next && this.mutableActive() === null) {
      await this.activate(next.path);
    }

    return closed;
  }

  private async closeSession(tab: DocumentTab, discard: boolean): Promise<void> {
    if (tab.session === null) {
      return;
    }

    const client = await this.library.api();
    const id = tab.session;
    try {
      await (isGraph(tab)
        ? client.DELETE('/api/v1/graphs/{id}', { params: { path: { id }, query: { discard } } })
        : client.DELETE('/api/v1/stacks/{id}', { params: { path: { id }, query: { discard } } }));
    } catch (failure) {
      // A session already gone is what closing wanted.
      if (!(failure instanceof ApiError && failure.status === 404)) {
        throw failure;
      }
    }
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

  private async linkBake(folder: string, material: string | null): Promise<void> {
    if (this.paint.state() && (this.document()?.material ?? null) === material) {
      await this.paint.useMeshMaps(folder);
    }

    const project = this.current()?.path;
    if (project) {
      await this.read(project);
    }
  }

  /** A graph saved since a stack last opened reaches its layers now. */
  private async refreshMaterials(project: string | null): Promise<void> {
    if (project !== null) {
      const client = await this.library.api();
      await client.POST('/api/v1/library/materials/refresh', { params: { query: { project } } });
    }
  }

  /**
   * Keeps how the viewport was left in a project, so it opens that way again. A project that has gone keeps nothing,
   * and saying so would only interrupt.
   */
  async keepViewport(path: string, viewport: ViewportDisplay): Promise<void> {
    try {
      const client = await this.library.api();
      const contents = unwrap(
        await client.PUT('/api/v1/library/project/viewport', { body: { project: path, viewport } })
      );
      this.mutableContents.update(all => new Map(all).set(path, contents));
    } catch {
      // The viewport shows the settings either way; only reopening the project would miss them.
    }
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
