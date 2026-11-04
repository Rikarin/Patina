import { DestroyRef, Injectable, InjectionToken, inject, signal } from '@angular/core';
import type { ViewportCameraState, ViewportInput } from '@patina/api-client';
import { unwrap } from '@patina/api-client';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';
import { type FrameTransport, type FrameTransportFactory, WebSocketFrameTransport, type ViewportFrame } from './frame-transport';
import { DefaultCamera } from './orbit';

/** Makes the frame transport; tests replace it. */
export const FRAME_TRANSPORT_FACTORY = new InjectionToken<FrameTransportFactory>('FRAME_TRANSPORT_FACTORY', {
  factory: () => (connection, onFrame, onOpen) => new WebSocketFrameTransport(connection, onFrame, onOpen)
});

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
  private transport: FrameTransport | null = null;
  private inputNumber = 0;
  private readonly sentAt = new Map<number, number>();
  private latest = 0;
  private painted: number[] = [];
  private size: { width: number; height: number } | null = null;

  readonly mesh = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly stats = signal<ViewportStats | null>(null);
  readonly camera = signal<ViewportCameraState>(DefaultCamera);
  /** Frames painted since the pane opened. */
  readonly frames = signal(0);
  onFrame: ((frame: ViewportFrame) => void) | null = null;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.transport?.close());
    void this.start();
  }

  async openMesh(path: string): Promise<void> {
    try {
      const client = this.createClient(await this.connection());
      const state = unwrap(await client.PUT('/api/v1/viewport/mesh', { body: { path } }));
      this.mesh.set(state.mesh ?? null);
      this.error.set(null);
    } catch (error) {
      this.error.set(error instanceof Error ? error.message : String(error));
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
      this.error.set(error instanceof Error ? error.message : String(error));
    }
  }
}
