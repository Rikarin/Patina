import { DestroyRef, Injectable, InjectionToken, inject, signal } from '@angular/core';
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

export type ServerState =
  { kind: 'connecting' } | { kind: 'online'; system: SystemInfo } | { kind: 'offline'; reason: string };

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
        this.mutableState.set({ kind: 'offline', reason: failed.detail ?? `The ${failed.name} stopped.` });
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
      this.mutableState.set({ kind: 'offline', reason: error instanceof Error ? error.message : String(error) });
    }

    if (!this.stopped) {
      this.timer = setTimeout(() => void this.refresh(), PollMs);
    }
  }
}
