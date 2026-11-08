import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import {
  type BakeRequest,
  type Connection,
  type JobEvent,
  type JobState,
  type PatinaClient,
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
    this.put(job);
  }

  /** Asks for a bake. Throws the Server's refusal, which is what the panel shows. */
  async start(request: BakeRequest): Promise<JobState> {
    const [job] = await this.startAll([request]);
    if (!job) {
      throw new Error('The Server made no job of the bake.');
    }

    return job;
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
      this.put(job);
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

  /** Shows a finished bake on the mesh it was baked onto, in every client's viewport: the Server holds the choice. */
  async showOnMesh(job: JobState): Promise<void> {
    const client = await this.api();
    unwrap(await client.PUT('/api/v1/viewport/bake', { body: { job: job.id } }));
    this.viewportChanged.update(count => count + 1);
  }

  /** Lays a graph's outputs over the mesh in the viewport, as they stand now. */
  async showGraphOnMesh(graph: string): Promise<void> {
    const client = await this.api();
    unwrap(await client.PUT('/api/v1/viewport/graph', { body: { graph } }));
    this.viewportChanged.update(count => count + 1);
  }

  /** Shows one map of a finished job in the Texture pane. */
  async show(job: JobState, map: string): Promise<void> {
    const client = await this.api();
    const blob = unwrap(
      await client.GET('/api/v1/jobs/{id}/previews/{map}', { params: { path: { id: job.id, map } }, parseAs: 'blob' })
    );
    const url = URL.createObjectURL(blob);
    const side = job.result?.maps.find(file => file.map === map)?.resolution ?? job.bake?.options.resolution ?? 0;
    this.revoke();
    this.shown.set({ job: job.id, map, url, width: side, height: side });
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

  private put(job: JobState): void {
    this.byId.update(jobs => new Map(jobs).set(job.id, job));
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
