import { DestroyRef, Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import {
  type BrushPreset,
  type BrushPresetInfo,
  type PatinaClient,
  type ProjectPaintSettings,
  unwrap
} from '@patina/api-client';
import { messageOf } from '../core/messages';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';
import { PaintStore, type PaintTool } from './paint-store';

/** How long the brush rests before the project keeps it, so a slider's drag is one write. */
const KeepQuiet = 600;

/**
 * The brush as a project keeps it, and the presets it can take up (#123, #124). The Server owns both: a project's
 * colours, palette, brush and symmetry are in its `project.yaml`, and a preset is a `.ptbrush` document of a project.
 * Nothing here is kept in the renderer, so a restart, or another client, starts where this one left off.
 */
@Injectable({ providedIn: 'root' })
export class BrushStore {
  private readonly paint = inject(PaintStore);
  private readonly connection = inject(SERVER_CONNECTION);
  private readonly createClient = inject(API_CLIENT_FACTORY);
  private client: PatinaClient | null = null;
  private keeping: ReturnType<typeof setTimeout> | undefined;
  /** What the project last held, so taking it up is not itself written back. */
  private kept = '';

  /** The project whose painting this is, below `projects/`, or null with none open. */
  readonly project = signal<string | null>(null);
  readonly presets = signal<readonly BrushPresetInfo[]>([]);
  /** The preset last taken up, by its source, so Update knows what to rewrite. */
  readonly chosen = signal<string | null>(null);
  readonly failure = signal<string | null>(null);

  readonly current = computed(() => this.presets().find(preset => preset.source === this.chosen()) ?? null);

  /** What the project keeps, as it stands in the app now. */
  private readonly settings = computed<ProjectPaintSettings>(() => ({
    recentColours: this.paint.recentColours(),
    palette: this.paint.palette(),
    brush: this.paint.wholeBrush(),
    symmetry: this.paint.symmetry()
  }));

  constructor() {
    effect(() => {
      const settings = this.settings();
      const project = this.project();
      if (project !== null) {
        untracked(() => this.keepSoon(project, settings));
      }
    });
    inject(DestroyRef).onDestroy(() => clearTimeout(this.keeping));
  }

  /** Takes up how a project left its painting, and lists the presets it can use. */
  async adopt(project: string, paint: ProjectPaintSettings | null | undefined): Promise<void> {
    if (this.project() !== project) {
      clearTimeout(this.keeping);
      this.paint.recentColours.set([...(paint?.recentColours ?? [])]);
      this.paint.palette.set([...(paint?.palette ?? [])]);
      if (paint?.brush) {
        this.paint.useBrush(paint.brush);
      }

      this.paint.symmetry.set(paint?.symmetry ?? null);
      this.kept = JSON.stringify(this.settings());
      this.project.set(project);
      this.chosen.set(null);
    }

    await this.refresh();
  }

  /** Reads the shelf again. */
  async refresh(): Promise<void> {
    const project = this.project();
    try {
      const client = await this.api();
      const query = project === null ? {} : { project };
      this.presets.set(unwrap(await client.GET('/api/v1/library/brushes', { params: { query } })).items);
    } catch (failure) {
      this.failure.set(messageOf(failure));
    }
  }

  /** Takes a preset up: its tool, its brush and size, and its colour if it has one. */
  use(info: BrushPresetInfo): void {
    const preset = info.preset;
    this.paint.tool.set(preset.tool as PaintTool);
    this.paint.useBrush(preset.brush);
    if (preset.colour) {
      this.paint.colour.set(preset.colour);
    }

    this.chosen.set(info.source);
  }

  /**
   * Takes up the preset in a file, which is what opening one from the library does. With no painting taken up yet, the
   * project's own is taken up first from `paint`, its `project.yaml` block, so the preset changes only the brush and
   * the project's colours, palette and symmetry are never written back from a state that was not loaded (#222).
   */
  async useFile(project: string, path: string, paint: ProjectPaintSettings | null | undefined): Promise<void> {
    if (this.project() === null) {
      await this.adopt(project, paint);
    } else {
      await this.refresh();
    }

    const found = this.presets().find(preset => preset.source === path);
    if (found) {
      this.use(found);
    }
  }

  /**
   * Takes up a preset by the source the shelf names it with (#124), reading the list again first if it is not on it
   * yet, as one just saved or found by a search may not be.
   */
  async useSource(source: string): Promise<void> {
    if (!this.presets().some(preset => preset.source === source)) {
      await this.refresh();
    }

    const found = this.presets().find(preset => preset.source === source);
    if (found) {
      this.use(found);
    }
  }

  /** Saves the brush as it stands as a new preset of the project, with the colour if asked. */
  async saveAs(name: string, withColour: boolean): Promise<void> {
    const project = this.project();
    if (project === null || name.trim() === '') {
      return;
    }

    await this.act(async client => {
      const made = unwrap(
        await client.POST('/api/v1/library/brushes', {
          body: { project, name: name.trim(), preset: this.standing(withColour, false) }
        })
      );
      await this.refresh();
      this.chosen.set(made.source);
    });
  }

  /** Rewrites the chosen preset from the brush as it stands, keeping whether it had a colour and was a favourite. */
  async updateChosen(): Promise<void> {
    const current = this.current();
    if (current) {
      await this.rewrite(current, this.standing(current.preset.colour != null, current.preset.favourite));
    }
  }

  /** Makes a preset a favourite, or not. A shipped one keeps a copy in the project, which is the favourite. */
  async toggleFavourite(info: BrushPresetInfo): Promise<void> {
    await this.rewrite(info, { ...info.preset, favourite: !info.preset.favourite });
  }

  private async rewrite(info: BrushPresetInfo, preset: BrushPreset): Promise<void> {
    const project = this.project();
    if (project === null) {
      return;
    }

    await this.act(async client => {
      const changed = unwrap(
        await client.PUT('/api/v1/library/brushes', { body: { project, source: info.source, preset } })
      );
      await this.refresh();
      if (this.chosen() === info.source) {
        this.chosen.set(changed.source);
      }
    });
  }

  private standing(withColour: boolean, favourite: boolean): BrushPreset {
    const tool = this.paint.tool();
    return {
      tool: tool === 'picker' ? 'brush' : tool,
      brush: this.paint.wholeBrush(),
      colour: withColour ? this.paint.colour() : null,
      favourite
    };
  }

  private async act(work: (client: PatinaClient) => Promise<void>): Promise<void> {
    this.failure.set(null);
    try {
      await work(await this.api());
    } catch (failure) {
      this.failure.set(messageOf(failure));
    }
  }

  /** Writes the project's painting once the brush has rested, and only when it changed. */
  private keepSoon(project: string, settings: ProjectPaintSettings): void {
    const text = JSON.stringify(settings);
    if (text === this.kept) {
      return;
    }

    clearTimeout(this.keeping);
    this.keeping = setTimeout(() => {
      this.kept = text;
      void this.api()
        .then(client => client.PUT('/api/v1/library/project/paint', { body: { project, paint: settings } }))
        .then(reply => unwrap(reply))
        // Painting goes on either way; only the next start would miss what was left.
        .catch(() => undefined);
    }, KeepQuiet);
  }

  private async api(): Promise<PatinaClient> {
    this.client ??= this.createClient(await this.connection());
    return this.client;
  }
}
