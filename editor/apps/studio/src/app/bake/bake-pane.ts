import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import type { BakeRequest, JobState } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiInputImports } from '@xui/input';
import { XuiNumericInputImports } from '@xui/numeric-input';
import { XuiSelectImports } from '@xui/select';
import { XuiSwitchImports } from '@xui/switch';
import { desktop } from '../core/desktop';
import { GraphStore } from '../graph/graph-store';
import { BakeStore } from './bake-store';

/** The sides the panel offers. The API takes any; these are the ones an atlas is. */
const Sides = ['512', '1024', '2048', '4096', '8192'];
const Formats = ['png8', 'png16', 'exr16', 'exr32'];

/** The maps beyond the normal and the height, which every bake makes. */
const ExtraMaps: readonly { name: string; label: string }[] = [
  { name: 'ao', label: 'Ambient occlusion' },
  { name: 'curvature', label: 'Curvature' },
  { name: 'thickness', label: 'Thickness' },
  { name: 'bent', label: 'Bent normal' },
  { name: 'position', label: 'Position' },
  { name: 'world', label: 'World normal' },
  { name: 'id', label: 'ID' }
];

type PathField = 'high' | 'low' | 'cage' | 'skewMap' | 'output';

/**
 * Asks the Server for a bake and follows it. Everything sent is a path, as for any client (plan §8): a Blender add-on
 * makes the same request. The Server keeps the job, so closing this panel stops nothing, and the results are files that
 * this only lists; a click shows one in the Texture pane.
 */
