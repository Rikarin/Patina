import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  effect,
  inject,
  signal,
  viewChild
} from '@angular/core';
import { unwrap } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiNonIdealStateImports } from '@xui/non-ideal-state';
import { XuiSelectImports } from '@xui/select';
import { XuiSpinnerImports } from '@xui/spinner';
import { Commands } from '../core/commands';
import { channelName } from '../core/labels';
import { messageOf } from '../core/messages';
import { amount } from '../core/numbers';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';
import { ProjectStore } from '../library/project-store';
import { PaintStore, penOf, toTexels } from './paint-store';
import { ScreenPictureOverlay, handleStencilKeys } from './screen-picture';
import { StrokeGesture } from './stroke-gesture';

/** What the channel list means by the one being painted, whichever that is. */
const Painting = 'painting:';

/** The stack flat, in UV space. It paints like the mesh does, and it is the whole picture for a layered material. */
@Component({
  selector: 'ptn-paint-atlas',
  imports: [XuiButtonImports, XuiNonIdealStateImports, XuiSelectImports, XuiSpinnerImports, ScreenPictureOverlay],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'relative flex h-full min-h-0 items-center justify-center overflow-hidden p-2 [container-type:size]',
    'data-commands': 'atlas'
  },
  template: `
    @if (store.state()) {
      <xui-select
        class="absolute top-3 left-3 z-10 min-w-0"
        aria-label="Channel to look at"
        data-testid="paint-view-channel"
        [filterable]="false"
        [itemText]="lookName"
        [items]="looks()"
        [matchTargetWidth]="false"
        [value]="store.viewed() ?? paintingChannel"
        (valueChange)="store.viewChannel($event === paintingChannel ? null : $event)"
      />
    }
    @if (store.picture()) {
      <div class="absolute top-3 right-3 z-10 flex gap-1">
        @if (tiling()) {
          <button
            type="button"
            xuiButton
            size="sm"
            data-testid="paint-tile"
            [attr.aria-pressed]="tiled()"
            (click)="tiled.set(!tiled())"
          >
            {{ tiled() ? 'One' : 'Tile' }}
          </button>
        }
        @if (hasMesh()) {
          <button
            type="button"
            xuiButton
            size="sm"
            data-testid="paint-uv-wire"
            [attr.aria-pressed]="wired()"
            [title]="wireFailure() ?? 'Draws the texture set’s UV layout over the map'"
            (click)="wired.set(!wired())"
          >
            UV
          </button>
        }
        <button
          type="button"
          xuiButton
          size="sm"
          data-testid="paint-fit"
          title="Fits the whole map in the view again"
          [disabled]="scale() === 1 && shift()[0] === 0 && shift()[1] === 0"
          (click)="fit()"
        >
          Fit
        </button>
      </div>
    }
    @if (store.picture(); as picture) {
      @if (tiling() && tiled()) {
        <div
          class="well"
          data-testid="paint-tiled"
          [style.aspect-ratio]="aspect()"
          [style.width]="wellWidth()"
          [style.background-image]="'url(' + picture + ')'"
          [style.background-size]="'33.3333% 33.3333%'"
        ></div>
      } @else {
        <div
          #well
          class="well relative touch-none overflow-hidden"
          tabindex="0"
          aria-label="The map to paint on"
          data-testid="paint-canvas"
          [style.aspect-ratio]="aspect()"
          [style.width]="wellWidth()"
          [style.transform]="transform()"
          (wheel)="wheel($event)"
        >
          <img
            class="size-full select-none"
            [class.cursor-crosshair]="store.canPaint() || store.picking() !== null"
            alt="The composited map"
            draggable="false"
            data-testid="paint-picture"
            [src]="picture"
            (pointerdown)="down($event)"
            (pointermove)="move($event)"
            (pointerup)="up()"
            (pointercancel)="up()"
            (pointerleave)="under.set(null)"
            (dblclick)="gesture.finish()"
          />
          @if (wired() && wire(); as layout) {
            <img
              class="pointer-events-none absolute inset-0 size-full select-none"
              alt="The UV layout"
              draggable="false"
              data-testid="paint-uv-layout"
              [src]="layout"
            />
          }
          <span
            class="readout text-foreground pointer-events-none absolute top-0.5 left-1 [text-shadow:0_0_3px_black,0_0_1px_black]"
            [style.font-size.px]="11 / scale()"
            aria-hidden="true"
            >0</span
          >
          <span
            class="readout text-foreground pointer-events-none absolute top-0.5 right-1 [text-shadow:0_0_3px_black,0_0_1px_black]"
            [style.font-size.px]="11 / scale()"
            aria-hidden="true"
            >U 1</span
          >
          <span
            class="readout text-foreground pointer-events-none absolute bottom-0.5 left-1 [text-shadow:0_0_3px_black,0_0_1px_black]"
            [style.font-size.px]="11 / scale()"
            aria-hidden="true"
            >V 1 · 1001</span
          >
          @if (gesture.outlining() && gesture.points().length > 1) {
            <svg
              class="pointer-events-none absolute inset-0 size-full"
              data-testid="atlas-outline"
              preserveAspectRatio="none"
              [attr.viewBox]="viewBox()"
            >
              <polygon
                class="fill-white/10 stroke-white/80"
                stroke-dasharray="4 3"
                vector-effect="non-scaling-stroke"
                [attr.points]="outline()"
              />
            </svg>
          }
        </div>
        <ptn-screen-picture of="material" view="atlas" />
        <ptn-screen-picture view="atlas" />
        @if (under(); as at) {
          <span
            class="readout chrome-bar absolute bottom-3 left-3 z-10 rounded px-1.5 py-0.5 text-xs"
            data-testid="paint-under"
            >u {{ at.u }} v {{ at.v }} · {{ at.x }}, {{ at.y }}</span
          >
        }
      }
    } @else if (!store.state()) {
      <xui-non-ideal-state
        class="flex-1"
        title="No document open"
        description="This shows a texturing document or a layered material flat, as its maps lie, to paint on. Open one from the Library, or make a project."
        data-testid="atlas-empty"
      >
        <button
          action
          type="button"
          xuiButton
          size="sm"
          [disabled]="!commands.enabled('newProject')"
          (click)="commands.run('newProject')"
        >
          New Project…
        </button>
      </xui-non-ideal-state>
    } @else if (store.failure(); as reason) {
      <xui-non-ideal-state
        class="flex-1"
        title="The map could not be made"
        [description]="reason"
        data-testid="atlas-empty"
      />
    } @else {
      <xui-non-ideal-state
        class="flex-1"
        title="Making the map"
        description="The layers are being worked out on the GPU. The map shows here as soon as it is ready."
        data-testid="atlas-empty"
      >
        <xui-spinner action size="md" aria-label="Making the map" />
      </xui-non-ideal-state>
    }
  `
})
export class PaintAtlas {
  protected readonly store = inject(PaintStore);
  protected readonly gesture = new StrokeGesture(this.store, () => undefined);
  /** The picture's box in texels, which a fill's outline is drawn in. */
  protected readonly viewBox = computed(() => `0 0 ${this.side()[0]} ${this.side()[1]}`);
  /** A fill's rectangle or lasso as it is dragged, in texels. */
  protected readonly outline = computed(() =>
    this.gesture
      .points()
      .map(point => `${point.x},${point.y}`)
      .join(' ')
  );
  protected readonly commands = inject(Commands);

