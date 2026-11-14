import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  effect,
  inject,
  signal,
  untracked
} from '@angular/core';
import type { StackDecal, StackLayer, StackSurfacePath, ViewportPick } from '@patina/api-client';
import { ViewportClient } from '../viewport/viewport-client';
import { PaintStore } from './paint-store';
import {
  type Quad,
  decalOutline,
  onDecalPlane,
  pathOutline,
  pathPoint,
  turned,
  vector,
  warpGrid,
  warpOf
} from './surface-frame';
import { project } from './symmetry-guide';

/** What a drag on the guide holds. */
type Handle =
  | { kind: 'move' }
  | { kind: 'scale' }
  | { kind: 'turn' }
  | { kind: 'warp'; index: number }
  | { kind: 'point'; index: number };

/** How long the camera rests before the guide asks the Worker where it now looks. */
const Settle = 120;

/**
 * The chosen fill's decal or path over the viewport (#130, #133, #134): the decal's square with handles that move it
 * over the mesh, scale it and turn it, its warp grid when that is open, and a path's points, each of which drags over
 * the mesh and an Alt-click takes away. A drag is drawn as it goes and sent once, on release, as one edit, so it undoes
 * as one step and the picture follows it from the Worker.
 */
@Component({
  selector: 'ptn-surface-guide',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'pointer-events-none absolute inset-0 overflow-hidden', 'data-testid': 'surface-guide' },
  template: `
    @if (look(); as view) {
      <svg class="absolute inset-0 size-full" aria-hidden="true">
        @if (decalShown(); as decal) {
          @if (outline(); as corners) {
            <polygon
              class="fill-amber-300/10 stroke-amber-300/90"
              stroke-width="1.5"
              data-testid="surface-decal"
              [attr.points]="points(corners)"
            />
            <line
              class="stroke-amber-300/90"
              stroke-width="1.5"
              [attr.x1]="middle(corners[0], corners[1])[0]"
              [attr.y1]="middle(corners[0], corners[1])[1]"
              [attr.x2]="turnAt(corners)[0]"
              [attr.y2]="turnAt(corners)[1]"
            />
            <circle
              class="pointer-events-auto cursor-move fill-black/50 stroke-white"
              r="7"
              data-testid="surface-decal-move"
              [attr.cx]="centre(corners)[0]"
              [attr.cy]="centre(corners)[1]"
              (pointerdown)="hold($event, { kind: 'move' })"
            >
              <title>Drag to move the decal over the mesh</title>
            </circle>
            <rect
              class="pointer-events-auto cursor-nwse-resize fill-black/50 stroke-white"
              width="12"
              height="12"
              data-testid="surface-decal-scale"
              [attr.x]="corners[2][0] - 6"
              [attr.y]="corners[2][1] - 6"
              (pointerdown)="hold($event, { kind: 'scale' })"
            >
              <title>Drag to scale the decal</title>
            </rect>
            <circle
              class="pointer-events-auto cursor-grab fill-black/50 stroke-white"
              r="6"
              data-testid="surface-decal-turn"
              [attr.cx]="turnAt(corners)[0]"
              [attr.cy]="turnAt(corners)[1]"
              (pointerdown)="hold($event, { kind: 'turn' })"
            >
              <title>Drag to turn the decal</title>
            </circle>
            @if (paint.warping()) {
              @if (grid(); as warp) {
                @for (line of gridLines(warp); track $index) {
                  <polyline class="fill-none stroke-sky-300/80" stroke-width="1" [attr.points]="line" />
                }
                @for (point of warp; track $index) {
                  <circle
                    class="pointer-events-auto cursor-move fill-sky-300/60 stroke-white"
                    r="5"
                    [attr.data-testid]="'surface-warp-' + $index"
                    [attr.cx]="point[0]"
                    [attr.cy]="point[1]"
                    (pointerdown)="hold($event, { kind: 'warp', index: $index })"
                  >
                    <title>Drag to bend the picture</title>
                  </circle>
                }
              }
            }
          }
        }
        @if (pathShown(); as path) {
          <polyline
            class="fill-none stroke-amber-300/90"
            stroke-width="1.5"
            stroke-dasharray="5 3"
            data-testid="surface-path"
            [attr.points]="pathLine()"
          />
          @for (point of pathPoints(); track $index) {
            @if (point; as at) {
              <circle
                class="pointer-events-auto cursor-move fill-black/50 stroke-white"
                r="6"
                [attr.data-testid]="'surface-path-point-' + $index"
                [attr.cx]="at[0]"
                [attr.cy]="at[1]"
                (pointerdown)="hold($event, { kind: 'point', index: $index })"
              >
                <title>Drag to move this point over the mesh, Alt-click to take it away</title>
              </circle>
            }
          }
        }
      </svg>
    }
  `
})
export class SurfaceGuide {
  protected readonly paint = inject(PaintStore);
  private readonly viewport = inject(ViewportClient);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private resting: ReturnType<typeof setTimeout> | undefined;
  private held: { handle: Handle; x: number; y: number; from: StackDecal | StackSurfacePath } | null = null;
  private asking = false;
  private wanted: { x: number; y: number } | null = null;

