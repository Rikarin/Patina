import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import type { StackFilterInput, StackFilterKind, StackInput } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { channelName, labelIn } from '../core/labels';
import { ProjectStore } from '../library/project-store';
import { FilterFields } from './filter-fields';
import { PaintStore } from './paint-store';

/** Where a filter's image inputs read, by port. */
export type FilterSources = Readonly<Record<string, StackInput>>;

/** The generator a slot starts from when a person picks Generator: a soft noise, which most fields want. */
const FirstGenerator = 'Noise/Clouds';

/**
 * A slot for each image a filter reads (ADR 0017): the layers beneath in a channel, an anchor point, a generator with
 * its own fields, an image or a mesh map. The subject reads the layers beneath until it is told otherwise, and on a
 * mask it is the mask and has no slot. What the slots hold, and where a change goes, is the caller's.
 */
@Component({
  selector: 'ptn-filter-inputs',
  imports: [FilterFields, XuiButtonImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex flex-col gap-1.5' },
  template: `
    @for (slot of slots(); track slot.port) {
      @let held = inputs()[slot.port];
      <div class="flex flex-col gap-1" [attr.data-testid]="testId() + slot.port">
        <div class="flex items-center gap-2">
          <span class="text-foreground-muted w-16 shrink-0 truncate text-xs" [title]="slot.summary">{{
            slot.port
          }}</span>
          <select
            class="well h-7 min-w-0 flex-1 px-1 text-sm"
            [attr.aria-label]="slot.port + ' reads'"
            [attr.data-testid]="testId() + slot.port + '-source'"
            (change)="choose(slot, text($event))"
          >
            <option value="" [selected]="!held">{{ unset(slot) }}</option>
            @for (term of sourceTerms(); track term.value) {
              <option [value]="term.value" [selected]="held?.source === term.value">{{ term.label }}</option>
            }
          </select>
        </div>
        @if (held) {
          @switch (held.source) {
            @case ('Beneath') {
              <div class="flex items-center gap-2 ps-[4.5rem]">
                <select
                  class="well h-7 min-w-0 flex-1 px-1 text-sm"
                  [attr.aria-label]="slot.port + ' channel'"
                  [attr.data-testid]="testId() + slot.port + '-channel'"
                  (change)="change(slot.port, { channel: text($event) })"
                >
                  @if (!mask()) {
                    <option value="" [selected]="!held.channel">This channel</option>
                  }
                  @for (channel of channels(); track channel) {
                    <option [value]="channel" [selected]="channel === held.channel">{{ channelName(channel) }}</option>
                  }
                </select>
              </div>
            }
            @case ('Anchor') {
              <div class="flex items-center gap-2 ps-[4.5rem]">
                <select
                  class="well h-7 min-w-0 flex-1 px-1 text-sm"
                  [attr.aria-label]="slot.port + ' anchor point'"
                  [attr.data-testid]="testId() + slot.port + '-anchor'"
                  (change)="
                    change(slot.port, { anchor: { anchor: text($event), channel: held.anchor?.channel ?? '' } })
                  "
                >
                  <option value="" [selected]="!held.anchor?.anchor">None</option>
                  @for (anchor of anchors(); track anchor) {
                    <option [value]="anchor" [selected]="anchor === held.anchor?.anchor">{{ anchor }}</option>
                  }
                </select>
                <select
                  class="well h-7 min-w-0 flex-1 px-1 text-sm"
                  [attr.aria-label]="slot.port + ' anchor channel'"
                  (change)="change(slot.port, { anchor: { anchor: held.anchor?.anchor ?? '', channel: text($event) } })"
                >
                  @if (!mask()) {
                    <option value="" [selected]="!held.anchor?.channel">This channel</option>
                  }
                  @for (channel of channels(); track channel) {
                    <option [value]="channel" [selected]="channel === held.anchor?.channel">
                      {{ channelName(channel) }}
                    </option>
                  }
                </select>
              </div>
            }
            @case ('Generator') {
              <div class="flex items-center gap-2 ps-[4.5rem]">
                <select
                  class="well h-7 min-w-0 flex-1 px-1 text-sm"
                  [attr.aria-label]="slot.port + ' generator'"
                  [attr.data-testid]="testId() + slot.port + '-generator'"
                  (change)="change(slot.port, { generator: text($event), values: null, parameters: null })"
                >
                  @for (group of generatorGroups(); track group.category) {
                    <optgroup [label]="group.category">
                      @for (source of group.sources; track source.node) {
                        <option [value]="source.node" [selected]="source.node === held.generator">
                          {{ source.name }}
                        </option>
                      }
                    </optgroup>
                  }
                </select>
              </div>
              @if (generatorOf(held.generator); as generator) {
                <ptn-filter-fields
                  class="ps-[4.5rem]"
                  [kind]="generator"
                  [numbers]="held.values ?? {}"
                  [testId]="testId() + slot.port + '-'"
                  [texts]="held.parameters ?? {}"
                  (numberChange)="setValue(slot.port, $event.name, $event.value)"
                  (textChange)="setText(slot.port, $event.name, $event.value)"
                />
              }
            }
            @case ('Texture') {
              <div class="flex items-center gap-2 ps-[4.5rem]">
                <span class="min-w-0 flex-1 truncate text-xs" [title]="held.asset">{{
                  fileOf(held.asset) || 'None'
                }}</span>
                <button
                  type="button"
                  xuiButton
                  size="sm"
                  variant="outline"
                  [attr.aria-label]="'Choose the picture ' + slot.port + ' reads'"
                  (click)="pickPicture(slot.port)"
                >
                  Choose…
                </button>
              </div>
            }
            @case ('Bake') {
              <div class="flex items-center gap-2 ps-[4.5rem]">
                <select
                  class="well h-7 min-w-0 flex-1 px-1 text-sm"
                  [attr.aria-label]="slot.port + ' mesh map'"
                  (change)="change(slot.port, { map: text($event) })"
                >
                  <option value="" [selected]="!held.map">None</option>
                  @for (map of meshMaps(); track map.value) {
                    <option [value]="map.value" [selected]="map.value === held.map">{{ map.label }}</option>
                  }
                </select>
              </div>
            }
          }
        }
      </div>
    }
  `
})
export class FilterInputs {
  private readonly store = inject(PaintStore);
  private readonly project = inject(ProjectStore);

  /** What the filter's node declares. */
  readonly kind = input.required<Pick<StackFilterKind, 'inputs'>>();
  /** Where its inputs read now, by port. What is not here reads what the slot says it reads by itself. */
  readonly inputs = input<FilterSources>({});
  /** Whether this is a step on a mask, whose subject is the mask and whose pictures name their channel. */
  readonly mask = input(false);
  /** The anchor points it may read: every one but the layer's own. */
  readonly anchors = input<readonly string[]>([]);
  /** What a slot's test id starts with. */
  readonly testId = input('filter-input-');

  /** Every input, replacing what it read before. */
  readonly inputsChange = output<FilterSources>();

  protected readonly channelName = channelName;

  /** A slot for each image, but a mask step's subject, which is the mask. */
  protected readonly slots = computed(() => (this.kind().inputs ?? []).filter(slot => !(this.mask() && slot.subject)));

  protected readonly sourceTerms = computed(() => this.store.vocabulary()?.inputs ?? []);
  protected readonly meshMaps = computed(() => this.store.vocabulary()?.meshMaps ?? []);
  protected readonly channels = computed(() => (this.store.state()?.channels ?? []).map(channel => channel.usage));

  private readonly generators = computed(
    () => new Map((this.store.vocabulary()?.sources ?? []).map(source => [source.node, source] as const))
  );

  protected readonly generatorGroups = computed(() => {
    const groups = new Map<string, StackFilterKind[]>();
    for (const source of this.generators().values()) {
      groups.set(source.category, [...(groups.get(source.category) ?? []), source]);
    }

    return [...groups].map(([category, sources]) => ({ category, sources }));
  });

  protected generatorOf(node: string | undefined): StackFilterKind | undefined {
    return node ? this.generators().get(node) : undefined;
  }

  /** What a slot reads when the layer says nothing, which is what its first choice says. */
  protected unset(slot: StackFilterInput): string {
    if (slot.channel) {
      return `${labelIn(this.sourceTerms(), 'Beneath')} · ${channelName(slot.channel)}`;
    }

    if (slot.subject) {
      return `${labelIn(this.sourceTerms(), 'Beneath')} · this channel`;
    }

    return slot.optional ? 'Nothing' : 'Choose what it reads';
  }

  protected choose(slot: StackFilterInput, source: string): void {
    const others = Object.entries(this.inputs()).filter(([port]) => port !== slot.port);
    this.inputsChange.emit(Object.fromEntries(source ? [...others, [slot.port, this.started(slot, source)]] : others));
  }

  protected change(port: string, change: Partial<StackInput>): void {
    const held = this.inputs()[port];
    if (held) {
      this.inputsChange.emit({ ...this.inputs(), [port]: { ...held, ...change } });
    }
  }

  protected setValue(port: string, name: string, value: readonly number[]): void {
    this.change(port, { values: { ...this.inputs()[port]?.values, [name]: [...value] } });
  }

  protected setText(port: string, name: string, value: string): void {
    this.change(port, { parameters: { ...this.inputs()[port]?.parameters, [name]: value } });
  }

  protected async pickPicture(port: string): Promise<void> {
    const picture = await this.project.pickPicture();
    if (picture) {
      this.change(port, { asset: picture });
    }
  }

  protected text(event: Event): string {
    return (event.target as HTMLSelectElement).value;
  }

  protected fileOf(path: string | undefined | null): string {
    return path?.split('/').pop() ?? '';
  }

  /** A new source, from where a person would start: the slot's own channel, or a noise. */
  private started(slot: StackFilterInput, source: string): StackInput {
    const blank: StackInput = { source, channel: '', generator: '', asset: '', map: '' };
    const first = this.mask() ? (this.channels()[0] ?? '') : '';
    switch (source) {
      case 'Beneath':
        return { ...blank, channel: slot.channel || first };
      case 'Anchor':
        return { ...blank, anchor: { anchor: this.anchors()[0] ?? '', channel: first } };
      case 'Generator':
        return {
          ...blank,
          generator: this.generators().has(FirstGenerator) ? FirstGenerator : ([...this.generators().keys()][0] ?? '')
        };
      default:
        return blank;
    }
  }
}
