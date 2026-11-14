import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import {
  type BakePreviewState,
  type BakeRequest,
  type Connection,
  type JobEvent,
  type JobState,
  type PatinaClient,
  type ViewportBakePreview,
  unwrap
} from '@patina/api-client';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';
import { GRAPH_SOCKET, type GraphSocket } from '../graph/graph-store';

const ReconnectMs = 1_000;

/** Where a client follows jobs from. */
export function jobSocketUrl(connection: Connection, since: number): string {
  const url = new URL('/ws/jobs', connection.baseUrl);
  url.protocol = url.protocol === 'https:' ? 'wss:' : 'ws:';
  url.searchParams.set('since', String(since));
  if (connection.token) {
    url.searchParams.set('access_token', connection.token);
  }

  return url.href;
}

/**
 * What a bake of a project is onto: the project by its path below `projects/`, the document whose mesh it bakes by
 * its path below the library, and a folder on the Server's machine instead of the project's own.
 */
export interface BakePlanQuery {
  project: string;
  document?: string;
  output?: string;
}

/** A map of a finished bake on screen. */
export interface ShownMap {
  job: string;
  map: string;
  url: string;
  width: number;
  height: number;
}

/**
 * The Server's jobs, as a client of it. The Server owns them, so this is a copy kept current by numbered events, and a
 * bake carries on whether or not anything here is listening. Results are paths. The one picture that comes over HTTP is
 * the 8-bit preview of a map, for the Texture pane.
 */
@Injectable({ providedIn: 'root' })
export class BakeStore {
  private readonly connection = inject(SERVER_CONNECTION);
  private readonly createClient = inject(API_CLIENT_FACTORY);
  private readonly sockets = inject(GRAPH_SOCKET);
  private readonly byId = signal<ReadonlyMap<string, JobState>>(new Map());
  private client: PatinaClient | null = null;
  private socket: GraphSocket | null = null;
  private timer: ReturnType<typeof setTimeout> | null = null;
  private sequence = 0;
  private stopped = false;

  /** Every job the Server remembers, bakes and exports, newest first. */
  readonly jobs = computed(() => [...this.byId().values()].sort((a, b) => b.created.localeCompare(a.created)));

  /** The jobs the last Bake started, one per texture set, in the order they were asked for. */
  private readonly batchIds = signal<readonly string[]>([]);

  /** The jobs of the last Bake, as they now stand. */
  readonly batch = computed(() =>
    this.batchIds()
      .map(id => this.byId().get(id))
      .filter((job): job is JobState => job !== undefined)
  );

  /** The job the panel is looking at: the first of the last Bake still going, or its last when all are over. */
  readonly current = computed(() => {
    const jobs = this.batch();
    return jobs.find(job => job.state === 'queued' || job.state === 'running') ?? jobs.at(-1) ?? null;
  });

  /** Whether a job of the last Bake is still waiting or running. */
  readonly busy = computed(() => this.batch().some(job => job.state === 'queued' || job.state === 'running'));

  /** The map the Texture pane shows, which a graph selection takes the place of. */
  readonly shown = signal<ShownMap | null>(null);

  readonly reachable = signal(true);

  /** Counts the times this client changed what the viewport shows, for whichever viewport pane is open. */
  readonly viewportChanged = signal(0);

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.stopped = true;
      this.socket?.close();
      if (this.timer !== null) {
        clearTimeout(this.timer);
      }