  constructor() {
    const destroyRef = inject(DestroyRef);
    destroyRef.onDestroy(() => this.drop());
    // What is fixed on the view moves by keys here too, with the map focused (#128).
    handleStencilKeys(this.commands, this.store, destroyRef);
    // The layout follows the stack and its mesh while it is drawn, and is let go of when it is not.
    effect(() => {
      const state = this.store.state();
      const key = this.wired() && state && state.model !== '' ? `${state.id}\n${state.model}` : null;
      if (key === this.wiredFor) {
        return;
      }

      this.wiredFor = key;
      this.drop();
      if (key !== null && state) {
        void this.fetchWire(state.id, key);
      }
    });
    this.commands.handle(
      'cancelStroke',
      {
        enabled: () => this.gesture.points().length > 0 || this.store.picking() !== null,
        run: () => this.gesture.cancel()
      },
      destroyRef
    );
    this.commands.handle(
      'finishPath',
      {
        enabled: () => this.store.tool() === 'path' && this.gesture.points().length > 1,
        run: () => this.gesture.finish()
      },
      destroyRef
    );
  }
  private readonly project = inject(ProjectStore);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly well = viewChild<ElementRef<HTMLElement>>('well');

  /** A layered material has to meet itself at its edges, which only shows when it repeats. */
  protected readonly tiling = computed(() => this.project.active()?.kind === 'layered-material');
  protected readonly tiled = signal(false);

  private readonly side = computed<readonly [number, number]>(() => {
    const state = this.store.state();
    return [state?.width ?? 1, state?.height ?? 1];
  });

  /** The well's shape is the set's, which need not be square (#246). */
  protected readonly aspect = computed(() => `${this.side()[0]} / ${this.side()[1]}`);
  /** As wide as the pane allows while the whole of the set's height still fits in it. */
  protected readonly wellWidth = computed(() => {
    const [width, height] = this.side();
    return `min(100cqw, ${(100 * width) / height}cqh)`;
  });

  private painting = false;

  /** The picture's own zoom and pan, which leave where a stamp lands alone: a stamp reads the box as drawn. */
  protected readonly scale = signal(1);
  protected readonly shift = signal<readonly [number, number]>([0, 0]);

  /** Where the pointer is on the map: its UV, v down as the map lies, and its texel. */
  protected readonly under = signal<{ u: string; v: string; x: number; y: number } | null>(null);

  private readonly connection = inject(SERVER_CONNECTION);
  private readonly createClient = inject(API_CLIENT_FACTORY);

