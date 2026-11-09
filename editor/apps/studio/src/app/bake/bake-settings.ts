import { DestroyRef, Injectable, computed, effect, inject, linkedSignal, signal, untracked } from '@angular/core';
import type {
  BakeOptions,
  BakePreviewState,
  BakeRequest,
  ProjectBakeSettings,
  ProjectMeshInfo,
  ViewportBakePreview
} from '@patina/api-client';
import { messageOf, refusalWords } from '../core/messages';
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

  private readonly outputChoice = signal<{ project: string; path: string } | null>(null);

  /** An output folder chosen for this session, which outranks the project's own while that project is open. */
  readonly output = computed(() => {
    const choice = this.outputChoice();
    return choice && choice.project === this.project.current()?.path ? choice.path : '';
  });

  /** Chooses where this session's bakes of the open project go, or the project's own folder for empty. */
  chooseOutput(path: string): void {
    const project = this.project.current()?.path;
    this.outputChoice.set(project && path ? { project, path } : null);
  }

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

  /**
   * Why the occlusion settings would bake white, in the words the Server's refusal has, or null: the rays of occlusion
   * and the bent normal start at the minimum occluder distance, so it has to be short of the occluder distance (#228).
   */
  readonly occlusionProblem = computed<string | null>(() => {
    const options = this.options();
    const occluded = options?.maps.some(map => map === 'ao' || map === 'bent') ?? false;
    return options && occluded && options.occlusion.minimum >= options.occlusion.distance
      ? refusalWords('bake-occluder-minimum')
      : null;
  });

  /** Why a bake cannot start now, said plainly, or null when it can. */
  readonly blocked = computed<string | null>(() => {
    const options = this.options();
    if (!this.library()) {
      return 'Open a library to bake.';
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

    const occlusion = this.occlusionProblem();
    if (occlusion) {
      return occlusion;
    }

    if (this.sets().length > 0 && this.chosen().length === 0) {
      return 'Tick at least one texture set.';
    }

    return null;
  });

  /** What the viewport shows of the bake's setup, as the Server last said, or null when it shows none (#149). */
  readonly preview = signal<BakePreviewState | null>(null);

  /** Whether the viewport shows the bake's setup. The Server holds it, so another client sees the same. */
  readonly previewing = computed(() => (this.preview()?.preview ?? null) !== null);

  /** What of the setup to draw: the reach, the high mesh and the matched parts. */
  readonly previewShows = signal({ reach: true, highMesh: true, matches: true });

  /** Why the setup could not be shown, or null. */
  readonly previewFailure = signal<string | null>(null);

  private previewTimer: ReturnType<typeof setTimeout> | null = null;
  private lastSetup: ViewportBakePreview | null = null;

  /** The setup the viewport would show, built as a bake request is, for the whole low mesh. */
  readonly setup = computed<ViewportBakePreview | null>(() => {
    const settings = this.settings();
    const low = this.low();
    if (!settings || low === '') {
      return null;
    }

    const shows = this.previewShows();
    return {
      high: this.high(),
      low,
      cage: settings.cage.trim() || null,
      material: null,
      options: settings.options,
      reach: shows.reach,
      highMesh: shows.highMesh,
      matches: shows.matches
    };
  });

  /** Reads what the viewport shows now, as another client may have left it. */
  async readPreview(): Promise<void> {
    try {
      this.preview.set(await this.bakes.setup());
    } catch {
      this.preview.set(null); // no Server: nothing is shown
    }
  }

  /** Shows the setup in the viewport, or takes it off. */
  async setPreviewing(on: boolean): Promise<void> {
    // Off is the same body with no low mesh, which the last setup gives when the project has closed.
    const setup = this.setup() ?? this.lastSetup;
    if (!setup) {
      return;
    }

    this.lastSetup = setup;
    try {
      this.previewFailure.set(null);
      this.preview.set(await this.bakes.showSetup(on ? setup : { ...setup, low: '' }));
    } catch (error) {
      this.previewFailure.set(messageOf(error));
      return;
    }

    // A change while the Server was reading the meshes found the preview not on yet, so nothing followed it: the
    // high mesh chosen a moment before Show was drawn as the low mesh it replaced.
    const now = this.setup();
    if (on && now !== null && now !== setup) {
      await this.setPreviewing(true);
    }
  }

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      if (this.timer !== null) {
        clearTimeout(this.timer);
      }

      if (this.previewTimer !== null) {
        clearTimeout(this.previewTimer);
      }
    });

    // While the setup is shown it follows the settings, a moment after each change, as the project's file does.
    effect(() => {
      const setup = this.setup();
      if (!untracked(() => this.previewing())) {
        return;
      }

      if (this.previewTimer !== null) {
        clearTimeout(this.previewTimer);
      }

      // A project that closes takes its setup off the viewport with it.
      this.previewTimer = setTimeout(() => void this.setPreviewing(setup !== null), SaveDelayMs);
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
    // Settings the Server would refuse wait, shown by their field, until they are fixed.
    if (!pending || this.occlusionProblem() !== null) {
      return;
    }

    try {
      await this.project.setBake(pending.project, pending.bake);
      this.failure.set(null);
    } catch (error) {
      this.failure.set(messageOf(error));
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
      this.refused.set(messageOf(error));
      return false;
    }
  }
}
