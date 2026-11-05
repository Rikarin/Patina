import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import type { GraphSettingDefinition } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiInputImports } from '@xui/input';
import { XuiNumericInputImports } from '@xui/numeric-input';
import { XuiSelectImports } from '@xui/select';
import { XuiSliderImports } from '@xui/slider';
import { XuiSwitchImports } from '@xui/switch';
import { GraphStore } from './graph-store';

const Sides = ['256', '512', '1024', '2048', '4096'];
const Kinds = ['Float', 'Int', 'Bool', 'Text'];
const SliderSteps = 1000;

/** What a kind's default starts as, which the Server has to accept. */
function startOf(kind: string): string {
  switch (kind) {
    case 'Bool':
      return 'false';
    case 'Text':
      return '';
    default:
      return '0';
  }
}

/** The first name of the form the graph does not use yet. */
export function freeParameterName(taken: readonly GraphSettingDefinition[]): string {
  const names = new Set(taken.map(parameter => parameter.name));
  let number = names.size + 1;
  while (names.has(`Parameter${String(number)}`)) {
    number++;
  }

  return `Parameter${String(number)}`;
}

/** The graph itself in the Inspector: its size and seed, what it exposes, and its frames and notes. */
@Component({
  selector: 'ptn-graph-settings',
  imports: [
    XuiButtonImports,
    XuiInputImports,
    XuiNumericInputImports,
    XuiSelectImports,
    XuiSliderImports,
    XuiSwitchImports
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block h-full overflow-auto p-3',
    '(pointerup)': 'store.endGesture()',
    '(focusout)': 'store.endGesture()'
  },
  template: `
    @let document = store.document();
    <h2 class="truncate text-sm font-semibold" data-testid="inspector-title">{{ document?.name }}</h2>
    <p class="section-label mb-3">Graph</p>

    <div class="flex flex-col gap-2.5">
      <div class="flex flex-col gap-1" data-testid="setting-baseWidth">
        <span class="text-foreground-muted text-xs">Width</span>
        <xui-select
          ariaLabel="Width"
          [disabled]="!store.editable()"
          [items]="sidesOf('baseWidth')"
          [value]="settingOf('baseWidth')"
          (valueChange)="setSide('baseWidth', $event)"
        />
      </div>
      <div class="flex flex-col gap-1" data-testid="setting-baseHeight">
        <span class="text-foreground-muted text-xs">Height</span>
        <xui-select
          ariaLabel="Height"
          [disabled]="!store.editable()"
          [items]="sidesOf('baseHeight')"
          [value]="settingOf('baseHeight')"
          (valueChange)="setSide('baseHeight', $event)"
        />
      </div>
      <div class="flex flex-col gap-1" data-testid="setting-seed">
        <span class="text-foreground-muted text-xs">Seed</span>
        <xui-numeric-input
          size="sm"
          ariaLabel="Seed"
          [disabled]="!store.editable()"
          [min]="0"
          [stepSize]="1"
          [value]="seed()"
          (valueChange)="setSeed($event)"
        />
      </div>
    </div>

    <p class="section-label mt-4 mb-1">Parameters</p>
    <div class="flex flex-col gap-3">
      @for (parameter of document?.parameters; track parameter.name) {
        <div class="well flex flex-col gap-1.5 p-2" [attr.data-testid]="'parameter-' + parameter.name">
          <div class="flex items-center gap-1">
            <input
              class="min-w-0 flex-1"
              xuiInput
              size="sm"
              aria-label="Parameter name"
              [disabled]="!store.editable()"
              [value]="parameter.name"
              (change)="rename(parameter, $event)"
            />
            <xui-select
              class="w-20 shrink-0"
              ariaLabel="Parameter kind"
              [disabled]="!store.editable()"
              [items]="kinds"
              [value]="parameter.kind"
              (valueChange)="setKind(parameter, $event)"
            />
          </div>
          @switch (controlOf(parameter)) {
            @case ('number') {
              <div class="flex items-center gap-2">
                @if (parameter.minimum !== null && parameter.minimum !== undefined && hasRange(parameter)) {
                  <xui-slider
                    class="min-w-0 flex-1"
                    labelStepSize="0"
                    [attr.aria-label]="parameter.name + ' default'"
                    [disabled]="!store.editable()"
                    [max]="parameter.maximum ?? 1"
                    [min]="parameter.minimum"
                    [stepSize]="stepOf(parameter)"
                    [value]="numberOf(parameter)"
                    (valueChange)="setNumber(parameter, $event)"
                  />
                }
                <xui-numeric-input
                  class="w-24 shrink-0"
                  size="sm"
                  [ariaLabel]="parameter.name + ' default value'"
                  [disabled]="!store.editable()"
                  [stepSize]="parameter.kind === 'Int' ? 1 : 0.1"
                  [value]="numberOf(parameter)"
                  (valueChange)="setNumber(parameter, $event)"
                />
              </div>
            }
            @case ('switch') {
              <xui-switch
                [attr.aria-label]="parameter.name + ' default'"
                [checked]="store.parameterDefault(parameter) === 'true'"
                [disabled]="!store.editable()"
                (checkedChange)="setDefault(parameter, $event ? 'true' : 'false')"
              />
            }
            @case ('choice') {
              <xui-select
                [aria-label]="parameter.name + ' default'"
                [disabled]="!store.editable()"
                [items]="parameter.accepted"
                [value]="store.parameterDefault(parameter)"
                (valueChange)="setDefault(parameter, $event ?? parameter.default)"
              />
            }
            @default {
              <input
                xuiInput
                size="sm"
                [attr.aria-label]="parameter.name + ' default'"
                [disabled]="!store.editable()"
                [value]="store.parameterDefault(parameter)"
                (change)="setDefaultText(parameter, $event)"
              />
            }
          }
          <button
            class="self-start"
            type="button"
            xuiButton
            size="sm"
            variant="ghost"
            [disabled]="!store.editable()"
            (click)="store.removeParameter(parameter.name)"
          >
            Remove
          </button>
        </div>
      } @empty {
        <p class="text-foreground-muted text-xs">This graph exposes nothing yet.</p>
      }
      <button
        class="self-start"
        type="button"
        xuiButton
        size="sm"
        data-testid="parameter-add"
        [disabled]="!store.editable()"
        (click)="addParameter()"
      >
        Add parameter
      </button>
    </div>

    @if (document?.groups?.length) {
      <p class="section-label mt-4 mb-1">Groups</p>
      <div class="flex flex-col gap-1.5">
        @for (group of document?.groups; track $index) {
          <div class="flex items-center gap-1" data-testid="group-row">
            <input
              class="min-w-0 flex-1"
              xuiInput
              size="sm"
              aria-label="Group title"
              [disabled]="!store.editable()"
              [value]="group.title"
              (change)="rename$($index, $event)"
            />
            <span class="readout text-foreground-muted shrink-0">{{ group.nodes.length }}</span>
            <button
              type="button"
              xuiButton
              size="sm"
              variant="ghost"
              [disabled]="!store.editable()"
              (click)="store.ungroup($index)"
            >
              Ungroup
            </button>
          </div>
        }
      </div>
    }
  `
})
export class GraphSettings {
  protected readonly store = inject(GraphStore);
  protected readonly kinds = Kinds;

