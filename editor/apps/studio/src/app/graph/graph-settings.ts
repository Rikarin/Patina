import { ChangeDetectionStrategy, Component, computed, inject, input, linkedSignal } from '@angular/core';
import type { GraphInterfacePort, GraphSettingDefinition } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiInputImports } from '@xui/input';
import { XuiNumericInputImports } from '@xui/numeric-input';
import { XuiSelectImports } from '@xui/select';
import { XuiSliderImports } from '@xui/slider';
import { XuiSwitchImports } from '@xui/switch';
import { SystemStatus } from '../core/system-status';
import { ProjectStore } from '../library/project-store';
import { GraphStore } from './graph-store';

const Sides = ['256', '512', '1024', '2048', '4096'];
const Kinds = ['Float', 'Int', 'Bool', 'Text'];
const BitDepths = ['16', '32'];
const PortKinds = ['Image', 'Float', 'Int', 'Bool', 'Float2', 'Float3', 'Float4'];
const DefaultBitDepth = '16';
const NoBake = '';
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

/** The first port name of the form the graph does not use yet, on one side. */
export function freePortName(taken: readonly GraphInterfacePort[], direction: string): string {
  const side = direction === 'Input' ? 'Input' : 'Output';
  const names = new Set(taken.filter(port => port.direction === direction).map(port => port.name));
  let number = names.size + 1;
  while (names.has(`${side}${String(number)}`)) {
    number++;
  }

  return `${side}${String(number)}`;
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
      <div class="flex flex-col gap-1" data-testid="setting-bitDepth">
        <span class="text-foreground-muted text-xs">Bit depth</span>
        <xui-select
          ariaLabel="Bit depth"
          [disabled]="!store.editable()"
          [items]="bitDepths"
          [value]="bitDepth()"
          (valueChange)="setBitDepth($event)"
        />
      </div>
      @if (bakes().length > 0) {
        <div class="flex flex-col gap-1" data-testid="setting-bake">
          <span class="text-foreground-muted text-xs">Bake</span>
          <xui-select
            ariaLabel="Bake"
            [disabled]="!store.editable()"
            [itemText]="bakeName"
            [items]="bakes()"
            [value]="bake()"
            (valueChange)="chooseBake($event)"
          />
        </div>
      }
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
                [ariaLabel]="parameter.name + ' default'"
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

    @if (isCompound()) {
      <p class="section-label mt-4 mb-1">Interface</p>
      <div class="flex flex-col gap-1.5">
        @for (port of document?.interface; track port.direction + '/' + port.name) {
          <div class="well flex flex-col gap-1.5 p-2" [attr.data-testid]="'port-' + port.name">
            <div class="flex items-center gap-1">
              <span class="readout text-foreground-muted w-8 shrink-0">{{
                port.direction === 'Input' ? 'In' : 'Out'
              }}</span>
              <input
                class="min-w-0 flex-1"
                xuiInput
                size="sm"
                aria-label="Port name"
                [disabled]="!store.editable()"
                [value]="port.name"
                (change)="renamePort(port, $event)"
              />
            </div>
            <div class="flex items-center gap-1">
              <xui-select
                class="min-w-0 flex-1"
                ariaLabel="Port kind"
                [disabled]="!store.editable()"
                [items]="portKinds"
                [value]="port.kind"
                (valueChange)="setPortKind(port, $event)"
              />
              <button
                class="shrink-0"
                type="button"
                xuiButton
                size="sm"
                variant="ghost"
                [disabled]="!store.editable()"
                (click)="store.removePort(port.name, port.direction)"
              >
                Remove
              </button>
            </div>
          </div>
        } @empty {
          <p class="text-foreground-muted text-xs">This compound has no ports yet.</p>
        }
        <div class="flex gap-1">
          <button
            type="button"
            xuiButton
            size="sm"
            data-testid="port-add-input"
            [disabled]="!store.editable()"
            (click)="addPort('Input')"
          >
            Add input
          </button>
          <button
            type="button"
            xuiButton
            size="sm"
            data-testid="port-add-output"
            [disabled]="!store.editable()"
            (click)="addPort('Output')"
          >
            Add output
          </button>
        </div>
      </div>
    }

    @if (document?.groups?.length) {
      <p class="section-label mt-4 mb-1">Groups</p>
      <div class="flex flex-col gap-1.5">
        @for (group of document?.groups; track group.id) {
          <div class="flex items-center gap-1" data-testid="group-row">
            <input
              class="min-w-0 flex-1"
              xuiInput
              size="sm"
              aria-label="Group title"
              [disabled]="!store.editable()"
              [value]="group.title"
              (change)="retitle(group.id, $event)"
            />
            <span class="readout text-foreground-muted shrink-0">{{ group.nodes.length }}</span>
            <button
              type="button"
              xuiButton
              size="sm"
              variant="ghost"
              [disabled]="!store.editable()"
              (click)="store.ungroup(group.id)"
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
  private readonly project = inject(ProjectStore);
  private readonly status = inject(SystemStatus);
  protected readonly kinds = Kinds;
  protected readonly bitDepths = BitDepths;
  protected readonly portKinds = PortKinds;

  protected readonly seed = computed(() => Number(this.store.document()?.settings['seed'] ?? 0));

  protected readonly bitDepth = computed(() => this.store.document()?.settings['bitDepth'] ?? DefaultBitDepth);

  /** The Server offers boundary nodes to a graph that can have an interface, and to no other. */
  protected readonly isCompound = computed(() => this.store.types().has('Sub-graph/Input'));

  /** The project's bakes, by path below the library, and no bake at all. */
  protected readonly bakes = computed(() => {
    const held = this.project.current()?.bakes ?? [];
    return held.length > 0 ? [NoBake, ...held.map(bake => bake.path)] : [];
  });

  protected readonly bake = linkedSignal(() => {
    const path = this.store.document()?.path;
    return this.project.current()?.documents.find(document => document.path === path)?.bake ?? NoBake;
  });

  protected readonly bakeName = (path: string): string =>
    path === NoBake ? 'None' : (this.project.current()?.bakes.find(bake => bake.path === path)?.name ?? path);

  /** What a graph's Mesh Map nodes read. A project's stacks find their bake; this says it for a graph. */
  protected chooseBake(path: string | null): void {
    const status = this.status.state();
    if (path === null || path === this.bake() || status.kind !== 'online') {
      return;
    }

    this.bake.set(path);
    void this.store.useMeshMaps(path === NoBake ? null : `${status.system.library ?? ''}/${path}`);
  }

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

  protected setBitDepth(value: string | null): void {
    if (value !== null && value !== this.bitDepth()) {
      this.store.setSetting('bitDepth', value);
    }
  }

  protected addPort(direction: string): void {
    this.store.addPort({
      name: freePortName(this.store.document()?.interface ?? [], direction),
      direction,
      kind: 'Image',
      default: [],
      summary: ''
    });
  }

  protected renamePort(port: GraphInterfacePort, event: Event): void {
    const name = (event.target as HTMLInputElement).value.trim();
    if (name !== port.name) {
      this.store.editPort(port.name, port.direction, { ...port, name });
    }
  }

  protected setPortKind(port: GraphInterfacePort, kind: string | null): void {
    if (kind !== null && kind !== port.kind) {
      this.store.editPort(port.name, port.direction, { ...port, kind, default: [] });
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

  protected retitle(id: number, event: Event): void {
    this.store.renameGroup(id, (event.target as HTMLInputElement).value);
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
      (click)="store.removeComment(note())"
    >
      Delete note
    </button>
  `
})
export class GraphNoteInspector {
  protected readonly store = inject(GraphStore);

  readonly note = input.required<number>();

  protected readonly text = computed(
    () => this.store.document()?.comments.find(comment => comment.id === this.note())?.text ?? ''
  );

  protected retype(event: Event): void {
    this.store.setCommentText(this.note(), (event.target as HTMLTextAreaElement).value);
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
