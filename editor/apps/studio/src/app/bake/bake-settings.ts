import { DestroyRef, Injectable, computed, inject, linkedSignal, signal } from '@angular/core';
import type { BakeOptions, BakeRequest, ProjectBakeSettings, ProjectMeshInfo } from '@patina/api-client';
import { SystemStatus } from '../core/system-status';
import { ProjectStore } from '../library/project-store';
import { BakeStore } from './bake-store';

/** How long an edit waits for the next before the project's file is written. */
const SaveDelayMs = 400;

/** One texture set a bake can make maps for: a material of the mesh, which a document paints. */
export interface BakeSet {
  material: string;
  name: string;
}

/**
 * How the open project bakes. The project's file holds it (files are the truth), so this is a copy that writes back a
 * moment after each edit; closing the dialog, restarting or opening the project in another client keeps it. Building
 * the requests is here too, so the dialog's Bake and a Quick Bake without the dialog send the same thing.
 */
@Injectable({ providedIn: 'root' })
export class BakeSettings {
  private readonly project = inject(ProjectStore);
  private readonly status = inject(SystemStatus);
  private readonly bakes = inject(BakeStore);
  private timer: ReturnType<typeof setTimeout> | null = null;
  private pending: { project: string; bake: ProjectBakeSettings } | null = null;

  /** Why the last write of the settings failed, or null. */
  readonly failure = signal<string | null>(null);

  /** An output folder chosen for this session, which outranks the project's own until the project changes. */
  readonly output = linkedSignal<string | null, string>({
    source: () => this.project.current()?.path ?? null,
    computation: () => ''
  });

  /** The settings as edited, which the project's file catches up with. */
  readonly settings = linkedSignal<
    { path: string | null; saved: ProjectBakeSettings | null },
    ProjectBakeSettings | null
  >({
    source: () => ({ path: this.project.current()?.path ?? null, saved: this.project.current()?.bake ?? null }),
    // An edit not written yet outranks what the file said before it.
    computation: (source, previous) =>
      this.pending?.project === source.path && previous ? previous.value : source.saved
  });

  readonly options = computed(() => this.settings()?.options ?? null);

  private readonly library = computed(() => {
    const state = this.status.state();
    return state.kind === 'online' ? state.system.library : null;
  });

  /**
   * The mesh a bake is onto: the one under the document in front, or the project's when that document has none. A
   * layered material paints no mesh, and its Bake button used to do nothing at all.
   */
  readonly mesh = computed<ProjectMeshInfo | null>(() => {
    const contents = this.project.current();
    if (!contents) {
      return null;
    }

    const used = new Set(contents.documents.map(document => document.mesh).filter(Boolean));
    return (
      this.project.mesh() ??
      contents.meshes.find(mesh => mesh.id && used.has(mesh.id)) ??
      contents.meshes.find(mesh => mesh.id) ??
      null
    );
  });

  /** The project's texture sets, one per material. A model with one material has none to choose. */
  readonly sets = computed<BakeSet[]>(() =>
    this.project.textureSets().map(document => ({ material: document.material ?? '', name: document.name }))
  );

  /** The sets the next bake makes maps for: the ticked ones, or all of them when none is ticked. */
  readonly chosen = computed(() => {
    const ticked = new Set(this.settings()?.sets ?? []);
    const sets = this.sets();
    return ticked.size === 0 ? sets : sets.filter(set => ticked.has(set.material));
  });

  /** The low mesh's path on the Server's machine, or empty. */
  readonly low = computed(() => {
    const mesh = this.mesh();
    const library = this.library();
    return mesh && library ? `${library}/${mesh.path}` : '';
  });

  /** The high meshes in order: the mesh's own detailed one, or the low mesh itself, then any added. */
  readonly high = computed(() => {
    const mesh = this.mesh();
    const library = this.library();
    const first = mesh?.high && library ? `${library}/${mesh.high}` : this.low();
    return [first, ...(this.settings()?.high ?? [])].filter(path => path !== '');
  });

  /** Why a bake cannot start now, said plainly, or null when it can. */
  readonly blocked = computed<string | null>(() => {
    const options = this.options();
    if (!this.library()) {
      return 'The engine is not running.';
    }

    if (!this.project.current()) {
      return 'Open a project to bake its mesh.';
    }

    if (!this.mesh() || !options) {
      return 'This project has no mesh yet. Choose one in Project Settings.';
    }

    if (this.project.unwrapping()) {
      return 'The mesh is being unwrapped.';
    }

    if (this.bakes.busy()) {
      return 'A bake is running.';
    }

    if (options.maps.length === 0) {
      return 'Turn on at least one map.';
    }

    if (this.sets().length > 0 && this.chosen().length === 0) {
      return 'Tick at least one texture set.';
    }

    return null;
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      if (this.timer !== null) {
        clearTimeout(this.timer);
      }
    });
  }

  /** Changes the settings and writes them to the project a moment later. */
  update(change: (settings: ProjectBakeSettings) => ProjectBakeSettings): void {
    const project = this.project.current()?.path;
    const settings = this.settings();
    if (!project || !settings) {
      return;
    }

    const next = change(settings);
    this.pending = { project, bake: next };
    this.settings.set(next);
    if (this.timer !== null) {
      clearTimeout(this.timer);
    }

    this.timer = setTimeout(() => void this.flush(), SaveDelayMs);
  }

  /** Changes the options only. */
  set(change: (options: BakeOptions) => BakeOptions): void {
    this.update(settings => ({ ...settings, options: change(settings.options) }));
  }

  /** Writes an edit that is waiting, now: a bake must not start from settings the file has not caught up with. */
  async flush(): Promise<void> {
    if (this.timer !== null) {
      clearTimeout(this.timer);
      this.timer = null;
    }

    const pending = this.pending;
    if (!pending) {
      return;
    }

    try {
      await this.project.setBake(pending.project, pending.bake);
      this.failure.set(null);
    } catch (error) {
      this.failure.set(error instanceof Error ? error.message : String(error));
    } finally {
      if (this.pending === pending) {
        this.pending = null;
      }
    }
  }

  /** One request per texture set to bake, or one for the whole mesh when it has none. */
  requests(): BakeRequest[] {
    const settings = this.settings();
    const contents = this.project.current();
    const library = this.library();
    if (!settings || !contents || !library || this.low() === '') {
      return [];
    }

    const chosen = this.sets().length > 0 ? this.chosen().map(set => set.material) : [null];
    const chosenOutput = this.output().trim();
    return chosen.map(material => {
      const folder = chosenOutput || `${library}/${contents.bakeFolder}/maps`;
      return {
        high: this.high(),
        low: this.low(),
        output: material && this.sets().length > 0 ? `${folder}/${material}` : folder,
        cage: settings.cage.trim() || null,
        material,
        name: null,
        options: settings.options
      };
    });
  }

  /** Why the Server refused the last bake, or null. */
  readonly refused = signal<string | null>(null);

  /** Bakes every chosen set with the settings as they are. Says whether it started; a refusal is in `refused`. */
  async bake(): Promise<boolean> {
    this.refused.set(null);
    try {
      await this.flush();
      await this.bakes.startAll(this.requests());
      return true;
    } catch (error) {
      this.refused.set(error instanceof Error ? error.message : String(error));
      return false;
    }
  }
}
