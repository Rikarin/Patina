import { DestroyRef, Injectable, InjectionToken, effect, inject, signal, untracked } from '@angular/core';
import type { ViewportCameraState, ViewportDisplay, ViewportInput } from '@patina/api-client';
import { unwrap } from '@patina/api-client';
import { BakeStore } from '../bake/bake-store';
import { messageOf } from '../core/messages';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';
import { ProjectStore } from '../library/project-store';
import {
  type FrameTransport,
  type FrameTransportFactory,
  StreamFrameTransport,
  type ViewportFrame
} from './frame-transport';
import { DefaultCamera } from './orbit';

/** Makes the frame transport; tests replace it. */
export const FRAME_TRANSPORT_FACTORY = new InjectionToken<FrameTransportFactory>('FRAME_TRANSPORT_FACTORY', {
  factory: () => (connection, onFrame, onOpen) => new StreamFrameTransport(connection, onFrame, onOpen)
});

/** How the viewport opens, which the Server says too: a project that kept nothing gets this. */
export const DefaultDisplay: ViewportDisplay = {
  environment: 'studio',
  environmentRotation: 0,
  backgroundBlur: 0.66,
  backgroundOpacity: 1,
  lightRotation: 0,
  exposure: 0,
  shadows: 1,
  toneMapping: 'aces',
  look: null,
  view: 'lit',
  picture: null,
  wireframe: false,
  orthographic: false,
  fieldOfView: 0.7,
  contrast: 1,
  saturation: 1,
  vignette: 0,
  grain: 0,
  aberration: 0,
  sharpen: 0
};

/** How long the display waits after the last change before the project keeps it. */
const KeepAfterMs = 600;

/** What a pane shows beside the picture. */
export interface ViewportStats {
  /** Frames painted in the last second. */
  fps: number;
  renderMs: number;
  /** From the newest input sent to the frame that includes it being ready to paint. */
  latencyMs: number;
  width: number;
  height: number;
}

/**
 * The viewport as a client: the Server owns its state, and this mirrors what the pane needs. Only the newest frame
 * counts; one that arrives before the last was painted replaces it.
 */
@Injectable()
export class ViewportClient {
  private readonly connection = inject(SERVER_CONNECTION);
  private readonly createClient = inject(API_CLIENT_FACTORY);
  private readonly createTransport = inject(FRAME_TRANSPORT_FACTORY);
  private readonly bakes = inject(BakeStore);
  private readonly projects = inject(ProjectStore);
  private transport: FrameTransport | null = null;
  /** The project whose display is on the viewport, which a change is kept in. */
  private adopted: string | null = null;
  private keeping: ReturnType<typeof setTimeout> | null = null;
  /** Settings sent while others were on their way, of which only the newest goes. */
  private queued: ViewportDisplay | null = null;
  private sending = false;
  private inputNumber = 0;
  private readonly sentAt = new Map<number, number>();
  private latest = 0;
  private painted: number[] = [];
  private size: { width: number; height: number } | null = null;

