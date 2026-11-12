import { Injectable, computed, inject, signal } from '@angular/core';
import {
  type BuildPreview,
  type BuildRequest,
  type CompressionPreviewInfo,
  type CompressionPreviewRequest,
  type ExportFileChoice,
  type ExportFormatInfo,
  type ExportPresetCheck,
  type ExportPresetDocument,
  type ExportPresetInfo,
  type ExportPresetSource,
  type JobState,
  type LibraryReport,
  type PatinaClient,
  type ProjectExportSettings,
  type ProjectReport,
  type StackTerm,
  unwrap
} from '@patina/api-client';
import { BakeStore } from '../bake/bake-store';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';

const ReportPage = 50;

/** What an export covers: the document in front, its project, or the whole library. */
export type ExportScope = 'document' | 'project' | 'library';

/**
 * One file of the preset, as the dialog has it: on or off, in which format, and for a KTX2 or DDS file what it holds,
 * how hard its blocks are searched and whether it has mips. Absent fields keep the preset's.
 */
export interface FileChoice {
  enabled: boolean;
  format: string;
  compression?: string | null;
  quality?: string | null;
  mips?: boolean | null;
}

/** One texture set's own changes over the whole export's: so far, its size. */
export interface SetChoice {
  /** The longest side of the set's files, or null for the export's. */
  size: number | null;
}

/**
 * Whose export settings the dialog holds: a project's, by its path, kept in its project.yaml, or the whole library's,
 * which belong to no project and are kept in library.yaml.
 */
export type ExportOwner = { readonly project: string } | 'library';

/** Whether two owners are the same project, or both the library. */
export function sameOwner(a: ExportOwner | null, b: ExportOwner | null): boolean {
  return a === b || (typeof a === 'object' && typeof b === 'object' && a?.project === b?.project);
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
  /** Per texture set, what it changes over the rest. */
  perSet: Readonly<Record<string, SetChoice>>;
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
    files: previous?.files ?? {},
    perSet: {}
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
        format: choice.format,
        compression: choice.compression ?? null,
        quality: choice.quality ?? null,
        mips: choice.mips ?? null
      }))
    ),
    perSet: Object.entries(settings.perSet)
      .filter(([, choice]) => choice.size !== null)
      .map(([set, choice]) => ({ set, size: choice.size }))
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
    files[file.preset] = {
      ...files[file.preset],
      [file.template]: {
        enabled: file.enabled,
        format: file.format,
        ...(file.compression ? { compression: file.compression } : {}),
        ...(file.quality ? { quality: file.quality } : {}),
        ...(file.mips !== null && file.mips !== undefined ? { mips: file.mips } : {})
      }
    };
  }
  const perSet: Record<string, SetChoice> = {};
  for (const set of stored.perSet) {
    perSet[set.set] = { size: set.size ?? null };
  }

  return {
    ...start,
    folder: stored.folder ?? null,
    skipped: stored.skipped,
    files,
    perSet: same ? perSet : {},
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
    .filter(({ file, choice }) => choice && changesFile(file, choice))
    .map(({ file, choice }) => fileOptions(file, choice as FileChoice));
  if (files.length > 0) {
    options.files = files;
  }
  if (scope.sets) {
    options.sets = [...scope.sets];
  }
  const perSet = Object.entries(settings.perSet)
    .filter(([set, choice]) => choice.size !== null && (!scope.sets || scope.sets.includes(set)))
    .map(([set, choice]) => ({ set, size: choice.size }));
  if (perSet.length > 0) {
    options.perSet = perSet;
  }

  return {
    presets: [preset.id],
    ...(scope.projects?.length ? { projects: [...scope.projects] } : {}),
    ...(scope.asset ? { asset: scope.asset } : {}),
    ...(settings.folder ? { folder: settings.folder } : {}),
    ...(Object.keys(options).length > 0 ? { options } : {})
  };
}

interface PresetFileShape {
  template: string;
  format: string;
  compression: string;
  quality: string;
  mips: boolean;
}

/** Whether a file's choice differs from what the preset says of it. */
function changesFile(file: PresetFileShape, choice: FileChoice): boolean {
  return (
    !choice.enabled ||
    choice.format !== file.format ||
    (!!choice.compression && choice.compression !== file.compression) ||
    (!!choice.quality && choice.quality !== file.quality) ||
    (choice.mips !== undefined && choice.mips !== null && choice.mips !== file.mips)
  );
}

