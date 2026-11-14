import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked
} from '@angular/core';
import { type PatinaClient, unwrap } from '@patina/api-client';
import type { Commands } from '../core/commands';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';
import { LibraryThumbnails } from '../library/thumbnails';
import { type FrameStep, PaintStore, type ScreenFrame } from './paint-store';

/** How much of the view's shorter side a picture first takes. */
const FirstShare = 0.6;

/** How far a key moves the picture on a view, in pixels; how much it scales it; and how far it turns it. */
const StencilStep = 10;
const StencilScale = 1.1;
const StencilTurn = Math.PI / 36;

/** Which part of the frame a drag holds. */
type Handle = 'move' | 'scale' | 'turn';

/**
 * Lets the stencil's keys move, scale and turn what is fixed on the view (#128), a step at a time, while a stencil or
 * the projection tool reads it. The viewport and the 2D view both call this; one handler runs a key, and each does the
 * same.
 */
export function handleStencilKeys(commands: Commands, paint: PaintStore, destroyRef: DestroyRef): void {
  const shown = () => paint.screenPictureShown() || paint.materialFrameShown();
  const steps: Readonly<Record<string, FrameStep>> = {
    moveStencilLeft: { x: -StencilStep },
    moveStencilRight: { x: StencilStep },
    moveStencilUp: { y: -StencilStep },
    moveStencilDown: { y: StencilStep },
    growStencil: { scale: StencilScale },
    shrinkStencil: { scale: 1 / StencilScale },
    turnStencilLeft: { angle: -StencilTurn },
    turnStencilRight: { angle: StencilTurn }
  };
  for (const [id, step] of Object.entries(steps)) {
    commands.handle(id, { run: () => paint.moveScreenPicture(step), enabled: shown }, destroyRef);
  }
}

/**
 * What is fixed on a view (#128): the picture the projection tool paints and a stencil masks with, or the material the
 * projection tool paints. It is drawn where a stroke will read it, kept from the middle of the view, and three handles
 * move it, scale it and turn it. The picture itself takes no pointer, so a stroke over it still paints. The viewport and the 2D
 * view each draw it at the same distance from their own middle, whatever their sizes (#271).
 */
@Component({
  selector: 'ptn-screen-picture',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'pointer-events-none absolute inset-0 overflow-hidden', '[attr.data-testid]': 'testId()' },
  template: `
    @if (shown(); as frame) {
      <div
        class="absolute"
        [attr.data-testid]="testId() + '-frame'"
        [attr.data-centre]="frame.centre.join(',')"
        [attr.data-scale]="frame.scale"
        [attr.data-angle]="frame.angle"
        [style.left]="'calc(50% + ' + frame.centre[0] + 'px)'"
        [style.top]="'calc(50% + ' + frame.centre[1] + 'px)'"
        [style.width.px]="size()[0] * frame.scale"
        [style.height.px]="size()[1] * frame.scale"
        [style.transform]="'translate(-50%, -50%) rotate(' + frame.angle + 'rad)'"
      >
        @if (url(); as source) {
          <img
            class="block size-full select-none"
            alt=""
            draggable="false"
            [class.opacity-50]="paint.tool() === 'projection'"
            [class.opacity-40]="paint.tool() !== 'projection'"
            [src]="source"
          />
        } @else {
          <span class="block size-full bg-white/10"></span>
        }
        <span class="absolute inset-0 border border-dashed border-white/70 mix-blend-difference"></span>
        <button
          type="button"
          class="pointer-events-auto absolute start-1/2 top-1/2 size-4 -translate-x-1/2 -translate-y-1/2 cursor-move rounded-full border border-white bg-black/50"
          [attr.aria-label]="'Move the ' + noun()"
          [title]="'Drag to move the ' + noun()"
          [attr.data-testid]="testId() + '-move'"
          (pointerdown)="hold($event, 'move')"
        ></button>
        <button
          type="button"
          class="pointer-events-auto absolute -end-2 -bottom-2 size-4 cursor-nwse-resize rounded-sm border border-white bg-black/50"
          [attr.aria-label]="'Scale the ' + noun()"
          [title]="'Drag to scale the ' + noun()"
          [attr.data-testid]="testId() + '-scale'"
          (pointerdown)="hold($event, 'scale')"
        ></button>
        <button
          type="button"
          class="pointer-events-auto absolute start-1/2 -top-6 size-4 -translate-x-1/2 cursor-grab rounded-full border border-white bg-black/50"
          [attr.aria-label]="'Turn the ' + noun()"
          [title]="'Drag to turn the ' + noun()"
          [attr.data-testid]="testId() + '-turn'"
          (pointerdown)="hold($event, 'turn')"
        ></button>
      </div>
    }
  `
})
export class ScreenPictureOverlay {
  protected readonly paint = inject(PaintStore);
  private readonly connection = inject(SERVER_CONNECTION);
  private readonly createClient = inject(API_CLIENT_FACTORY);
  private readonly thumbnails = inject(LibraryThumbnails);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private client: PatinaClient | null = null;
  private held: { handle: Handle; from: ScreenFrame; x: number; y: number } | null = null;

  /** What this draws: the picture, or the material the projection tool paints. */
  readonly of = input<'picture' | 'material'>('picture');
  /** Which view it is drawn over, which only its test names tell apart. */
  readonly view = input<'viewport' | 'atlas'>('viewport');

  protected readonly testId = computed(
    () => (this.of() === 'material' ? 'screen-material' : 'screen-picture') + (this.view() === 'atlas' ? '-2d' : '')
  );
  protected readonly noun = computed(() => (this.of() === 'material' ? 'material' : 'picture'));

