import { type ChildProcess, spawn } from 'node:child_process';
import { randomBytes } from 'node:crypto';
import { createServer } from 'node:net';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { type Command, engine } from './engine';

export type SidecarName = 'worker' | 'server';

export interface SidecarStatus {
  name: SidecarName;
  state: 'starting' | 'running' | 'restarting' | 'stopped' | 'failed';
  detail: string | null;
  /** The running process's id, or null. */
  pid: number | null;
}

export interface Connection {
  baseUrl: string;
  token: string;
}

const MaxBackoffMs = 10_000;
const ReadyTimeoutMs = 60_000;

/**
 * One supervised child process: started with a command line computed at each (re)start, restarted with backoff
 * when it exits on its own, stopped politely on request (its standard input closed, and SIGTERM where there are
 * signals), and killed if it is still running 3 s later.
 */
class Sidecar {
  private child: ChildProcess | null = null;
  private wanted = false;
  private failures = 0;
  private restartTimer: NodeJS.Timeout | null = null;
  readonly log: string[] = [];
  status: SidecarStatus;

  constructor(
    readonly name: SidecarName,
    private readonly commandLine: () => Command & { env?: NodeJS.ProcessEnv },
    /** What the process prints once it serves: running (again, after a restart). */
    private readonly ready: RegExp,
    private readonly onStatus: () => void
  ) {
    this.status = { name, state: 'stopped', detail: null, pid: null };
  }

  /** Starts the process unless it already runs (or is about to restart): never a second one beside the first. */
  start(): void {
    this.wanted = true;
    if (this.child || this.restartTimer) {
      return;
    }

    this.launch();
  }

  /** Whether the process runs now. */
  get running(): boolean {
    return this.child !== null;
  }

  async stop(): Promise<void> {
    this.wanted = false;
    if (this.restartTimer) {
      clearTimeout(this.restartTimer);
      this.restartTimer = null;
    }

    const child = this.child;
    if (child?.exitCode === null) {
      const exited = new Promise<void>(done => child.once('exit', () => done()));
      // Closing its lifeline asks it to stop, and it then stops cleanly: the Server leaves the discovery file and removes its
      // socket. On Windows that is the only polite way: Node has no signals there, and kill() terminates outright.
      child.stdin?.end();
      if (process.platform !== 'win32') {
        child.kill('SIGTERM');
      }

      const killer = setTimeout(() => child.kill('SIGKILL'), 3_000);
      await exited;
      clearTimeout(killer);
    }

    this.set('stopped', null);
  }

  /** Called once the process proves healthy (the server answered), so the next failure starts the backoff over. */
  healthy(): void {
    this.failures = 0;
    this.set('running', null);
  }

  private launch(): void {
    this.restartTimer = null;
    let command: ReturnType<typeof this.commandLine>;
    try {
      command = this.commandLine();
    } catch (error) {
      this.set('failed', (error as Error).message);
      return;
    }

    const child = spawn(command.command, command.args, {
      env: { ...process.env, ...command.env },
      // Standard input is the child's lifeline (StdinWatch in the engine): the OS closes it however this app ends, a
      // crash or a force quit included, and the child then stops on its own instead of outliving the app.
      stdio: ['pipe', 'pipe', 'pipe'],
      windowsHide: true
    });
    this.child = child;
    this.set(this.failures === 0 ? 'starting' : 'restarting', null);
    child.stdin.on('error', () => undefined); // a child that already exited: nothing to tell it
    const keep = (data: Buffer) => {
      const lines = data.toString().split('\n').filter(Boolean);
      this.log.push(...lines);
      this.log.splice(0, Math.max(0, this.log.length - 200));
      const waiting = this.status.state === 'starting' || this.status.state === 'restarting';
      if (this.child === child && waiting && lines.some(line => this.ready.test(line))) {
        this.set('running', null);
      }
    };
    child.stdout.on('data', keep);
    child.stderr.on('data', keep);
    const ended = (code: number | string | null) => {
      if (this.child !== child) {
        return;
      }

      this.child = null;
      if (!this.wanted) {
        return;
      }

      const delay = Math.min(MaxBackoffMs, 500 * 2 ** this.failures++);
      this.set('restarting', `exited (${code}); restarting in ${Math.round(delay / 1000)} s`);
      this.restartTimer = setTimeout(() => this.launch(), delay);
    };
    child.once('exit', (code, signal) => ended(signal ?? code));
    // A process that never started emits no exit: it ends here, so it is retried like one that crashed.
    child.once('error', error => {
      this.log.push(`spawn failed: ${error.message}`);
      if (child.pid === undefined) {
        ended(error.message);
      }
    });
  }