  protected readonly seed = computed(() => Number(this.store.document()?.settings['seed'] ?? 0));

  protected settingOf(key: string): string {
    return this.store.document()?.settings[key] ?? '';
  }

  /** The usual sizes, and the one this graph has if it is something else. */
  protected sidesOf(key: string): readonly string[] {
    const held = this.settingOf(key);
    return held === '' || Sides.includes(held) ? Sides : [...Sides, held];
  }

  protected setSide(key: string, value: string | null): void {
    if (value !== null && value !== this.settingOf(key)) {
      this.store.setSetting(key, value);
    }
  }

  protected setSeed(value: number | null): void {
    if (value !== null && Number.isInteger(value) && value >= 0 && value !== this.seed()) {
      this.store.setSetting('seed', String(value));
    }
  }

  protected controlOf(parameter: GraphSettingDefinition): 'number' | 'switch' | 'choice' | 'text' {
    if (parameter.accepted.length > 0) {
      return 'choice';
    }

    if (parameter.kind === 'Bool') {
      return 'switch';
    }

    return parameter.kind === 'Float' || parameter.kind === 'Int' ? 'number' : 'text';
  }

  protected hasRange(parameter: GraphSettingDefinition): boolean {
    return parameter.maximum !== null && parameter.maximum !== undefined;
  }

  protected stepOf(parameter: GraphSettingDefinition): number {
    return parameter.kind === 'Int' ? 1 : ((parameter.maximum ?? 1) - (parameter.minimum ?? 0)) / SliderSteps;
  }