  /** The picture's own size in pixels, once it has loaded. */
  private readonly natural = signal<readonly [number, number] | null>(null);
  /** The picture's address, once it has loaded from the library. */
  private readonly picture = signal<string | null>(null);

  /** How large what is drawn is at a scale of 1: the picture's pixels, or a material's maps as large as the set's. */
  protected readonly size = computed<readonly [number, number]>(() => {
    if (this.of() === 'material') {
      const state = this.paint.state();
      return [Math.max(1, state?.width ?? 1), Math.max(1, state?.height ?? 1)];
    }

    return this.natural() ?? [1, 1];
  });

  /** What is shown inside the frame: the picture, or the material's picture from the shelf if it has one. */
  protected readonly url = computed(() => {
    if (this.of() === 'picture') {
      return this.picture();
    }

    const source = this.paint.material()?.source;
    return source ? (this.thumbnails.url(source) ?? null) : null;
  });

  /** The frame that this edits. */
  private readonly frame = computed<ScreenFrame | null>(() =>
    this.of() === 'material' ? this.paint.materialFrame() : this.paint.screenPicture()
  );

  /** The frame, while a tool reads it: the projection, or any brush through a stencil. */
  protected readonly shown = computed(() => {
    const frame = this.frame();
    const read = this.of() === 'material' ? this.paint.projectsMaterial() : this.paint.screenPictureRead();
    return frame && read && frame.scale > 0 ? frame : null;
  });

  constructor() {
    effect(() => {
      const image = this.of() === 'picture' ? (this.paint.screenPicture()?.image ?? null) : null;
      untracked(() => void this.load(image));
    });
    effect(() => {
      const source = this.of() === 'material' ? this.paint.material()?.source : undefined;
      if (source) {
        this.thumbnails.want([source]);
      }
    });
    // A frame not placed yet goes in the middle of the view at a size that fits, as soon as its size is known.
    effect(() => {
      const frame = this.frame();
      const read = this.of() === 'material' ? this.paint.projectsMaterial() : this.paint.screenPictureRead();
      const known = this.of() === 'material' || this.natural() !== null;
      if (frame && read && known && !(frame.scale > 0)) {
        untracked(() => this.place(frame));
      }
    });
    inject(DestroyRef).onDestroy(() => this.revoke());
  }

  private place(frame: ScreenFrame): void {
    const box = this.host.nativeElement.getBoundingClientRect();
    if (box.width <= 0 || box.height <= 0) {
      return;
    }

    const [width, height] = this.size();
    const fit = (Math.min(box.width, box.height) * FirstShare) / Math.max(width, height);
    this.write({ ...frame, centre: [0, 0], scale: fit, angle: 0 });
  }

  private write(frame: ScreenFrame): void {
    if (this.of() === 'material') {
      this.paint.materialFrame.set(frame);
      return;
    }

    const picture = this.paint.screenPicture();
    if (picture) {
      this.paint.screenPicture.set({ ...picture, ...frame });
    }
  }

  protected hold(event: PointerEvent, handle: Handle): void {
    const frame = this.frame();
    if (!frame || event.button !== 0) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    const target = event.target as HTMLElement;
    target.setPointerCapture(event.pointerId);
    this.held = { handle, from: frame, ...this.at(event) };
    target.onpointermove = moved => this.drag(moved);
    target.onpointerup = target.onpointercancel = () => {
      this.held = null;
      target.onpointermove = target.onpointerup = target.onpointercancel = null;
    };
  }

  private drag(event: PointerEvent): void {
    const held = this.held;
    if (!held) {
      return;
    }

    const { x, y } = this.at(event);
    const [cx, cy] = held.from.centre as [number, number];
    switch (held.handle) {
      case 'move':
        this.write({ ...held.from, centre: [cx + x - held.x, cy + y - held.y] });
        break;
      case 'scale': {
        const before = Math.max(1, Math.hypot(held.x - cx, held.y - cy));
        const scale = (held.from.scale * Math.hypot(x - cx, y - cy)) / before;
        this.write({ ...held.from, scale: Math.max(scale, 1e-3) });
        break;
      }
      case 'turn': {
        const turned = Math.atan2(y - cy, x - cx) - Math.atan2(held.y - cy, held.x - cx);
        this.write({ ...held.from, angle: held.from.angle + turned });
        break;
      }
    }
  }

  /** Where a pointer is in pixels from the middle of the view, which a frame is kept in (#271). */
  private at(event: PointerEvent): { x: number; y: number } {
    const box = this.host.nativeElement.getBoundingClientRect();
    return { x: event.clientX - box.left - box.width / 2, y: event.clientY - box.top - box.height / 2 };
  }

  private async load(image: string | null): Promise<void> {
    this.revoke();
    if (!image) {
      return;
    }

    try {
      this.client ??= this.createClient(await this.connection());
      const blob = unwrap(
        await this.client.GET('/api/v1/library/picture', { params: { query: { path: image } }, parseAs: 'blob' })
      );
      if (this.paint.screenPicture()?.image !== image) {
        return;
      }

      // The size is read before the picture is drawn, since a picture not placed yet has no size to be drawn at.
      const url = URL.createObjectURL(blob);
      const read = new Image();
      read.src = url;
      await read.decode();
      if (this.paint.screenPicture()?.image === image) {
        this.picture.set(url);
        this.natural.set([Math.max(1, read.naturalWidth), Math.max(1, read.naturalHeight)]);
      } else {
        URL.revokeObjectURL(url);
      }
    } catch {
      // A picture that will not load shows nothing; the stroke that reads it says why.
    }
  }

  private revoke(): void {
    const url = this.picture();
    if (url) {
      URL.revokeObjectURL(url);
      this.picture.set(null);
    }

    this.natural.set(null);
  }
}