  private set(state: SidecarStatus['state'], detail: string | null): void {
    this.status = { name: this.name, state, detail, pid: this.child?.pid ?? null };
    this.onStatus();
  }
}

/**
 * The local backend: patina-server on loopback and patina-worker connected to it. The Server starts nothing itself
 * (plan §8), so this is what keeps the two running: either can die and come back without the other.
 */
export class Sidecars {
  private readonly socket = join(tmpdir(), `patina-${process.pid}.sock`);
  /** A token for this run only. */
  private readonly token = randomBytes(32).toString('base64url');
  private port = 0;
  private ready: Promise<Connection> | null = null;
  private readonly worker: Sidecar;
  private readonly server: Sidecar;

  constructor(
    /** The library to open, or null to start the Server with none. */
    private readonly library: string | null,
    onStatus: (statuses: SidecarStatus[]) => void
  ) {
    const notify = () => onStatus(this.statuses());
    this.worker = new Sidecar('worker', () => this.workerCommand(), / connected to /, notify);
    this.server = new Sidecar('server', () => this.serverCommand(), /Now listening on/, notify);
  }

  statuses(): SidecarStatus[] {
    return [this.worker.status, this.server.status];
  }

  logs(): Record<SidecarName, string[]> {
    return { worker: this.worker.log, server: this.server.log };
  }

  /** Starts both processes; resolves with the connection once the Server answers. */
  start(): Promise<Connection> {
    this.ready ??= (async () => {
      // A retry after a slow first start finds the Server still starting: it keeps its port and is not started twice.
      if (this.port === 0 || !this.server.running) {
        this.port = await freePort();
      }

      this.server.start();
      this.worker.start();
      const connection = { baseUrl: `http://127.0.0.1:${this.port}`, token: this.token };
      await waitForServer(connection);
      this.server.healthy();
      return connection;
    })().catch((error: unknown) => {
      this.ready = null;
      throw error;
    });
    return this.ready;
  }

  async stop(): Promise<void> {
    await Promise.all([this.worker.stop(), this.server.stop()]);
    this.ready = null;
  }

  private workerCommand(): Command {
    const worker = engine.worker();
    return { command: worker.command, args: [...worker.args, '--socket', this.socket, '--watch-stdin'] };
  }

  /**
   * The token goes in the environment, never on the command line: other accounts on the machine can read every
   * process's arguments (`ps`, Task Manager), but not its environment.
   */
  private serverCommand(): Command & { env: NodeJS.ProcessEnv } {
    const server = engine.server();
    return {
      command: server.command,
      args: [
        ...server.args,
        `--urls=http://127.0.0.1:${this.port}`,
        `--Patina:Worker:SocketPath=${this.socket}`,
        '--Patina:WatchStdin=true',
        ...(this.library ? ['--library', this.library] : [])
      ],
      env: { Patina__Token: this.token }
    };
  }
}

function freePort(): Promise<number> {
  return new Promise((done, fail) => {
    const probe = createServer();
    probe.once('error', fail);
    probe.listen(0, '127.0.0.1', () => {
      const address = probe.address();
      probe.close(() => (typeof address === 'object' && address ? done(address.port) : fail(new Error('No port'))));
    });
  });
}

/** Waits for a Server to answer. A Server that refuses the token (401) fails at once: waiting will not change that. */
export async function waitForServer(connection: Connection, timeoutMs = ReadyTimeoutMs): Promise<void> {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    let response: Response | null = null;
    try {
      response = await fetch(`${connection.baseUrl}/api/v1/system`, {
        headers: { Authorization: `Bearer ${connection.token}` },
        signal: AbortSignal.timeout(2_000)
      });
    } catch {
      // not up yet
    }

    if (response?.ok) {
      return;
    }

    if (response?.status === 401) {
      throw new Error(`The Patina server at ${connection.baseUrl} refused the access token.`);
    }

    await new Promise(resolve => setTimeout(resolve, 250));
  }

  throw new Error(`The Patina server did not start within ${timeoutMs / 1000} s.`);
}
