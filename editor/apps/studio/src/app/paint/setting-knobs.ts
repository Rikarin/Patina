import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import type { GraphSettingDefinition } from '@patina/api-client';
import { XuiInputImports } from '@xui/input';
import { XuiNumericInputImports } from '@xui/numeric-input';
import { XuiSelectImports } from '@xui/select';
import { XuiSliderImports } from '@xui/slider';
import { XuiSwitchImports } from '@xui/switch';
import { choiceName, termName } from '../core/labels';
import { knobRange, knobShown } from '../core/numbers';

/** A setting a knob changed to. */
export interface KnobChange {
  name: string;
  value: string;
}

/**
 * The knobs of a graph a layer or a mask runs, one per setting it publishes: a list for a choice, a slider for a range,
 * a number, a switch or a text. A fill layer, a mask's generator and a generator layer of a mask all hold their knobs as
 * text by name, so what is set and where it goes is the caller's.
 */
@Component({
  selector: 'ptn-setting-knobs',
  imports: [XuiInputImports, XuiNumericInputImports, XuiSelectImports, XuiSliderImports, XuiSwitchImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'contents' },
  template: `
    @for (parameter of parameters(); track parameter.name) {
      @let current = valueOf(parameter);
      <div class="flex items-center gap-2">
        <span class="text-foreground-muted w-16 shrink-0 truncate text-xs" [title]="parameter.summary">{{
          nameOf(parameter)
        }}</span>
        @if (parameter.accepted.length > 0) {
          <xui-select
            class="min-w-0 flex-1"
            [aria-label]="nameOf(parameter)"
            [attr.data-testid]="testId() + parameter.name"
            [filterable]="false"
            [itemText]="choicesOf(parameter)"
            [items]="parameter.accepted"
            [value]="current"
            (valueChange)="$event && set(parameter, $event)"
          />
        } @else if (bounded(parameter); as range) {
          <xui-slider
            class="min-w-0 flex-1"
            labelStepSize="0"
            [aria-label]="nameOf(parameter)"
            [attr.data-testid]="testId() + parameter.name"
            [max]="range.maximum"
            [min]="range.minimum"
            [stepSize]="range.step"
            [value]="+current"
            (valueChange)="set(parameter, $event.toString())"
          />
          <span class="readout w-12 shrink-0 text-end text-xs">{{ shown(parameter, +current) }}</span>
        } @else if (parameter.kind === 'Float' || parameter.kind === 'Int') {
          <xui-numeric-input
            class="min-w-0 flex-1"
            size="sm"
            [aria-label]="nameOf(parameter)"
            [attr.data-testid]="testId() + parameter.name"
            [stepSize]="parameter.kind === 'Int' ? 1 : step()"
            [value]="+current"
            (valueChange)="set(parameter, ($event ?? 0).toString())"
          />
        } @else if (parameter.kind === 'Bool') {
          <xui-switch
            [aria-label]="nameOf(parameter)"
            [attr.data-testid]="testId() + parameter.name"
            [checked]="current === 'true'"
            (checkedChange)="set(parameter, $event.toString())"
          />
        } @else {
          <input
            class="min-w-0 flex-1"
            xuiInput
            size="sm"
            autocomplete="off"
            [attr.aria-label]="nameOf(parameter)"
            [attr.data-testid]="testId() + parameter.name"
            [value]="current"
            (change)="set(parameter, text($event))"
          />
        }
      </div>
    }
  `
})
export class SettingKnobs {
  /** The settings the graph publishes. */
  readonly parameters = input.required<readonly GraphSettingDefinition[]>();
  /** What each is set to, by name; a setting not here shows its default. */
  readonly values = input<Readonly<Record<string, string>> | null | undefined>({});
  /** What each knob's test id starts with, its setting's name following. */
  readonly testId = input('paint-knob-');
  /** How far a number without a range moves per step. */
  readonly step = input(0.1);
  readonly knobChange = output<KnobChange>();

  protected readonly bounded = knobRange;
  protected readonly shown = knobShown;
  private readonly choiceReaders = new Map<string, (value: string) => string>();

  protected valueOf(parameter: GraphSettingDefinition): string {
    return this.values()?.[parameter.name] ?? parameter.default;
  }

  protected nameOf(parameter: GraphSettingDefinition): string {
    return parameter.label ?? termName(parameter.name);
  }

  protected choicesOf(parameter: GraphSettingDefinition): (value: string) => string {
    const key = parameter.name + '\u0000' + parameter.accepted.join('\u0000');
    let read = this.choiceReaders.get(key);
    if (!read) {
      read = value => choiceName(parameter, value);
      this.choiceReaders.set(key, read);
    }

    return read;
  }

  protected set(parameter: GraphSettingDefinition, value: string): void {
    this.knobChange.emit({ name: parameter.name, value });
  }

  protected text(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }
}
