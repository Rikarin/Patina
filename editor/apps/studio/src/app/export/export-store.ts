import { Injectable, computed, inject, signal } from '@angular/core';
import {
  type BuildPreview,
  type BuildRequest,
  type ExportFormatInfo,
  type ExportPresetInfo,
  type JobState,
  type LibraryReport,
  type PatinaClient,
  type ProjectExportSettings,
  type ProjectReport,
  unwrap
} from '@patina/api-client';
import { BakeStore } from '../bake/bake-store';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';

const ReportPage = 50;

/** What an export covers: the document in front, its project, or the whole library. */
export type ExportScope = 'document' | 'project' | 'library';

/** One file of the preset, as the dialog has it: on or off, and in which format. */
export interface FileChoice {
  enabled: boolean;
  format: string;
}

/** What the dialog sets, kept per project so the next export of it starts where the last one ended. */
export interface ExportSettings {
  preset: string | null;
  /** Where files go, or null for the library's exports folder. */
  folder: string | null;
  /** The longest side, or null for the maps' own size. */
  size: number | null;
  filter: string;
  padding: string;
  distance: number;
  colour: readonly number[];
  dither: boolean;
  /** The texture sets left out, by name; every other set is written. */
  skipped: readonly string[];
  /** Per preset, per file template. */
  files: Readonly<Record<string, Readonly<Record<string, FileChoice>>>>;
}

/** What the dialog starts from for a preset nobody has changed. */
export function settingsFor(preset: ExportPresetInfo | null, previous?: ExportSettings): ExportSettings {
  return {
    preset: preset?.id ?? null,
    folder: previous?.folder ?? null,
    size: preset?.size ?? null,
    filter: preset?.filter ?? 'box',
    padding: preset?.padding.mode ?? 'none',
    distance: preset && preset.padding.distance > 0 ? preset.padding.distance : 16,
    colour: preset?.padding.colour.length ? preset.padding.colour : [0, 0, 0, 1],
    dither: preset?.dither ?? false,
    skipped: previous?.skipped ?? [],
    files: previous?.files ?? {}
  };
}

/** The dialog's settings as the project keeps them in its project.yaml. */
export function storedOf(settings: ExportSettings): ProjectExportSettings {
  return {
    preset: settings.preset,
    folder: settings.folder,
    size: settings.size,
    filter: settings.filter,
    padding: { mode: settings.padding, distance: settings.distance, colour: [...settings.colour] },
    dither: settings.dither,
    skipped: [...settings.skipped],
    files: Object.entries(settings.files).flatMap(([preset, files]) =>
      Object.entries(files).map(([template, choice]) => ({
        preset,
        template,
        enabled: choice.enabled,
        format: choice.format
      }))
    )
  };
}

/**
 * The dialog's settings from what a project keeps. A field the project has not kept, or a preset it no longer has,
 * starts from the preset.
 */
export function settingsOf(stored: ProjectExportSettings, preset: ExportPresetInfo): ExportSettings {
  const start = settingsFor(preset);
  const same = stored.preset === preset.id;
  const files: Record<string, Record<string, FileChoice>> = {};
  for (const file of stored.files) {
    files[file.preset] = { ...files[file.preset], [file.template]: { enabled: file.enabled, format: file.format } };
  }

  return {
    ...start,
    folder: stored.folder ?? null,
    skipped: stored.skipped,
    files,
    ...(same
      ? {
          size: stored.size ?? null,
          filter: stored.filter ?? start.filter,
          padding: stored.padding?.mode ?? start.padding,
          distance: stored.padding?.distance ?? start.distance,
          colour: stored.padding?.colour.length ? stored.padding.colour : start.colour,
          dither: stored.dither ?? start.dither
        }
      : {})
  };
}

/**
 * The request for an export. Options go only where the dialog differs from the preset, so an export with nothing
 * changed has the preset's own key and reuses what `patina build` or another client made.
 */
export function requestFor(
  settings: ExportSettings,
  preset: ExportPresetInfo,
  scope: { projects?: readonly string[]; sets?: readonly string[] | null; asset?: string | null }
): BuildRequest {
  const options: NonNullable<BuildRequest['options']> = {};
  if (settings.size !== (preset.size ?? null)) {
    options.size = settings.size;
  }
  if (settings.size !== null && settings.filter !== preset.filter) {
    options.filter = settings.filter;
  }
  const padding = preset.padding;
  const colourChanged =
    settings.padding === 'colour' &&
    settings.colour.join() !== (padding.colour.length ? padding.colour : [0, 0, 0, 1]).join();
  if (
    settings.padding !== padding.mode ||
    (usesDistance(settings.padding) && settings.distance !== padding.distance) ||
    colourChanged
  ) {
    options.padding = {
      mode: settings.padding,
      distance: settings.distance,
      colour: settings.padding === 'colour' ? [...settings.colour] : []
    };
  }
  if (settings.dither !== preset.dither) {
    options.dither = settings.dither;
  }
  const choices = settings.files[preset.id] ?? {};
  const files = preset.files
    .map(file => ({ file, choice: choices[file.template] }))
    .filter(({ file, choice }) => choice && (!choice.enabled || choice.format !== file.format))
    .map(({ file, choice }) => ({
      template: file.template,
      enabled: choice?.enabled ?? true,
      ...(choice && choice.format !== file.format ? { format: choice.format } : {})
    }));
  if (files.length > 0) {
    options.files = files;
  }
  if (scope.sets) {
    options.sets = [...scope.sets];
  }

  return {
    presets: [preset.id],
    ...(scope.projects?.length ? { projects: [...scope.projects] } : {}),
    ...(scope.asset ? { asset: scope.asset } : {}),
    ...(settings.folder ? { folder: settings.folder } : {}),
    ...(Object.keys(options).length > 0 ? { options } : {})
  };
}