  /** Whether the set is on a mesh, so it has a UV layout to draw. */
  protected readonly hasMesh = computed(() => (this.store.state()?.model ?? '') !== '');
  /** Whether the UV layout is drawn over the map (#137). */
  protected readonly wired = signal(false);
  /** The layout as an SVG the Server sent, for the stack it was asked for. */
  protected readonly wire = signal<string | null>(null);
  protected readonly wireFailure = signal<string | null>(null);
  private wiredFor: string | null = null;
  private panning = false;
  /** What the 2D view can show: the channel being painted, or any other to look at. */
  protected readonly paintingChannel = Painting;
  protected readonly looks = computed(() => [
    Painting,
    ...(this.store.state()?.channels.map(channel => channel.usage) ?? [])
  ]);
  protected readonly lookName = (usage: string) => (usage === Painting ? 'Painting channel' : channelName(usage));
  protected readonly transform = computed(() => {
    const [x, y] = this.shift();
    return `translate(${x}px, ${y}px) scale(${this.scale()})`;
  });

  /** Shows the whole map again, as it opened. */
  protected fit(): void {
    this.scale.set(1);
    this.shift.set([0, 0]);
  }

  /** Asks for the set's layout when it is wanted and not already here: a stack on another mesh asks again. */
  private async fetchWire(id: string, key: string): Promise<void> {
    try {
      const client = this.createClient(await this.connection());
      const blob = unwrap(
        await client.GET('/api/v1/stacks/{id}/uv-wire', { params: { path: { id } }, parseAs: 'blob' })
      );
      if (this.wiredFor === key) {
        this.drop();
        this.wire.set(URL.createObjectURL(blob));
        this.wireFailure.set(null);
      }
    } catch (error) {
      this.wireFailure.set(messageOf(error));
    }
  }

  private drop(): void {
    const old = this.wire();
    if (old) {
      URL.revokeObjectURL(old);
    }

    this.wire.set(null);
  }

  protected wheel(event: WheelEvent): void {
    event.preventDefault();
    this.scale.update(scale => Math.max(0.25, Math.min(16, scale * Math.exp(-event.deltaY * 0.001))));
  }

  protected down(event: PointerEvent): void {
    // Focus marks the 2D view as where keys go, as the viewport's does for it.
    this.well()?.nativeElement.focus({ preventScroll: true });
    if (this.commands.navigation().pans2d(event)) {
      (event.target as HTMLElement).setPointerCapture(event.pointerId);
      this.panning = true;
      return;
    }

    if (this.store.finding()) {
      const box = (event.target as HTMLElement).getBoundingClientRect();
      void this.store.findLayerAt((event.clientX - box.left) / box.width, (event.clientY - box.top) / box.height);
      return;
    }

    if (this.store.tool() === 'picker') {
      const box = (event.target as HTMLElement).getBoundingClientRect();
      void this.store.pickBrushColour((event.clientX - box.left) / box.width, (event.clientY - box.top) / box.height);
      return;
    }

    if (this.store.picking() !== null) {
      const box = (event.target as HTMLElement).getBoundingClientRect();
      void this.store.pickColourAt((event.clientX - box.left) / box.width, (event.clientY - box.top) / box.height);
      return;
    }

    // The clone's source, from Set Clone Source or a Cmd- or Ctrl-click with the clone in hand.
    if (
      this.store.settingCloneSource() ||
      (this.store.tool() === 'clone' && (event.metaKey || event.ctrlKey) && !event.shiftKey)
    ) {
      void this.store.setCloneSource(this.stamp(event));
      return;
    }

    if (!this.store.canPaint()) {
      return;
    }

    (event.target as HTMLElement).setPointerCapture(event.pointerId);
    this.painting = true;
    const box = (event.target as HTMLElement).getBoundingClientRect();
    const view = this.host.nativeElement.getBoundingClientRect();
    this.store.atlasScale.set(this.side()[0] / Math.max(box.width, 1));
    this.store.atlasOrigin.set([box.left - view.left, box.top - view.top]);
    this.gesture.press(this.stamp(event), { line: event.shiftKey, snap: event.ctrlKey || event.metaKey });
  }

  protected move(event: PointerEvent): void {
    const box = (event.target as HTMLElement).getBoundingClientRect();
    const [width, height] = this.side();
    const [u, v] = [(event.clientX - box.left) / box.width, (event.clientY - box.top) / box.height];
    this.under.set({
      u: amount(u, 3),
      v: amount(v, 3),
      x: Math.min(width - 1, Math.max(0, Math.floor(u * width))),
      y: Math.min(height - 1, Math.max(0, Math.floor(v * height)))
    });
    if (this.panning) {
      this.shift.update(([x, y]) => [x + event.movementX, y + event.movementY]);
    } else if (this.painting) {
      this.gesture.move(this.stamp(event));
    }
  }

  protected up(): void {
    this.panning = false;
    if (this.painting) {
      this.painting = false;
      this.gesture.release();
    }
  }

  private stamp(event: PointerEvent) {
    const box = (event.target as HTMLElement).getBoundingClientRect();
    return {
      ...toTexels(event.clientX - box.left, event.clientY - box.top, box.width, box.height, this.side()),
      ...penOf(event)
    };
  }
}
