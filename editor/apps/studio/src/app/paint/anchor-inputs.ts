import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import type { StackAnchorRef, StackFilterKind } from '@patina/api-client';
import { XuiSelectImports } from '@xui/select';
import { channelName } from '../core/labels';
import { PaintStore } from './paint-store';

/** Which anchor point feeds each image input of a mask's generator, by port. */
export type AnchorSources = Readonly<Record<string, StackAnchorRef>>;

/**
 * A slot for each image a mask's generator reads, fed by an anchor point in one channel (#111). A generator on a mask
 * reads nothing beneath it, so an anchor point is the one thing a slot takes, and a mask is one picture whichever
 * channel it lands on, so each names the channel it reads.
 */
@Component({
  selector: 'ptn-anchor-inputs',
  imports: [XuiSelectImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex flex-col gap-1' },
  template: `
    @for (slot of kind().inputs ?? []; track slot.port) {
      @let held = inputs()[slot.port];
      <div class="flex items-center gap-2" [attr.data-testid]="testId() + slot.port">
        <span class="text-foreground-muted w-16 shrink-0 truncate text-xs" [title]="slot.summary">{{ slot.port }}</span>
        <xui-select
          class="min-w-0 flex-1"
          [aria-label]="slot.port + ' reads the anchor point'"
          [attr.data-testid]="testId() + slot.port + '-anchor'"
          [filterable]="false"
          [itemText]="slot.optional ? optionalAnchor : requiredAnchor"
          [items]="anchorChoices()"
          [placeholder]="slot.optional ? 'Nothing' : 'Choose an anchor point'"
          [value]="held?.anchor ?? ''"
          (valueChange)="choose(slot.port, $event ?? '', held?.channel)"
        />
        @if (held?.anchor) {
          <xui-select
            class="w-24 min-w-0 shrink-0"
            [aria-label]="slot.port + ' anchor channel'"
            [attr.data-testid]="testId() + slot.port + '-channel'"
            [filterable]="false"
            [itemText]="channelName"
            [items]="channels()"
            [matchTargetWidth]="false"
            [value]="channelOf(held)"
            (valueChange)="choose(slot.port, held.anchor, $event ?? undefined)"
          />
        }
      </div>
    }
  `
})
export class AnchorInputs {
  private readonly store = inject(PaintStore);

  /** What the generator declares. */
  readonly kind = input.required<Pick<StackFilterKind, 'inputs'>>();
  /** Which anchor point each port reads now. */
  readonly inputs = input<AnchorSources>({});
  /** The anchor points it may read: every one but its layer's own. */
  readonly anchors = input<readonly string[]>([]);
  /** What a slot's test id starts with. */
  readonly testId = input('anchor-input-');

  /** Every input, replacing what it read before. */
  readonly inputsChange = output<AnchorSources>();

  protected readonly channelName = channelName;
  /** Nothing first, for a slot to read nothing again, then every anchor point it may read. */
  protected readonly anchorChoices = computed(() => ['', ...this.anchors()]);
  protected readonly optionalAnchor = (anchor: string) => anchor || 'Nothing';
  protected readonly requiredAnchor = (anchor: string) => anchor || 'Choose an anchor point';
  protected readonly channels = computed(() => (this.store.state()?.channels ?? []).map(channel => channel.usage));

  /** The channel a reference reads, or the one a mask reads when it names none: height when there is one, as wear is. */
  protected channelOf(held: StackAnchorRef | undefined): string {
    const channels = this.channels();
    return held?.channel && channels.includes(held.channel)
      ? held.channel
      : channels.includes('height')
        ? 'height'
        : (channels[0] ?? '');
  }

  protected choose(port: string, anchor: string, channel: string | undefined): void {
    const others = Object.entries(this.inputs()).filter(([held]) => held !== port);
    const reference = { anchor, channel: this.channelOf(channel ? { anchor, channel } : undefined) };
    this.inputsChange.emit(Object.fromEntries(anchor ? [...others, [port, reference]] : others));
  }
}
