import { DestroyRef, Injectable, InjectionToken, effect, inject, signal, untracked } from '@angular/core';
import type {
  ViewportCameraState,
  ViewportDisplay,
  ViewportFollow,
  ViewportInput,
  ViewportNodeShown,
  ViewportScreenshot,
  ViewportState
} from '@patina/api-client';
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
import { IsolationStore } from './isolation-store';
import { DefaultCamera } from './orbit';
import { ViewportJump } from './viewport-jump';

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
  sharpen: 0,
  bloom: 0,
  warmth: 0,
  tint: 0,
  backgroundColor: '#000000',
  grid: false,
  pictureLane: 'rgb',
  pictureScale: 1,
  shape: 'sphere',
  depthOfField: 0,
  focus: 0
};

/** What a material or a graph with no mesh of its own is seen on, as the Display panel offers them. */
export const Shapes: readonly { value: string; label: string }[] = [
  { value: 'sphere', label: 'Sphere' },
  { value: 'plane', label: 'Plane' },
  { value: 'cube', label: 'Cube' },
  { value: 'cylinder', label: 'Cylinder' }
];

/** The largest picture a screenshot can be, which is the largest the Server's frame slots hold. */
const MaxShot = { width: 3840, height: 2160 };

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
  private readonly isolation = inject(IsolationStore);
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
  /** How many times the input socket has opened, so a reopen can be told from the first. */
  private opens = 0;
  private size: { width: number; height: number } | null = null;

  readonly mesh = signal<string | null>(null);
  /** The graph node whose picture is on the mesh in place of the material, or null. */
  readonly node = signal<ViewportNodeShown | null>(null);
  /** The graph the Server keeps on the mesh, or null. */
  readonly following = signal<ViewportFollow | null>(null);
  /** The frames' count of changes to what lies on the mesh, once a frame has said it. */
  private laid: number | null = null;
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
    // Another pane asked to look at something, such as a problem Checks found.
    const jump = inject(ViewportJump);
    effect(() => {
      const target = jump.target();
      if (target) {
        untracked(() => this.go(target.to.view ?? null, target.to.camera ?? null));
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

  /** Shows a view and looks through a camera, for another pane: either left out stays as it is. */
  private go(view: string | null, camera: ViewportCameraState | null): void {
    if (view !== null && view !== this.display().view) {
      this.setDisplay({ view, picture: null });
    }

    if (camera !== null) {
      this.moveCamera(camera, false);
    }
  }

  /**
   * Writes the picture as it is now to a PNG on the Server's machine, at the size it is drawn at, or says why it could
   * not.
   */
  async screenshot(path: string): Promise<ViewportScreenshot | null> {
    try {
      const client = this.createClient(await this.connection());
      const size = this.size;
      // The Server's frame slots hold 4K at most, so a larger canvas is taken at the most a slot holds.
      const fit = size ? Math.min(1, MaxShot.width / size.width, MaxShot.height / size.height) : 1;
      const body = size
        ? { path, width: Math.max(2, Math.floor(size.width * fit)), height: Math.max(2, Math.floor(size.height * fit)) }
        : { path };
      const shot = unwrap(await client.POST('/api/v1/viewport/screenshot', { body }));
      this.error.set(null);
      return shot;
    } catch (error) {
      this.error.set(messageOf(error));
      return null;
    }
  }

  /**
   * Has the Server keep a graph on the mesh: its outputs, or with `node` the node its selection views, laid again after
   * each evaluation and each change of selection (#244). Null stops. The Server does the following, so any client that
   * asks gets it; the frames say when what lies on the mesh has changed.
   */
  async follow(graph: string | null, node = false): Promise<void> {
    try {
      const client = this.createClient(await this.connection());
      const state = unwrap(await client.PUT('/api/v1/viewport/follow', { body: { graph, node } }));
      this.hear(state);
    } catch (error) {
      this.error.set(messageOf(error));
    }
  }

  /** Opens a mesh, once a tab on its way to the front is there: it would otherwise take the mesh away again (#237). */
  async openMesh(path: string): Promise<void> {
    try {
      const state = await this.projects.openMesh(path);
      this.mesh.set(state.mesh ?? null);
      this.node.set(null);
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

      // A new shape takes a stand-in's place.
      this.mesh.set(state.mesh ?? null);
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
    this.hear(state);
    this.display.set(state.display);
    this.isolation.heard(state.isolation);
    this.error.set(state.error ?? null);
  }

  /** What lies on the mesh, as the Server says. */
  private hear(state: ViewportState): void {
    this.mesh.set(state.mesh ?? null);
    this.node.set(state.node ?? null);
    this.following.set(state.following ?? null);
  }

  /**
   * What lies on the mesh changed without this client asking, as when a followed graph evaluates: read it again. The
   * first frame is read too, since a mesh the Server laid where there was none is what starts the frames.
   */
  private heardLaid(laid: number): void {
    const seen = this.laid;
    this.laid = laid;
    if (seen !== laid) {
      void this.refresh().catch(() => undefined); // the next change reads it again
    }
  }

  private async hearIsolation(): Promise<void> {
    try {
      const state = unwrap(await this.createClient(await this.connection()).GET('/api/v1/viewport'));
      this.isolation.heard(state.isolation);
    } catch (error) {
      this.error.set(messageOf(error));
    }
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
      this.hear(state);
      this.camera.set(state.camera);
      this.isolation.heard(state.isolation);
      if (this.adopted === null) {
        this.display.set(state.display);
      }

      this.error.set(state.error ?? null);
      this.transport = this.createTransport(
        connection,
        frame => {
          this.heardLaid(frame.laid);
          this.onFrame?.(frame);
        },
        () => {
          if (this.size) {
            this.resize(this.size.width, this.size.height);
          }

          // Open again after a drop: a Server that restarted starts from its defaults, so the view is told again
          // where it looked and how it was shown (#226). A Server that kept them hears what it has.
          if (this.opens++ > 0) {
            this.send({ camera: this.camera(), interacting: false });
            this.sendDisplay(this.display());
            // What it drew alone is not told again: that is the Server's, kept in memory only, so one that restarted
            // draws the whole model, and the controls hear that rather than claim parts are alone that are not.
            void this.hearIsolation();
          }
        }
      );
    } catch (error) {
      this.error.set(messageOf(error));
    }
  }
}
