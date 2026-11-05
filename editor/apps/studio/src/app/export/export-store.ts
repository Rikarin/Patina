import { Injectable, computed, inject, signal } from '@angular/core';
import {
  type BuildPreview,
  type BuildRequest,
  type ExportPresetInfo,
  type JobState,
  type LibraryReport,
  type PatinaClient,
  type ProjectReport,
  unwrap
} from '@patina/api-client';
import { BakeStore } from '../bake/bake-store';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';

const ReportPage = 50;

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

  readonly presets = signal<readonly ExportPresetInfo[]>([]);
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

  /** Reads the presets this library can use. */
  async load(): Promise<void> {
    const list = unwrap(await (await this.api()).GET('/api/v1/export/presets'));
    this.presets.set(list.presets);
    this.problems.set(list.problems);
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

  private async api(): Promise<PatinaClient> {
    this.client ??= this.createClient(await this.connection());
    return this.client;
  }
}