  /** The decal or path while a hand is on it, drawn ahead of the Server. */
  private readonly draft = signal<{ layer: string; decal?: StackDecal; path?: StackSurfacePath } | null>(null);
  /** Where the camera looks, asked of the Worker as it settles: what puts the model's places on the picture. */
  private readonly looked = signal<{ key: string; pick: ViewportPick } | null>(null);

  /** The chosen fill, when it is a decal or a path. */
  private readonly layer = computed<StackLayer | null>(() => {
    const layer = this.paint.chosen();
    return layer?.kind === 'fill' && (layer.projection === 'Decal' || layer.projection === 'Path') ? layer : null;
  });

  private readonly key = computed(() => {
    const { yaw, pitch, distance, panX, panY, panZ } = this.viewport.camera();
    return [yaw, pitch, distance, panX, panY, panZ].join();
  });

  /** The camera, while it is the one the picture shows. */
  protected readonly look = computed(() => {
    const looked = this.looked();
    return this.layer() && looked?.key === this.key() ? looked.pick.view : null;
  });

  protected readonly decalShown = computed(() => {
    const layer = this.layer();
    const draft = this.draft();
    return layer?.projection === 'Decal'
      ? ((draft?.layer === layer.id ? draft.decal : null) ?? layer.decal ?? null)
      : null;
  });

  protected readonly pathShown = computed(() => {
    const layer = this.layer();
    const draft = this.draft();
    return layer?.projection === 'Path'
      ? ((draft?.layer === layer.id ? draft.path : null) ?? layer.path ?? null)
      : null;
  });

  protected readonly outline = computed(() => {
    const decal = this.decalShown();
    const view = this.look();
    return decal && view ? decalOutline(decal, view) : null;
  });

  protected readonly grid = computed(() => {
    const decal = this.decalShown();
    const view = this.look();
    return decal && view ? warpGrid(decal, view) : null;
  });

  protected readonly pathPoints = computed(() => {
    const path = this.pathShown();
    const view = this.look();
    return path && view ? pathOutline(path, view) : [];
  });

  protected readonly pathLine = computed(() =>
    this.pathPoints()
      .filter(point => point !== null)
      .map(([x, y]) => `${x.toFixed(1)},${y.toFixed(1)}`)
      .join(' ')
  );

  constructor() {
    // The guide follows the camera once it rests, since each look is a round trip to the Worker.
    effect(() => {
      const key = this.key();
      if (!this.layer()) {
        return;
      }

      untracked(() => {
        clearTimeout(this.resting);
        this.resting = setTimeout(() => {
          void this.paint
            .look(this.aim())
            .then(pick => this.looked.set({ key, pick }))
            .catch(() => undefined);
        }, Settle);
      });
    });
    inject(DestroyRef).onDestroy(() => clearTimeout(this.resting));
  }

  protected points(corners: Quad): string {
    return corners.map(([x, y]) => `${x.toFixed(1)},${y.toFixed(1)}`).join(' ');
  }

  protected middle(a: readonly [number, number], b: readonly [number, number]): [number, number] {
    return [(a[0] + b[0]) / 2, (a[1] + b[1]) / 2];
  }

  protected centre(corners: Quad): [number, number] {
    const [a, , c] = corners;
    return this.middle(a, c);
  }

  /** The turn handle: above the middle of the top edge, away from the centre. */
  protected turnAt(corners: Quad): [number, number] {
    const top = this.middle(corners[0], corners[1]);
    const centre = this.centre(corners);
    const away = Math.hypot(top[0] - centre[0], top[1] - centre[1]) || 1;
    return [top[0] + ((top[0] - centre[0]) / away) * 22, top[1] + ((top[1] - centre[1]) / away) * 22];
  }

  /** The warp grid's rows, then its columns, each as a line through its points: a 3 by 3 grid or a finer one. */
  protected gridLines(grid: readonly (readonly [number, number])[]): string[] {
    const side = Math.round(Math.sqrt(grid.length));
    const through = (indices: readonly number[]) =>
      indices
        .map(index => grid[index] ?? [0, 0])
        .map(([x, y]) => `${x.toFixed(1)},${y.toFixed(1)}`)
        .join(' ');
    const steps = Array.from({ length: side }, (_, at) => at);
    return [
      ...steps.map(row => through(steps.map(column => row * side + column))),
      ...steps.map(column => through(steps.map(row => row * side + column)))
    ];
  }