  readonly mesh = signal<string | null>(null);
  /** How the model is lit, shown and finished, as the Server has it or as it was just asked for. */
  readonly display = signal<ViewportDisplay>(DefaultDisplay);
  readonly error = signal<string | null>(null);
  readonly stats = signal<ViewportStats | null>(null);
  readonly camera = signal<ViewportCameraState>(DefaultCamera);
  /** Frames painted since the pane opened. */
  readonly frames = signal(0);
  onFrame: ((frame: ViewportFrame) => void) | null = null;

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.transport?.close();
      this.keepNow();
    });
    void this.start();
    effect(() => {
      if (this.bakes.viewportChanged() > 0) {
        void this.refresh();
      }
    });
    // A project opens its viewport as it was left, once its contents have been read. Only a change of project puts
    // it back, not a save.
    effect(() => {
      const project = this.projects.active()?.project ?? null;
      if (this.projects.current() !== null) {
        untracked(() => this.adopt(project));
      }
    });
  }

  /** Changes some of the display settings: the frames show it at once, and the project keeps it a moment later. */
  setDisplay(change: Partial<ViewportDisplay>): void {
    const next = { ...this.display(), ...change };
    this.display.set(next);
    this.sendDisplay(next);
    this.keepLater();
  }

  /** Lays a graph's outputs over the mesh on screen. */
  async showGraph(graph: string): Promise<void> {
    try {
      await this.bakes.showGraphOnMesh(graph);
      this.error.set(null);
    } catch (error) {
      this.error.set(messageOf(error));
    }
  }

  async openMesh(path: string): Promise<void> {
    try {
      const client = this.createClient(await this.connection());
      const state = unwrap(await client.PUT('/api/v1/viewport/mesh', { body: { path } }));
      this.mesh.set(state.mesh ?? null);
      this.error.set(null);
    } catch (error) {
      this.error.set(messageOf(error));
    }
  }

  /** Puts back how a project left the viewport, or how it opens, when another project comes to the front. */
  private adopt(project: string | null): void {
    if (project === this.adopted || project === null) {
      return;
    }

    this.keepNow();
    this.adopted = project;
    const kept = this.projects.current()?.viewport ?? DefaultDisplay;
    this.display.set(kept);
    this.sendDisplay(kept);
  }

  /** Sends settings, one request at a time: a slider moves faster than a reply comes back. */
  private sendDisplay(display: ViewportDisplay): void {
    if (this.sending) {
      this.queued = display;
      return;
    }

    this.sending = true;
    void this.put(display).finally(() => {
      this.sending = false;
      const next = this.queued;
      this.queued = null;
      if (next) {
        this.sendDisplay(next);
      }
    });
  }

  private async put(display: ViewportDisplay): Promise<void> {
    try {
      const client = this.createClient(await this.connection());
      const state = unwrap(await client.PUT('/api/v1/viewport/display', { body: display }));
      if (!this.queued) {
        this.display.set(state.display);
      }

      this.error.set(null);
    } catch (error) {
      // A file the project kept may have gone: what the Server still shows is what the controls say.
      this.error.set(messageOf(error));
      await this.refresh().catch(() => undefined);
    }
  }

  private keepLater(): void {
    if (this.keeping !== null) {
      clearTimeout(this.keeping);
    }

    this.keeping = setTimeout(() => this.keepNow(), KeepAfterMs);
  }

  /** The project keeps the settings now, if a change is waiting. */
  private keepNow(): void {
    if (this.keeping === null) {
      return;
    }

    clearTimeout(this.keeping);
    this.keeping = null;
    if (this.adopted !== null) {
      void this.projects.keepViewport(this.adopted, this.display());
    }
  }

  moveCamera(camera: ViewportCameraState, interacting: boolean): void {
    this.camera.set(camera);
    this.send({ camera, interacting });
  }

  resize(width: number, height: number): void {
    this.size = { width, height };
    this.send({ width, height });
  }

  /** Called by the pane once a frame is on the canvas. */
  framePainted(frame: ViewportFrame): void {
    const now = performance.now();
    this.painted.push(now);
    this.painted = this.painted.filter(time => now - time < 1000);
    const sent = this.sentAt.get(frame.input);
    if (sent !== undefined) {
      this.latest = now - sent;
    }

    for (const input of this.sentAt.keys()) {
      if (input <= frame.input) {
        this.sentAt.delete(input);
      }
    }

    this.frames.update(count => count + 1);
    this.stats.set({
      fps: this.painted.length,
      renderMs: frame.renderMs,
      latencyMs: this.latest,
      width: frame.width,
      height: frame.height
    });
  }

  /** Something else changed what the Server shows: the frames say so by themselves, and the label needs telling. */
  async sync(): Promise<void> {
    await this.refresh();
  }

  private async refresh(): Promise<void> {
    const state = unwrap(await this.createClient(await this.connection()).GET('/api/v1/viewport'));
    this.mesh.set(state.mesh ?? null);
    this.display.set(state.display);
    this.error.set(state.error ?? null);
  }

  private send(input: ViewportInput): void {
    const number = ++this.inputNumber;
    this.sentAt.set(number, performance.now());
    this.transport?.send({ ...input, input: number });
  }

  private async start(): Promise<void> {
    try {
      const connection = await this.connection();
      const state = unwrap(await this.createClient(connection).GET('/api/v1/viewport'));
      this.mesh.set(state.mesh ?? null);
      this.camera.set(state.camera);
      if (this.adopted === null) {
        this.display.set(state.display);
      }

      this.error.set(state.error ?? null);
      this.transport = this.createTransport(
        connection,
        frame => this.onFrame?.(frame),
        () => {
          if (this.size) {
            this.resize(this.size.width, this.size.height);
          }
        }
      );
    } catch (error) {
      this.error.set(messageOf(error));
    }
  }
}