  protected numberOf(parameter: GraphSettingDefinition): number {
    const value = Number(this.store.parameterDefault(parameter));
    return Number.isFinite(value) ? value : 0;
  }

  protected setNumber(parameter: GraphSettingDefinition, value: number | null): void {
    if (value === null || !Number.isFinite(value)) {
      return;
    }

    const next = parameter.kind === 'Int' ? Math.round(value) : value;
    if (next !== this.numberOf(parameter)) {
      this.store.setParameterDefault(parameter, String(next));
    }
  }

  protected setDefault(parameter: GraphSettingDefinition, value: string): void {
    this.store.setParameterDefault(parameter, value);
    this.store.endGesture();
  }

  protected setDefaultText(parameter: GraphSettingDefinition, event: Event): void {
    this.setDefault(parameter, (event.target as HTMLInputElement).value);
  }

  protected rename(parameter: GraphSettingDefinition, event: Event): void {
    const name = (event.target as HTMLInputElement).value.trim();
    if (name !== parameter.name) {
      this.store.editParameter(parameter.name, { ...parameter, name });
    }
  }

  protected setKind(parameter: GraphSettingDefinition, kind: string | null): void {
    if (kind !== null && kind !== parameter.kind) {
      const { minimum, maximum, ...rest } = parameter;
      this.store.editParameter(parameter.name, {
        ...rest,
        ...(kind === 'Float' ? { minimum, maximum } : {}),
        kind,
        default: startOf(kind),
        accepted: []
      });
    }
  }

  protected addParameter(): void {
    this.store.addParameter({
      name: freeParameterName(this.store.document()?.parameters ?? []),
      default: '0.5',
      summary: '',
      kind: 'Float',
      minimum: 0,
      maximum: 1,
      group: '',
      accepted: []
    });
  }

  protected rename$(index: number, event: Event): void {
    this.store.renameGroup(index, (event.target as HTMLInputElement).value);
  }
}

/** A note in the Inspector. */
@Component({
  selector: 'ptn-graph-note',
  imports: [XuiButtonImports, XuiInputImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block h-full overflow-auto p-3' },
  template: `
    <h2 class="truncate text-sm font-semibold" data-testid="inspector-title">Note</h2>
    <p class="section-label mb-3">Graph</p>
    <textarea
      class="h-32 w-full"
      xuiInput
      aria-label="Note text"
      data-testid="note-text"
      [disabled]="!store.editable()"
      [value]="text()"
      (change)="retype($event)"
    ></textarea>
    <button
      class="mt-2"
      type="button"
      xuiButton
      size="sm"
      variant="ghost"
      [disabled]="!store.editable()"
      (click)="store.removeComment(index())"
    >
      Delete note
    </button>
  `
})
export class GraphNoteInspector {
  protected readonly store = inject(GraphStore);

  readonly index = input.required<number>();

  protected readonly text = computed(() => this.store.document()?.comments[this.index()]?.text ?? '');

  protected retype(event: Event): void {
    this.store.setCommentText(this.index(), (event.target as HTMLTextAreaElement).value);
  }
}

/** Several nodes in the Inspector: a single node's fields would not say which they apply to. */
@Component({
  selector: 'ptn-graph-selection',
  imports: [XuiButtonImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block h-full overflow-auto p-3' },
  template: `
    <h2 class="truncate text-sm font-semibold" data-testid="inspector-title">{{ titles().length }} nodes</h2>
    <p class="section-label mb-2">Selection</p>
    <ul class="mb-3 flex flex-col gap-0.5" data-testid="selection-list">
      @for (title of titles(); track $index) {
        <li class="truncate text-xs">{{ title }}</li>
      }
    </ul>
    <div class="flex gap-1">
      <button type="button" xuiButton size="sm" [disabled]="!store.editable()" (click)="store.groupSelection()">
        Group
      </button>
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        data-testid="selection-delete"
        [disabled]="!store.editable()"
        (click)="store.remove(nodes(), [])"
      >
        Delete
      </button>
    </div>
  `
})
export class GraphSelection {
  protected readonly store = inject(GraphStore);

  protected readonly nodes = computed(() => [...this.store.selection()]);
  protected readonly titles = computed(() => {
    const document = this.store.document();
    const types = this.store.types();
    return this.nodes().map(id => {
      const type = document?.nodes.get(id)?.type ?? '';
      return types.get(type)?.title ?? type;
    });
  });
}