  protected hold(event: PointerEvent, handle: Handle): void {
    const layer = this.layer();
    const from = layer?.projection === 'Decal' ? this.decalShown() : this.pathShown();
    if (!layer || !from || event.button !== 0) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    if (handle.kind === 'point' && event.altKey) {
      const path = from as StackSurfacePath;
      void this.paint.edit({
        op: 'setLayer',
        layer: layer.id,
        path: { ...path, points: path.points.filter((_, index) => index !== handle.index) }
      });
      return;
    }

    const target = event.target as Element;
    target.setPointerCapture(event.pointerId);
    this.held = { handle, from, ...this.at(event) };
    const moved = (move: Event) => this.drag(move as PointerEvent);
    const done = () => {
      target.removeEventListener('pointermove', moved);
      target.removeEventListener('pointerup', done);
      target.removeEventListener('pointercancel', done);
      void this.release();
    };
    target.addEventListener('pointermove', moved);
    target.addEventListener('pointerup', done);
    target.addEventListener('pointercancel', done);
  }

  private drag(event: PointerEvent): void {
    const held = this.held;
    const layer = this.layer();
    const view = this.look();
    if (!held || !layer || !view) {
      return;
    }

    const { x, y } = this.at(event);
    const handle = held.handle;
    if (handle.kind === 'move' || handle.kind === 'point') {
      this.wanted = { x, y };
      void this.ask();
      return;
    }

    const decal = held.from as StackDecal;
    const centre = project(view, vector(decal.centre));
    if (!centre) {
      return;
    }

    if (handle.kind === 'scale') {
      const before = Math.max(1, Math.hypot(held.x - centre[0], held.y - centre[1]));
      const by = Math.max(Math.hypot(x - centre[0], y - centre[1]) / before, 0.01);
      this.draft.set({ layer: layer.id, decal: { ...decal, width: decal.width * by, height: decal.height * by } });
    } else if (handle.kind === 'turn') {
      // Clockwise on the screen is clockwise seen from where the decal faces, which is against its facing.
      const angle = Math.atan2(y - centre[1], x - centre[0]) - Math.atan2(held.y - centre[1], held.x - centre[0]);
      this.draft.set({ layer: layer.id, decal: turned(decal, -angle) });
    } else {
      const shown = this.decalShown() ?? decal;
      const place = onDecalPlane(shown, view, x, y);
      if (place) {
        const warp = [...warpOf(shown)];
        warp[handle.index * 2] = place[0];
        warp[handle.index * 2 + 1] = place[1];
        this.draft.set({ layer: layer.id, decal: { ...shown, warp } });
      }
    }
  }

  /** Asks what is under the newest place a hand dragged to, one question at a time. */
  private asked: Promise<void> = Promise.resolve();

  private async ask(): Promise<void> {
    if (this.asking) {
      return;
    }

    this.asking = true;
    const asked = (async () => {
      for (let at = this.take(); at; at = this.take()) {
        await this.answer(at);
      }
    })();
    this.asked = asked;
    try {
      await asked;
    } finally {
      this.asking = false;
    }
  }

  /** The newest place a hand dragged to, once: places it passed on the way are not worth a question. */
  private take(): { x: number; y: number } | null {
    const at = this.wanted;
    this.wanted = null;
    return at;
  }

  private async answer(at: { x: number; y: number }): Promise<void> {
    const held = this.held;
    const layer = this.layer();
    if (!held || !layer) {
      return;
    }

    try {
      const hit = await this.paint.pickOnMesh({ x: at.x, y: at.y, pressure: 1 }, this.aim());
      if (!hit.found) {
        return;
      }

      if (held.handle.kind === 'point') {
        const index = held.handle.index;
        const path = this.pathShown() ?? (held.from as StackSurfacePath);
        const points = path.points.map((point, at) =>
          at === index ? { ...pathPoint(hit), pressure: point.pressure } : point
        );
        this.draft.set({ layer: layer.id, path: { ...path, points } });
      } else {
        const decal = this.decalShown() ?? (held.from as StackDecal);
        this.draft.set({
          layer: layer.id,
          decal: { ...decal, centre: [...vector(hit.position)], facing: [...vector(hit.normal)] }
        });
      }
    } catch {
      // A drag off the mesh leaves the handle where it last met it.
    }
  }

  /** The hand came off: the drag is one edit, and the guide draws the Server's again once it has answered. */
  private async release(): Promise<void> {
    await this.asked;
    const held = this.held;
    this.held = null;
    const draft = this.draft();
    const layer = this.layer();
    if (held && layer && draft?.layer === layer.id) {
      try {
        await this.paint.edit(
          draft.decal
            ? { op: 'setLayer', layer: layer.id, decal: draft.decal }
            : { op: 'setLayer', layer: layer.id, path: draft.path ?? null }
        );
      } finally {
        this.draft.set(null);
      }
    } else {
      this.draft.set(null);
    }
  }

  private at(event: PointerEvent): { x: number; y: number } {
    const box = this.host.nativeElement.getBoundingClientRect();
    return { x: event.clientX - box.left, y: event.clientY - box.top };
  }

  private aim() {
    const { clientWidth, clientHeight } = this.host.nativeElement;
    return { width: clientWidth, height: clientHeight, camera: this.viewport.camera() };
  }
}
