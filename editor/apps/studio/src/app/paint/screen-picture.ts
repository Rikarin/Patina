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
import { type PatinaClient, unwrap } from '@patina/api-client';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';
import { PaintStore, type ScreenPicture } from './paint-store';

/** How much of the viewport's shorter side a picture first takes. */
const FirstShare = 0.6;

/** Which part of the frame a drag holds. */
type Handle = 'move' | 'scale' | 'turn';

/**
 * The picture fixed on the viewport (#128): what the projection tool paints through the brush, and what a stencil masks
 * the brush with. It is drawn where a stroke will read it, in the viewport's own units, and three handles move it,
 * scale it and turn it. The picture itself takes no pointer, so a stroke over it still paints.
 */
@Component({
  selector: 'ptn-screen-picture',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'pointer-events-none absolute inset-0 overflow-hidden', 'data-testid': 'screen-picture' },
  template: `
    @if (shown(); as picture) {
      @if (url(); as source) {
        <div
          class="absolute"
          data-testid="screen-picture-frame"
          [attr.data-centre]="picture.centre.join(',')"
          [attr.data-scale]="picture.scale"
          [attr.data-angle]="picture.angle"
          [style.left.px]="picture.centre[0]"
          [style.top.px]="picture.centre[1]"
          [style.width.px]="size()[0] * picture.scale"
          [style.height.px]="size()[1] * picture.scale"
          [style.transform]="'translate(-50%, -50%) rotate(' + picture.angle + 'rad)'"
        >
          <img
            class="block size-full select-none"
            alt=""
            draggable="false"
            [class.opacity-50]="paint.tool() === 'projection'"
            [class.opacity-40]="paint.tool() !== 'projection'"
            [src]="source"
            (load)="loaded($event)"
          />
          <span class="absolute inset-0 border border-dashed border-white/70 mix-blend-difference"></span>
          <button
            type="button"
            class="pointer-events-auto absolute start-1/2 top-1/2 size-4 -translate-x-1/2 -translate-y-1/2 cursor-move rounded-full border border-white bg-black/50"
            aria-label="Move the picture"
            title="Drag to move the picture"
            data-testid="screen-picture-move"
            (pointerdown)="hold($event, 'move')"
          ></button>
          <button
            type="button"
            class="pointer-events-auto absolute -end-2 -bottom-2 size-4 cursor-nwse-resize rounded-sm border border-white bg-black/50"
            aria-label="Scale the picture"
            title="Drag to scale the picture"
            data-testid="screen-picture-scale"
            (pointerdown)="hold($event, 'scale')"
          ></button>
          <button
            type="button"
            class="pointer-events-auto absolute start-1/2 -top-6 size-4 -translate-x-1/2 cursor-grab rounded-full border border-white bg-black/50"
            aria-label="Turn the picture"
            title="Drag to turn the picture"
            data-testid="screen-picture-turn"
            (pointerdown)="hold($event, 'turn')"
          ></button>
        </div>
      }
    }
  `
})
export class ScreenPictureOverlay {
  protected readonly paint = inject(PaintStore);
  private readonly connection = inject(SERVER_CONNECTION);
  private readonly createClient = inject(API_CLIENT_FACTORY);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private client: PatinaClient | null = null;
  private held: { handle: Handle; from: ScreenPicture; x: number; y: number } | null = null;

  /** The picture's own size in pixels, once it has loaded. */
  protected readonly size = signal<readonly [number, number]>([1, 1]);
  protected readonly url = signal<string | null>(null);

  /** The picture, while a tool reads it: the projection, or any brush through a stencil. */
  protected readonly shown = computed(() => {
    const picture = this.paint.screenPicture();
    return picture && (this.paint.tool() === 'projection' || this.paint.stencil()) ? picture : null;
  });

  constructor() {
    effect(() => {
      const image = this.paint.screenPicture()?.image ?? null;
      untracked(() => void this.load(image));
    });
    inject(DestroyRef).onDestroy(() => this.revoke());
  }

  /** Takes the picture's size, and places one not placed yet in the middle of the view at a size that fits. */
  protected loaded(event: Event): void {
    const image = event.target as HTMLImageElement;
    const [width, height] = [Math.max(1, image.naturalWidth), Math.max(1, image.naturalHeight)];
    this.size.set([width, height]);
    const picture = this.paint.screenPicture();
    if (picture && !(picture.scale > 0)) {
      const box = this.host.nativeElement.getBoundingClientRect();
      const fit = (Math.min(box.width, box.height) * FirstShare) / Math.max(width, height);
      this.paint.screenPicture.set({ ...picture, centre: [box.width / 2, box.height / 2], scale: fit, angle: 0 });
    }
  }

  protected hold(event: PointerEvent, handle: Handle): void {
    const picture = this.paint.screenPicture();
    if (!picture || event.button !== 0) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    const target = event.target as HTMLElement;
    target.setPointerCapture(event.pointerId);
    this.held = { handle, from: picture, ...this.at(event) };
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
        this.paint.screenPicture.set({ ...held.from, centre: [cx + x - held.x, cy + y - held.y] });
        break;
      case 'scale': {
        const before = Math.max(1, Math.hypot(held.x - cx, held.y - cy));
        const scale = (held.from.scale * Math.hypot(x - cx, y - cy)) / before;
        this.paint.screenPicture.set({ ...held.from, scale: Math.max(scale, 1e-3) });
        break;
      }
      case 'turn': {
        const turned = Math.atan2(y - cy, x - cx) - Math.atan2(held.y - cy, held.x - cx);
        this.paint.screenPicture.set({ ...held.from, angle: held.from.angle + turned });
        break;
      }
    }
  }

  /** Where a pointer is in the viewport's own units, which a stroke's picture is in. */
  private at(event: PointerEvent): { x: number; y: number } {
    const box = this.host.nativeElement.getBoundingClientRect();
    return { x: event.clientX - box.left, y: event.clientY - box.top };
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
      if (this.paint.screenPicture()?.image === image) {
        this.url.set(URL.createObjectURL(blob));
      }
    } catch {
      // A picture that will not load shows nothing; the stroke that reads it says why.
    }
  }

  private revoke(): void {
    const url = this.url();
    if (url) {
      URL.revokeObjectURL(url);
      this.url.set(null);
    }
  }
}