/** A file's choice as an export's options: only what differs from the preset. */
export function fileOptions(file: PresetFileShape, choice: FileChoice): ExportFileChoice {
  return {
    template: file.template,
    enabled: choice.enabled,
    ...(choice.format !== file.format ? { format: choice.format } : {}),
    ...(choice.compression && choice.compression !== file.compression ? { compression: choice.compression } : {}),
    ...(choice.quality && choice.quality !== file.quality ? { quality: choice.quality } : {}),
    ...(choice.mips !== undefined && choice.mips !== null && choice.mips !== file.mips ? { mips: choice.mips } : {})
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
  /** The preset an export starts from when nothing is kept for it: the library's choice, or the glTF starter (#240). */
  readonly defaultPreset = signal<string | null>(null);
  readonly formats = signal<readonly ExportFormatInfo[]>([]);
  /** The padding modes and resize filters, each with the label the Server gives it. */
  readonly paddingTerms = signal<readonly StackTerm[]>([]);
  readonly filterTerms = signal<readonly StackTerm[]>([]);
  /** What a KTX2 or DDS file can hold, the maps a lane can read, the material files, and the names a template takes. */
  readonly compressionTerms = signal<readonly StackTerm[]>([]);
  readonly mapTerms = signal<readonly StackTerm[]>([]);
  readonly materialTerms = signal<readonly StackTerm[]>([]);
  readonly tokens = signal<readonly string[]>([]);
  readonly paddings = computed(() => this.paddingTerms().map(term => term.value));
  readonly filters = computed(() => this.filterTerms().map(term => term.value));
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

  /**
   * Reads the presets this library can use, with a project's own when one is named, and the formats, paddings and
   * filters an export can choose.
   */
  async load(project: string | null = null): Promise<void> {
    const query = project ? { project } : {};
    const list = unwrap(await (await this.api()).GET('/api/v1/export/presets', { params: { query } }));
    this.presets.set(list.presets);
    this.defaultPreset.set(list.default ?? null);
    this.problems.set(list.problems);
    this.formats.set(list.formats);
    this.paddingTerms.set(list.paddings);
    this.filterTerms.set(list.filters);
    this.compressionTerms.set(list.compressions);
    this.mapTerms.set(list.maps);
    this.materialTerms.set(list.materials);
    this.tokens.set(list.tokens);
  }

  /** Makes a preset the one every export starts from when it has kept none, in the library's own file. */
  async keepDefault(id: string): Promise<void> {
    const client = await this.api();
    unwrap(await client.PUT('/api/v1/library/export/preset', { body: { preset: id } }));
    this.defaultPreset.set(id);
  }

  /** The preset an export with nothing kept starts from: the default while it is listed, else the first. */
  starting(): ExportPresetInfo | null {
    const presets = this.presets();
    return presets.find(preset => preset.id === this.defaultPreset()) ?? presets[0] ?? null;
  }

  /** A preset as its file says it, where it comes from, and its text. */
  async preset(id: string, project: string | null): Promise<ExportPresetSource> {
    const query = project ? { project } : {};
    return unwrap(await (await this.api()).GET('/api/v1/export/presets/{id}', { params: { path: { id }, query } }));
  }

  /** What a preset would be, and the files it would write for a project, keeping nothing. */
  async check(preset: ExportPresetDocument, project: string | null): Promise<ExportPresetCheck> {
    return unwrap(await (await this.api()).POST('/api/v1/export/presets/check', { body: { preset, project } }));
  }

  /** Keeps a preset as a file in the library or a project, renaming the one it replaces. */
  async save(
    preset: ExportPresetDocument,
    project: string | null,
    replaces: string | null = null
  ): Promise<ExportPresetSource> {
    return unwrap(
      await (
        await this.api()
      ).PUT('/api/v1/export/presets/{id}', {
        params: { path: { id: preset.id } },
        body: { preset, project, replaces }
      })
    );
  }

  /** Removes a library's or a project's preset file. */
  async remove(id: string, project: string | null): Promise<void> {
    const query = project ? { project } : {};
    unwrap(await (await this.api()).DELETE('/api/v1/export/presets/{id}', { params: { path: { id }, query } }));
  }

  /** Brings a preset file someone shared into the library or a project. */
  async bringIn(file: string, project: string | null): Promise<ExportPresetSource> {
    return unwrap(await (await this.api()).POST('/api/v1/export/presets/import', { body: { file, project } }));
  }

  /** A region of one file as the file holds it, beside its source, with the error per lane. */
  async compression(request: CompressionPreviewRequest): Promise<CompressionPreviewInfo> {
    return unwrap(await (await this.api()).POST('/api/v1/export/compression', { body: request }));
  }

  /** One picture of a compression preview, as an object URL the caller revokes. */
  async picture(id: string, picture: 'source' | 'written' | 'difference'): Promise<string> {
    const blob = unwrap(
      await (
        await this.api()
      ).GET('/api/v1/export/compression/{id}/{picture}', {
        params: { path: { id, picture } },
        parseAs: 'blob'
      })
    );
    return URL.createObjectURL(blob);
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

  /**
   * What an export was last set to, through the Server, or null for none: a project's from its project.yaml, the
   * library's from library.yaml. A project's texture sets come with it, so a project that is not open has them too.
   */
  async settings(owner: ExportOwner): Promise<{ kept: ProjectExportSettings | null; sets: readonly string[] }> {
    const client = await this.api();
    if (owner === 'library') {
      return { kept: unwrap(await client.GET('/api/v1/library')).export ?? null, sets: [] };
    }
    const query = { path: owner.project };
    const contents = unwrap(await client.GET('/api/v1/library/project', { params: { query } }));
    return { kept: contents.export ?? null, sets: contents.exportSets };
  }

  /** Keeps what an export is set to, in its owner's file, where any client's next export of it starts. */
  async keep(owner: ExportOwner, settings: ExportSettings): Promise<void> {
    const client = await this.api();
    const stored = storedOf(settings);
    if (owner === 'library') {
      unwrap(await client.PUT('/api/v1/library/export', { body: { export: stored } }));
    } else {
      unwrap(await client.PUT('/api/v1/library/project/export', { body: { project: owner.project, export: stored } }));
    }
  }

  private async api(): Promise<PatinaClient> {
    this.client ??= this.createClient(await this.connection());
    return this.client;
  }
}
