import { DestroyRef, Injectable, InjectionToken, computed, inject, signal } from '@angular/core';
import { type Connection, type PatinaClient, type SystemInfo, createPatinaClient, unwrap } from '@patina/api-client';
import { desktop } from './desktop';

/** What a development Server listens on (engine/src/Patina.Server/Program.cs) when started with no library. */
const DevelopmentServer: Connection = { baseUrl: 'http://127.0.0.1:5190', token: null };
const PollMs = 5_000;

/** Where the Server is: the desktop app's sidecar, or a development Server in a plain browser. */
export const SERVER_CONNECTION = new InjectionToken<() => Promise<Connection>>('SERVER_CONNECTION', {
  factory: () => () => desktop()?.connection() ?? Promise.resolve(DevelopmentServer)
});

/** Makes the client for a connection; tests replace it. */
export const API_CLIENT_FACTORY = new InjectionToken<(connection: Connection) => PatinaClient>('API_CLIENT_FACTORY', {
  factory: () => connection => createPatinaClient(connection)
});

/**
 * Where the Server stands. Offline, `reason` is a sentence for a person and `detail` what the process or the network
 * said, for Diagnostics and a tooltip (#198).
 */
export type ServerState =
  | { kind: 'connecting' }
  | { kind: 'online'; system: SystemInfo }
  | { kind: 'offline'; reason: string; detail: string | null };

/** What the app says when it cannot reach its engine, whatever the network said. */
export const EngineUnreachable = 'Patina cannot reach its engine. It tries again every few seconds.';

/** The Server and its Worker, as `/api/v1/system` reports them, refreshed every few seconds. */
@Injectable({ providedIn: 'root' })
export class SystemStatus {
  private readonly connection = inject(SERVER_CONNECTION);
  private readonly createClient = inject(API_CLIENT_FACTORY);
  private readonly mutableState = signal<ServerState>({ kind: 'connecting' });
  private client: PatinaClient | null = null;
  private timer: ReturnType<typeof setTimeout> | null = null;
  private stopped = false;

  readonly state = this.mutableState.asReadonly();

  private readonly formats = computed(() => {
    const state = this.mutableState();
    return state.kind === 'online' ? state.system.formats : null;
  });

  /** What a file dialog offers for a picture: the Server's one list of what it reads. */
  readonly pictures = computed(() => this.formats()?.pictures);
  /** What a file dialog offers for a mesh, the same list a project's folder and Import read. */
  readonly meshes = computed(() => this.formats()?.meshes);
  /** What a file dialog offers for an environment. */
  readonly environments = computed(() => this.formats()?.environments);

  /** The texture sides the Server offers, so that every panel offers the same ones. Empty until it answers. */
  readonly textureSides = computed<readonly number[]>(
    () => {
      const state = this.state();
      return state.kind === 'online' ? state.system.textureSides : [];
    },
    { equal: (a, b) => a.join() === b.join() }
  );

  /** The side a new document starts at, or null until the Server answers. */
  readonly defaultTextureSide = computed(() => {
    const state = this.state();
    return state.kind === 'online' ? state.system.defaultTextureSide : null;
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.stopped = true;
      if (this.timer !== null) {
        clearTimeout(this.timer);
      }
    });
    // A Server that cannot start never answers, so its supervisor is the one who can say so.
    desktop()?.onSidecars(statuses => {
      const failed = statuses.find(status => status.state === 'failed');
      if (failed) {
        this.mutableState.set({
          kind: 'offline',
          reason: failed.name === 'worker' ? 'The GPU engine stopped.' : 'The engine stopped.',
          detail: failed.detail
        });
      }
    });
    void this.refresh();
  }

  /** Asks once, then again after a pause, whatever the answer: a Server can come up or go away at any time. */
  private async refresh(): Promise<void> {
    try {
      this.client ??= this.createClient(await this.connection());
      const system = unwrap(await this.client.GET('/api/v1/system'));
      this.mutableState.set({ kind: 'online', system });
    } catch (error) {
      this.client = null;
      this.mutableState.set({
        kind: 'offline',
        reason: EngineUnreachable,
        detail: error instanceof Error ? error.message : String(error)
      });
    }

    if (!this.stopped) {
      this.timer = setTimeout(() => void this.refresh(), PollMs);
    }
  }
}