      this.revoke();
    });
    void this.follow();
  }

  /** A job as it now stands. Reading this follows it. */
  find(id: string): JobState | null {
    return this.byId().get(id) ?? null;
  }

  /** Holds a job a caller just heard of, which the events carry on from. */
  track(job: JobState): void {
    this.started(job);
  }

  /** Asks for a bake. Throws the Server's refusal, which is what the panel shows. */
  async start(request: BakeRequest): Promise<JobState> {
    const [job] = await this.startAll([request]);
    if (!job) {
      throw new Error('The bake did not start. Try again.');
    }

    return job;
  }

  /**
   * The bakes a project's own settings ask for, as the Server works them out from its file: the same requests
   * `patina bake-maps --project` sends, so no client builds them itself (plan §8). Throws the Server's refusal.
   */
  async plan(query: BakePlanQuery): Promise<BakeRequest[]> {
    const client = await this.api();
    return unwrap(await client.GET('/api/v1/library/project/bake/plan', { params: { query } })).requests;
  }

  /**
   * Asks for several bakes, one per texture set, which the Server runs one after another. A refusal stops the rest,
   * and the ones asked for before it stay queued: they are bakes somebody asked for.
   */
  async startAll(requests: readonly BakeRequest[]): Promise<JobState[]> {
    const client = await this.api();
    const jobs: JobState[] = [];
    this.batchIds.set([]);
    for (const request of requests) {
      const job = unwrap(await client.POST('/api/v1/jobs/bakes', { body: request }));
      this.started(job);
      jobs.push(job);
      this.batchIds.update(ids => [...ids, job.id]);
    }

    return jobs;
  }

  async cancel(id: string): Promise<void> {
    const client = await this.api();
    this.put(unwrap(await client.POST('/api/v1/jobs/{id}/cancel', { params: { path: { id } } })));
  }

  /** Stops every job of the last Bake that has not ended. */
  async cancelAll(): Promise<void> {
    for (const job of this.batch().filter(job => job.state === 'queued' || job.state === 'running')) {
      await this.cancel(job.id);
    }
  }

  /**
   * Brings a PNG baked in another program in as one map of a texturing document's bake (#144). The Server copies it
   * where the document's own bake goes, and the document reads it from then on, open or not.
   */
  async importMap(project: string, document: string, map: string, file: string, space: string): Promise<void> {
    const client = await this.api();
    unwrap(
      await client.POST('/api/v1/library/project/bake/import', {
        body: { project, document, maps: [{ map, file }], space }
      })
    );
  }

  /** Makes a graph's Mesh Map nodes read a finished bake. The Server remembers it beside the graph's file. */
  async useInGraph(graph: string, job: JobState): Promise<void> {
    const client = await this.api();
    unwrap(
      await client.PUT('/api/v1/graphs/{id}/mesh-maps', {
        params: { path: { id: graph } },
        body: { folder: job.bake?.output ?? null }
      })
    );
  }

  /**
   * Shows a finished bake on the mesh it was baked onto, in every client's viewport: the Server holds the choice. With
   * a map, that map alone on the mesh as it is stored, through the picture view (#149).
   */
  async showOnMesh(job: JobState, map: string | null = null): Promise<void> {
    const client = await this.api();
    unwrap(await client.PUT('/api/v1/viewport/bake', { body: { job: job.id, map } }));
    this.viewportChanged.update(count => count + 1);
  }

  /**
   * Lays a bake's setup over the model in every client's viewport, or takes it off for an empty low mesh: its reach,
   * the high mesh and the parts matched by name. Answers with the key to the colours (#149).
   */
  async showSetup(preview: ViewportBakePreview): Promise<BakePreviewState> {
    const client = await this.api();
    const state = unwrap(await client.PUT('/api/v1/viewport/bake-preview', { body: preview }));
    this.viewportChanged.update(count => count + 1);
    return state;
  }

  /** The bake setup the viewport shows now, which another client may have chosen. */
  async setup(): Promise<BakePreviewState> {
    const client = await this.api();
    return unwrap(await client.GET('/api/v1/viewport/bake-preview'));
  }

  /** Shows one map of a finished job in the Texture pane. */
  async show(job: JobState, map: string): Promise<void> {
    const client = await this.api();
    const blob = unwrap(
      await client.GET('/api/v1/jobs/{id}/previews/{map}', { params: { path: { id: job.id, map } }, parseAs: 'blob' })
    );
    const url = URL.createObjectURL(blob);
    const file = job.result?.maps.find(baked => baked.map === map);
    const width = file?.resolution ?? job.bake?.options.resolution ?? 0;
    const height = file ? (file.rows ?? width) : (job.bake?.options.rows ?? width);
    this.revoke();
    this.shown.set({ job: job.id, map, url, width, height });
  }

  /** Stops showing a map: something else now has the Texture pane. */
  hide(): void {
    this.revoke();
    this.shown.set(null);
  }

  private revoke(): void {
    const url = this.shown()?.url;
    if (url) {
      URL.revokeObjectURL(url);
    }
  }

  private async api(): Promise<PatinaClient> {
    this.client ??= this.createClient(await this.connection());
    return this.client;
  }

  /**
   * A job as an answer or an event gave it, unless what is held is newer: an answer to a start or a cancel can arrive
   * after the events that followed it, and is then older than what is held. Each change raises the job's version.
   */
  private put(job: JobState): void {
    this.byId.update(jobs => {
      const held = jobs.get(job.id);
      return held && held.version > job.version ? jobs : new Map(jobs).set(job.id, job);
    });
  }

  /**
   * A job as the answer that started it gave it, unless the events have said more: the job can run, and even end,
   * before that answer arrives, and the answer is then older than what is held.
   */
  private started(job: JobState): void {
    if (!this.byId().has(job.id)) {
      this.put(job);
    }
  }

  /** Reads where things stand, then follows from there. A restarted Server has a new port and new numbers. */
  private async follow(): Promise<void> {
    try {
      const connection = await this.connection();
      this.client = this.createClient(connection);
      const page = unwrap(await this.client.GET('/api/v1/jobs'));
      this.byId.set(new Map(page.jobs.map(job => [job.id, job])));
      this.sequence = page.sequence;
      this.reachable.set(true);

      const socket = this.sockets(jobSocketUrl(connection, this.sequence));
      this.socket = socket;
      socket.onmessage = event => {
        const change = JSON.parse(event.data as string) as JobEvent;
        this.sequence = change.sequence;
        this.put(change.job);
      };
      socket.onclose = () => {
        this.again();
      };
    } catch {
      this.reachable.set(false);
      this.again();
    }
  }

  private again(): void {
    if (!this.stopped) {
      this.timer = setTimeout(() => void this.follow(), ReconnectMs);
    }
  }
}
