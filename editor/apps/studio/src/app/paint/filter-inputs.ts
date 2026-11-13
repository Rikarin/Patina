import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import type { StackFilterInput, StackFilterKind, StackInput } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiSelectImports } from '@xui/select';
import { channelName, labelIn, termName, valuesOf } from '../core/labels';
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
  imports: [FilterFields, XuiButtonImports, XuiSelectImports],
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
          <xui-select
            class="min-w-0 flex-1"
            [aria-label]="slot.port + ' reads'"
            [attr.data-testid]="testId() + slot.port + '-source'"
            [filterable]="false"
            [itemText]="sourceNames().get(slot.port) ?? termName"
            [items]="sources()"
            [placeholder]="unset(slot)"
            [value]="held?.source ?? ''"
            (valueChange)="choose(slot, $event ?? '')"
          />
        </div>
        @if (held) {
          @switch (held.source) {
            @case ('Beneath') {
              <div class="flex items-center gap-2 ps-[4.5rem]">
                <xui-select
                  class="min-w-0 flex-1"
                  placeholder="This channel"
                  [aria-label]="slot.port + ' channel'"
                  [attr.data-testid]="testId() + slot.port + '-channel'"
                  [filterable]="false"
                  [itemText]="channelChoiceName"
                  [items]="channelChoices()"
                  [value]="held.channel"
                  (valueChange)="change(slot.port, { channel: $event ?? '' })"
                />
              </div>
            }
            @case ('Anchor') {
              <div class="flex items-center gap-2 ps-[4.5rem]">
                <xui-select
                  class="min-w-0 flex-1"
                  placeholder="None"
                  [aria-label]="slot.port + ' anchor point'"
                  [attr.data-testid]="testId() + slot.port + '-anchor'"
                  [filterable]="false"
                  [itemText]="anchorChoiceName"
                  [items]="anchorChoices()"
                  [value]="held.anchor?.anchor ?? ''"
                  (valueChange)="
                    change(slot.port, { anchor: { anchor: $event ?? '', channel: held.anchor?.channel ?? '' } })
                  "
                />
                <xui-select
                  class="min-w-0 flex-1"
                  placeholder="This channel"
                  [aria-label]="slot.port + ' anchor channel'"
                  [filterable]="false"
                  [itemText]="channelChoiceName"
                  [items]="channelChoices()"
                  [value]="held.anchor?.channel ?? ''"
                  (valueChange)="
                    change(slot.port, { anchor: { anchor: held.anchor?.anchor ?? '', channel: $event ?? '' } })
                  "
                />
              </div>
            }
            @case ('Generator') {
              <div class="flex items-center gap-2 ps-[4.5rem]">
                <xui-select
                  class="min-w-0 flex-1"
                  [aria-label]="slot.port + ' generator'"
                  [attr.data-testid]="testId() + slot.port + '-generator'"
                  [itemText]="generatorName"
                  [items]="generatorNodes()"
                  [matchTargetWidth]="false"
                  [value]="held.generator || null"
                  (valueChange)="$event && change(slot.port, { generator: $event, values: null, parameters: null })"
                />
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
                  [title]="'Choose the picture ' + slot.port + ' reads'"
                  (click)="pickPicture(slot.port)"
                >
                  Choose…
                </button>
              </div>
            }
            @case ('Bake') {
              <div class="flex items-center gap-2 ps-[4.5rem]">
                <xui-select
                  class="min-w-0 flex-1"
                  placeholder="None"
                  [aria-label]="slot.port + ' mesh map'"
                  [filterable]="false"
                  [itemText]="meshMapName()"
                  [items]="meshMapChoices()"
                  [value]="held.map"
                  (valueChange)="change(slot.port, { map: $event ?? '' })"
                />
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

  protected readonly termName = termName;

  /** A slot for each image, but a mask step's subject, which is the mask. */
  protected readonly slots = computed(() => (this.kind().inputs ?? []).filter(slot => !(this.mask() && slot.subject)));

  protected readonly sourceTerms = computed(() => this.store.vocabulary()?.inputs ?? []);
  protected readonly meshMaps = computed(() => this.store.vocabulary()?.meshMaps ?? []);
  protected readonly channels = computed(() => (this.store.state()?.channels ?? []).map(channel => channel.usage));

  private readonly generators = computed(
    () => new Map((this.store.vocabulary()?.sources ?? []).map(source => [source.node, source] as const))
  );

  /** Every generator, a category's together in the order the categories first come. */
  protected readonly generatorNodes = computed(() => {
    const groups = new Map<string, string[]>();
    for (const source of this.generators().values()) {
      groups.set(source.category, [...(groups.get(source.category) ?? []), source.node]);
    }

    return [...groups.values()].flat();
  });

  /** A generator by its name and category, as the Filter list names a filter. */
  protected readonly generatorName = (node: string): string => {
    const source = this.generators().get(node);
    return source ? `${source.name} · ${source.category}` : termName(node);
  };

  /** Nothing first, for a slot to read what it reads by itself again, then every source. */
  protected readonly sources = computed(() => ['', ...valuesOf(this.sourceTerms())]);
  /** What each slot's sources are called: its own words for nothing, and the Server's for the rest. */
  protected readonly sourceNames = computed(() => {
    const terms = this.sourceTerms();
    return new Map(
      this.slots().map(slot => [slot.port, (source: string) => (source ? labelIn(terms, source) : this.unset(slot))])
    );
  });

  /** This channel first, on a layer, where a picture can name none. A mask's pictures always name their channel. */
  protected readonly channelChoices = computed(() => (this.mask() ? this.channels() : ['', ...this.channels()]));
  protected readonly channelChoiceName = (channel: string): string => (channel ? channelName(channel) : 'This channel');
  protected readonly anchorChoices = computed(() => ['', ...this.anchors()]);
  protected readonly anchorChoiceName = (anchor: string): string => anchor || 'None';
  protected readonly meshMapChoices = computed(() => ['', ...valuesOf(this.meshMaps())]);
  protected readonly meshMapName = computed(() => {
    const maps = this.meshMaps();
    return (map: string) => (map ? labelIn(maps, map) : 'None');
  });

  protected generatorOf(node: string | undefined): StackFilterKind | undefined {
    return node ? this.generators().get(node) : undefined;
  }

  /**
   * What a slot reads when the layer says nothing, which is what its first choice says. A channel the set lacks is read
   * as it would stand untouched (#254), so the slot says that rather than the layers beneath.
   */
  protected unset(slot: StackFilterInput): string {
    if (slot.channel) {
      const channels = this.channels();
      return channels.length > 0 && !channels.includes(slot.channel)
        ? `Untouched · ${channelName(slot.channel)}`
        : `${labelIn(this.sourceTerms(), 'Beneath')} · ${channelName(slot.channel)}`;
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