@Component({
  selector: 'ptn-bake',
  imports: [
    XuiButtonImports,
    XuiCalloutImports,
    XuiInputImports,
    XuiNumericInputImports,
    XuiSelectImports,
    XuiSwitchImports
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col' },
  template: `
    <div class="flex min-h-0 flex-1 flex-col gap-2.5 overflow-x-hidden overflow-y-auto p-3">
      @for (field of pathFields; track field.key) {
        <div class="flex flex-col gap-1">
          <span class="text-foreground-muted text-xs">{{ field.label }}</span>
          <div class="flex gap-1">
            <input
              class="min-w-0 flex-1"
              xuiInput
              autocomplete="off"
              [attr.aria-label]="field.label"
              [attr.data-testid]="'bake-' + field.key"
              [placeholder]="field.placeholder"
              [value]="paths()[field.key]"
              (input)="setPath(field.key, text($event))"
            />
            @if (canPick && field.key !== 'skewMap') {
              <button
                type="button"
                xuiButton
                size="sm"
                variant="ghost"
                [attr.aria-label]="'Choose ' + field.label"
                (click)="pick(field.key)"
              >
                …
              </button>
            }
          </div>
        </div>
      }

      <div class="grid grid-cols-[minmax(0,1fr)_minmax(0,1fr)] gap-2">
        <div class="flex min-w-0 flex-col gap-1">
          <span class="text-foreground-muted text-xs">Resolution</span>
          <xui-select
            class="w-full"
            ariaLabel="Resolution"
            data-testid="bake-resolution"
            [items]="sides"
            [value]="side()"
            (valueChange)="side.set($event ?? side())"
          />
        </div>
        <div class="flex min-w-0 flex-col gap-1">
          <span class="text-foreground-muted text-xs">Format</span>
          <xui-select
            class="w-full"
            ariaLabel="Format"
            data-testid="bake-format"
            [items]="formats"
            [value]="format()"
            (valueChange)="format.set($event ?? format())"
          />
        </div>
        <div class="flex min-w-0 flex-col gap-1">
          <span class="text-foreground-muted text-xs">Samples per side</span>
          <xui-numeric-input
            size="sm"
            ariaLabel="Samples per side"
            [max]="8"
            [min]="1"
            [stepSize]="1"
            [value]="samples()"
            (valueChange)="samples.set($event ?? 1)"
          />
        </div>
        <div class="flex min-w-0 flex-col gap-1">
          <span class="text-foreground-muted text-xs">Occlusion rays</span>
          <xui-numeric-input
            size="sm"
            ariaLabel="Occlusion rays"
            [max]="8192"
            [min]="1"
            [stepSize]="32"
            [value]="rays()"
            (valueChange)="rays.set($event ?? 64)"
          />
        </div>
        <div class="flex min-w-0 flex-col gap-1">
          <span class="text-foreground-muted text-xs">Search radius</span>
          <xui-numeric-input
            size="sm"
            ariaLabel="Search radius"
            [max]="1"
            [min]="0.001"
            [stepSize]="0.01"
            [value]="search()"
            (valueChange)="search.set($event ?? 0.05)"
          />
        </div>
        <div class="flex min-w-0 flex-col gap-1">
          <span class="text-foreground-muted text-xs">Skew</span>
          <xui-numeric-input
            size="sm"
            ariaLabel="Skew"
            [max]="1"
            [min]="0"
            [stepSize]="0.1"
            [value]="skew()"
            (valueChange)="skew.set($event ?? 0)"
          />
        </div>
      </div>

      <label class="flex items-center justify-between gap-2 text-sm">
        Match parts by name
        <xui-switch ariaLabel="Match parts by name" [(checked)]="match" />
      </label>

      <p class="section-label">Maps</p>
      <p class="text-foreground-muted text-xs">Normal and height are always baked.</p>
      <div class="flex flex-col gap-1.5">
        @for (map of extraMaps; track map.name) {
          <label class="flex items-center justify-between gap-2 text-sm">
            {{ map.label }}
            <xui-switch
              [ariaLabel]="map.label"
              [attr.data-testid]="'bake-map-' + map.name"
              [checked]="maps().has(map.name)"
              (checkedChange)="setMap(map.name, $event)"
            />
          </label>
        }
      </div>

      @if (refused(); as reason) {
        <xui-callout color="error" data-testid="bake-refused" compact>{{ reason }}</xui-callout>
      }

      @if (store.current(); as job) {
        <div class="well flex flex-col gap-1.5 p-2" data-testid="bake-job">
          <div class="flex items-center gap-2">
            <span class="readout" data-testid="bake-state">{{ job.state }}</span>
            <span class="readout ms-auto" data-testid="bake-percent">{{ percent() }}%</span>
          </div>
          <div
            class="bg-surface-sunken h-1.5 overflow-hidden rounded-sm"
            role="progressbar"
            aria-label="Bake progress"
            [attr.aria-valuenow]="percent()"
            aria-valuemin="0"
            aria-valuemax="100"
          >
            <div class="bg-primary h-full" [style.width.%]="percent()"></div>
          </div>
          @if (job.error; as error) {
            <xui-callout color="error" compact>{{ error }}</xui-callout>
          }
          @if (job.result; as result) {
            @if (result.cached) {
              <span class="text-foreground-muted text-xs" data-testid="bake-cached">From the cache: nothing ran.</span>
            }
            @if (result.missed > 0) {
              <span class="text-foreground-muted text-xs">
                {{ result.missed }} of {{ result.covered }} texels found no surface and took the nearest point.
              </span>
            }
            @for (warning of result.warnings; track warning) {
              <xui-callout color="warning" compact>{{ warning }}</xui-callout>
            }
            <ul class="flex flex-col gap-0.5">
              @for (map of result.maps; track map.map) {
                <li>
                  <button
                    type="button"
                    xuiButton
                    class="w-full justify-start"
                    size="sm"
                    variant="ghost"
                    [attr.data-testid]="'bake-result-' + map.map"
                    [title]="map.path"
                    (click)="store.show(job, map.map)"
                  >
                    {{ map.map }}
                  </button>
                </li>
              }
            </ul>
            @if (job.bake?.space === 'tangent') {
              <button
                type="button"
                xuiButton
                size="sm"
                variant="ghost"
                data-testid="bake-show-on-mesh"
                title="The low mesh with these maps, in the viewport"
                (click)="showOnMesh(job)"
              >
                Show on low mesh
              </button>
            }
            @if (graph.document(); as document) {
              <button
                type="button"
                xuiButton
                size="sm"
                variant="ghost"
                data-testid="bake-use-in-graph"
                [title]="'Mesh Map nodes in ' + document.name + ' read this bake'"
                (click)="store.useInGraph(document.id, job)"
              >
                Use in {{ document.name }}
              </button>
            }
          }
        </div>
      }
    </div>

    <footer class="chrome-bar flex h-8 shrink-0 items-center gap-2 px-2">
      @if (running()) {
        <button type="button" xuiButton size="sm" variant="ghost" data-testid="bake-cancel" (click)="cancel()">
          Stop
        </button>
      }
      <button
        type="button"
        xuiButton
        class="ms-auto"
        color="primary"
        size="sm"
        data-testid="bake-start"
        [disabled]="!canStart()"
        (click)="start()"
      >
        Bake
      </button>
    </footer>
  `
})
export class BakePane {
  protected readonly store = inject(BakeStore);
  protected readonly graph = inject(GraphStore);
  protected readonly sides = Sides;
  protected readonly formats = Formats;
  protected readonly extraMaps = ExtraMaps;
  protected readonly canPick = desktop() !== undefined;
  protected readonly pathFields: readonly { key: PathField; label: string; placeholder: string }[] = [
    { key: 'high', label: 'High mesh', placeholder: 'Path on the Server' },
    { key: 'low', label: 'Low mesh', placeholder: 'Path on the Server' },
    { key: 'cage', label: 'Cage', placeholder: 'Optional' },
    { key: 'skewMap', label: 'Skew map', placeholder: 'Optional: a square grey image' },
    { key: 'output', label: 'Output folder', placeholder: 'Path on the Server' }
  ];

  protected readonly paths = signal<Record<PathField, string>>({
    high: '',
    low: '',
    cage: '',
    skewMap: '',
    output: ''
  });
  protected readonly side = signal('2048');
  protected readonly format = signal('png16');
  protected readonly samples = signal(1);
  protected readonly rays = signal(64);
  protected readonly search = signal(0.05);
  protected readonly skew = signal(0);
  protected readonly match = signal(false);
  protected readonly maps = signal<ReadonlySet<string>>(new Set(['ao']));
  protected readonly refused = signal<string | null>(null);

  protected readonly running = computed(() => {
    const state = this.store.current()?.state;
    return state === 'queued' || state === 'running';
  });
  protected readonly percent = computed(() => Math.round((this.store.current()?.progress ?? 0) * 100));
  protected readonly canStart = computed(() => {
    const { high, low, output } = this.paths();
    return high.trim() !== '' && low.trim() !== '' && output.trim() !== '' && !this.running();
  });

  protected text(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  protected setPath(field: PathField, value: string): void {
    this.paths.update(paths => ({ ...paths, [field]: value }));
  }

  protected setMap(name: string, on: boolean): void {
    this.maps.update(maps => {
      const next = new Set(maps);
      if (on) {
        next.add(name);
      } else {
        next.delete(name);
      }

      return next;
    });
  }

  protected async pick(field: PathField): Promise<void> {
    const bridge = desktop();
    const path = await (field === 'output' ? bridge?.openFolder() : bridge?.openMesh());
    if (path) {
      this.setPath(field, path);
    }
  }

  protected request(): BakeRequest {
    const { high, low, cage, skewMap, output } = this.paths();
    return {
      high: high.trim(),
      low: low.trim(),
      output: output.trim(),
      cage: cage.trim() || null,
      resolution: Number(this.side()),
      maps: [...this.maps()],
      format: this.format(),
      supersampling: this.samples(),
      occlusionRays: this.rays(),
      searchRadius: this.search(),
      occlusionRadius: 0.5,
      skew: this.skew(),
      skewMap: skewMap.trim() || null,
      matchByName: this.match(),
      gutter: 4,
      space: 'tangent',
      name: null
    };
  }

  protected async start(): Promise<void> {
    this.refused.set(null);
    try {
      await this.store.start(this.request());
    } catch (error) {
      this.refused.set(error instanceof Error ? error.message : String(error));
    }
  }

  protected async showOnMesh(job: JobState): Promise<void> {
    this.refused.set(null);
    try {
      await this.store.showOnMesh(job);
    } catch (error) {
      this.refused.set(error instanceof Error ? error.message : String(error));
    }
  }

  protected async cancel(): Promise<void> {
    const job = this.store.current();
    if (job) {
      await this.store.cancel(job.id);
    }
  }
}