/** Whether a padding mode grows by a distance before it fills the rest. */
export function usesDistance(mode: string): boolean {
  return mode === 'transparent' || mode === 'colour' || mode === 'diffusion';
}

/**
 * Presets, what a build would do, and what one did. The Server owns all of it, and a build is a job, so it carries on
 * whether or not anything here is listening. Results are paths: this shows them and does nothing with them.
 */
@Injectable({ providedIn: 'root' })
export class ExportStore {
  private readonly connection = inject(SERVER_CONNECTION);
  private readonly createClient = inject(API_CLIENT_FACTORY);
  private readonly jobs = inject(BakeStore);
  private readonly currentId = signal<string | null>(null);
  private client: PatinaClient | null = null;

  /** What the last export of an open stack wrote. */
  readonly exported = signal<readonly string[]>([]);
  readonly presets = signal<readonly ExportPresetInfo[]>([]);
  readonly formats = signal<readonly ExportFormatInfo[]>([]);
  readonly paddings = signal<readonly string[]>([]);
  readonly filters = signal<readonly string[]>([]);
  readonly problems = signal<readonly string[]>([]);
  readonly preview = signal<BuildPreview | null>(null);
  readonly report = signal<LibraryReport | null>(null);
  readonly projects = signal<readonly ProjectReport[]>([]);
  readonly projectCount = signal(0);

  /** The build the panel is following, kept current by the jobs' events. */
  readonly build = computed<JobState | null>(() => {
    const id = this.currentId();
    return id ? this.jobs.find(id) : null;
  });

  /** Writes each map of an open stack to a folder, from what is on screen, saved or not. */
  async exportStack(id: string, folder: string): Promise<void> {
    const client = await this.api();
    const maps = unwrap(
      await client.POST('/api/v1/stacks/{id}/export', { params: { path: { id } }, body: { folder } })
    );
    this.exported.set(maps.map(map => map.file ?? map.usage));
  }

  /** Reads the presets this library can use, and the formats, paddings and filters an export can choose. */
  async load(): Promise<void> {
    const list = unwrap(await (await this.api()).GET('/api/v1/export/presets'));
    this.presets.set(list.presets);
    this.problems.set(list.problems);
    this.formats.set(list.formats);
    this.paddings.set(list.paddings);
    this.filters.set(list.filters);
  }

  /** Asks what a build would do. Nothing runs. */
  async ask(request: BuildRequest): Promise<void> {
    const client = await this.api();
    this.preview.set(
      unwrap(await client.POST('/api/v1/builds/preview', { body: request, params: { query: { limit: ReportPage } } }))
    );
  }

  /** Starts a build. Throws the Server's refusal, which is what the panel shows. */
  async start(request: BuildRequest): Promise<void> {
    const client = await this.api();
    const job = unwrap(await client.POST('/api/v1/builds', { body: request }));
    this.jobs.track(job);
    this.report.set(null);
    this.projects.set([]);
    this.projectCount.set(0);
    this.currentId.set(job.id);
  }

  /** Stops the build the panel is following. What it finished stays finished. */
  async cancel(): Promise<void> {
    const id = this.currentId();
    if (id) {
      await this.jobs.cancel(id);
    }
  }

  /** Reads what a finished build did. */
  async read(id: string): Promise<void> {
    const client = await this.api();
    const path = { id };
    this.report.set(unwrap(await client.GET('/api/v1/builds/{id}/report', { params: { path } })));
    const page = unwrap(
      await client.GET('/api/v1/builds/{id}/report/projects', {
        params: { path, query: { limit: ReportPage } }
      })
    );
    this.projects.set(page.items);
    this.projectCount.set(page.total);
  }

  /** What a project's export was last set to, from its project.yaml through the Server, or null for none. */
  async settings(project: string): Promise<ProjectExportSettings | null> {
    const client = await this.api();
    const contents = unwrap(await client.GET('/api/v1/library/project', { params: { query: { path: project } } }));
    return contents.export ?? null;
  }

  /** Keeps what a project's export is set to, in its project.yaml, where any client's next export starts. */
  async keep(project: string, settings: ExportSettings): Promise<void> {
    const client = await this.api();
    unwrap(
      await client.PUT('/api/v1/library/project/export', {
        body: { project, export: storedOf(settings) }
      })
    );
  }

  private async api(): Promise<PatinaClient> {
    this.client ??= this.createClient(await this.connection());
    return this.client;
  }
}
