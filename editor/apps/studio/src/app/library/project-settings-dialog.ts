import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiDialogImports } from '@xui/dialog';
import { XuiInputImports } from '@xui/input';
import { XuiSwitchImports } from '@xui/switch';
import { XuiNumericInputImports } from '@xui/numeric-input';
import { XuiSelectImports } from '@xui/select';
import type { UnwrapOptions } from '@patina/api-client';
import { desktop } from '../core/desktop';
import { SystemStatus } from '../core/system-status';
import { MeshFactsView } from './mesh-facts';
import { ProjectStore } from './project-store';

/** What a project unwraps with until someone says otherwise, as the Server has it. */
const DefaultUnwrap: UnwrapOptions = {
  resolution: 2048,
  margin: 4,
  quality: 'outlines',
  rotations: 4,
  texelDensity: 0,
  overflow: 'shrink'
};

function fileName(path: string | null | undefined): string {
  return path?.split(/[\\/]/u).pop() ?? '';
}

/** The options with their keys in one order, so that comparing their text compares them. */
function sameOrder(options: UnwrapOptions): UnwrapOptions {
  const { resolution, margin, quality, rotations, texelDensity, overflow } = options;
  return { resolution, margin, quality, rotations, texelDensity, overflow };
}

/** What a whole project shares. The mesh is here once, and every texturing document in the project is on it. */
@Component({
  selector: 'ptn-project-settings',
  imports: [
    XuiButtonImports,
    XuiCalloutImports,
    XuiDialogImports,
    XuiInputImports,
    XuiNumericInputImports,
    XuiSelectImports,
    XuiSwitchImports,
    MeshFactsView
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <xui-dialog
      size="sm"
      title="Project Settings"
      [open]="project.settingsOpen()"
      (openChange)="project.settingsOpen.set($event)"
    >
      <form (submit)="apply($event)">
        <xui-dialog-body class="flex flex-col gap-3" data-testid="project-settings">
          <div class="flex flex-col gap-1">
            <span class="section-label">Mesh</span>
            <div class="flex items-center gap-2">
              @if (canPick) {
                <span class="min-w-0 flex-1 truncate" data-testid="project-settings-mesh-name">
                  {{ meshName() || 'None yet' }}
                </span>
                <button type="button" xuiButton size="sm" data-testid="project-settings-mesh" (click)="pickMesh()">
                  Choose…
                </button>
              } @else {
                <input
                  class="w-full"
                  xuiInput
                  autocomplete="off"
                  aria-label="Mesh file"
                  data-testid="project-settings-mesh-path"
                  [placeholder]="meshName()"
                  [value]="mesh()"
                  (input)="mesh.set(text($event))"
                />
              }
            </div>
            <ptn-mesh-facts [path]="factsOf()" />
          </div>
          <label class="flex items-center justify-between gap-2">
            <span class="flex flex-col">
              <span class="text-sm">Auto Unwrap</span>
              <span class="text-foreground-muted text-xs">Gives a mesh with no UVs a layout. The original file stays.</span>
            </span>
            <xui-switch ariaLabel="Auto Unwrap" data-testid="project-settings-unwrap" [(checked)]="autoUnwrap" />
          </label>
          @if (autoUnwrap()) {
            <div class="well grid grid-cols-2 gap-2 px-2 py-2" data-testid="project-settings-unwrap-options">
              <label class="flex flex-col gap-1">
                <span class="text-foreground-muted text-xs">Atlas</span>
                <xui-select
                  size="sm"
                  ariaLabel="Unwrap atlas size"
                  data-testid="unwrap-resolution"
                  [items]="resolutions()"
                  [itemText]="texels"
                  [value]="options().resolution"
                  (valueChange)="set({ resolution: $event ?? options().resolution })"
                />
              </label>
              <label class="flex flex-col gap-1">
                <span class="text-foreground-muted text-xs">Margin, in texels</span>
                <xui-numeric-input
                  size="sm"
                  ariaLabel="Unwrap margin in texels"
                  data-testid="unwrap-margin"
                  [min]="0"
                  [max]="64"
                  [stepSize]="1"
                  [value]="options().margin"
                  (valueChange)="set({ margin: $event ?? 0 })"
                />
              </label>
              <label class="flex flex-col gap-1">
                <span class="text-foreground-muted text-xs">Packing</span>
                <xui-select
                  size="sm"
                  ariaLabel="Unwrap packing"
                  data-testid="unwrap-quality"
                  [items]="qualities"
                  [itemText]="qualityName"
                  [value]="options().quality"
                  (valueChange)="set({ quality: $event ?? options().quality })"
                />
              </label>
              <label class="flex flex-col gap-1">
                <span class="text-foreground-muted text-xs">Island turns</span>
                <xui-select
                  size="sm"
                  ariaLabel="Unwrap island turns"
                  data-testid="unwrap-rotations"
                  [items]="turns"
                  [itemText]="turnName"
                  [value]="options().rotations"
                  (valueChange)="set({ rotations: $event ?? options().rotations })"
                />
              </label>
              <label class="flex flex-col gap-1">
                <span class="text-foreground-muted text-xs">Texels per unit, 0 for each island's own</span>
                <xui-numeric-input
                  size="sm"
                  ariaLabel="Unwrap texel density"
                  data-testid="unwrap-density"
                  [min]="0"
                  [stepSize]="64"
                  [value]="options().texelDensity"
                  (valueChange)="set({ texelDensity: $event ?? 0 })"
                />
              </label>
              <label class="flex flex-col gap-1">
                <span class="text-foreground-muted text-xs">If islands do not fit</span>
                <xui-select
                  size="sm"
                  ariaLabel="Unwrap overflow"
                  data-testid="unwrap-overflow"
                  [items]="overflows"
                  [itemText]="overflowName"
                  [value]="options().overflow"
                  (valueChange)="set({ overflow: $event ?? options().overflow })"
                />
              </label>
            </div>
          }
          @if (report(); as report) {
            <div class="flex flex-col gap-1" data-testid="project-settings-unwrap-report">
              <span class="section-label">Unwrapped from {{ report.from }}</span>
              @for (set of report.sets; track set.material) {
                <div class="flex flex-col" data-testid="unwrap-report-set">
                  <div class="flex items-center gap-2 text-sm">
                    <span class="min-w-0 flex-1 truncate">{{ set.material || 'Mesh' }}</span>
                    <span class="readout" title="Islands">{{ set.charts }} islands</span>
                    <span class="readout" title="Stretch, averaged and worst. 1 is none.">
                      ×{{ set.stretch.toFixed(2) }} / {{ set.worstStretch.toFixed(2) }}
                    </span>
                    <span class="readout" title="Share of the atlas the islands cover">{{ percent(set.packed) }}</span>
                  </div>
                  @if (set.flipped > 0) {
                    <span class="text-error text-xs">
                      {{ set.flipped }} triangles are turned over in the atlas and will paint in the wrong place.
                    </span>
                  }
                  @for (warning of set.warnings; track $index) {
                    <span class="text-foreground-muted text-xs">{{ warning }}</span>
                  }
                </div>
              }
            </div>
          }
          <label class="flex items-center justify-between gap-2">
            <span class="flex flex-col">
              <span class="text-sm">Preserve Paint</span>
              <span class="text-foreground-muted text-xs">
                Moves paint to a new mesh's UVs by where it sits on the surface.
              </span>
            </span>
            <xui-switch ariaLabel="Preserve Paint" data-testid="project-settings-preserve" [(checked)]="preservePaint" />
          </label>
          <div class="flex flex-col gap-1" data-testid="project-files">
            <span class="section-label">Files</span>
            <div class="well flex max-h-40 flex-col overflow-auto px-2 py-1">
              @for (file of files(); track file.path) {
                <div class="flex items-center gap-2" data-testid="project-file" [title]="file.path">
                  <span class="min-w-0 flex-1 truncate text-sm">{{ file.name }}</span>
                  <span class="readout shrink-0">{{ file.kind }}</span>
                </div>
              } @empty {
                <span class="text-foreground-muted text-sm">Nothing yet. Use File > Import, or drop files on the window.</span>
              }
            </div>
          </div>
          @if (closes(); as documents) {
            <xui-callout color="warning" data-testid="project-settings-closes">
              Applying closes {{ documents.length === 1 ? documents[0]?.name : documents.length + ' open documents' }} to
              move {{ documents.length === 1 ? 'it' : 'them' }} onto the new layout, and
              {{ documents.length === 1 ? 'its' : 'their' }} undo history is lost. Saved
              work is kept{{ unsaved() ? ', and unsaved edits have to be saved first' : '' }}.
            </xui-callout>
          }
          @if (project.settingsFailure(); as failure) {
            <xui-callout color="error" data-testid="project-settings-failure">{{ failure }}</xui-callout>
          }
        </xui-dialog-body>
        <xui-dialog-footer>
          <button type="button" xuiButton variant="ghost" (click)="project.settingsOpen.set(false)">Cancel</button>
          <button
            type="submit"
            xuiButton
            color="primary"
            data-testid="project-settings-apply"
            [disabled]="project.unwrapping() || !changed()"
          >
            {{ project.unwrapping() ? 'Applying…' : closes() ? 'Close and Apply' : 'Apply' }}
          </button>
        </xui-dialog-footer>
      </form>
    </xui-dialog>
  `
})
export class ProjectSettingsDialog {
  protected readonly project = inject(ProjectStore);
  protected readonly canPick = desktop() !== undefined;
  protected readonly mesh = signal('');
  protected readonly autoUnwrap = signal(true);
  protected readonly preservePaint = signal(true);
  protected readonly meshName = computed(() => fileName(this.mesh()) || (this.project.meshName() ?? ''));
  /** What the project holds besides its documents. */
  protected readonly files = computed(() => {
    const contents = this.project.current();
    return [
      ...(contents?.meshes ?? []).map(mesh => ({ kind: 'Mesh', name: fileName(mesh.path), path: mesh.path })),
      ...(contents?.pictures ?? []).map(picture => ({ kind: 'Picture', name: picture.name, path: picture.path })),
      ...(contents?.bakes ?? []).map(bake => ({
        kind: bake.stale ? 'Maps, out of date' : 'Maps',
        name: bake.name,
        path: bake.path
      }))
    ];
  });

  protected readonly options = signal<UnwrapOptions>(DefaultUnwrap);
  private readonly status = inject(SystemStatus);
  /** The sides every panel offers, from the Server, and the one this project has even if the list has not. */
  protected readonly resolutions = computed(() => {
    const sides = this.status.textureSides();
    const held = this.options().resolution;
    return sides.includes(held) ? sides : [...sides, held].sort((a, b) => a - b);
  });
  protected readonly qualities = ['boxes', 'outlines', 'groups'];
  protected readonly turns = [1, 2, 4];
  protected readonly overflows = ['shrink', 'refuse'];
  protected readonly texels = (side: number): string => `${side} px`;
  protected readonly qualityName = (quality: string): string =>
    ({ boxes: 'Fast, by boxes', outlines: 'Tight, by outlines', groups: 'Tightest, grouped' })[quality] ?? quality;
  protected readonly turnName = (count: number): string =>
    ({ 1: 'Never', 2: 'Half turns', 4: 'Quarter turns' })[count] ?? `${count}`;
  protected readonly overflowName = (overflow: string): string =>
    ({ shrink: 'Shrink them all', refuse: 'Stop and say so' })[overflow] ?? overflow;
  /** The mesh whose facts show: one just chosen, or the one the documents are on. */
  protected readonly factsOf = computed(() => {
    const chosen = this.mesh().trim();
    if (chosen || !this.project.settingsOpen()) {
      return chosen;
    }

    const contents = this.project.current();
    const on = contents?.documents.find(document => !!document.mesh)?.mesh;
    return (contents?.meshes.find(mesh => !!on && mesh.id === on) ?? contents?.meshes[0])?.path ?? '';
  });
  /** What Auto Unwrap made of the mesh the documents are on. */
  protected readonly report = computed(() => {
    const contents = this.project.current();
    const on = contents?.documents.find(document => !!document.mesh)?.mesh;
    return contents?.meshes.find(mesh => !!on && mesh.id === on)?.unwrapped ?? null;
  });
  protected readonly optionsChanged = computed(
    () => JSON.stringify(this.options()) !== JSON.stringify(this.project.current()?.unwrap ?? DefaultUnwrap)
  );

  /** What applying would close, which it says before the click rather than after. Null when it closes nothing. */
  protected readonly closes = computed(() => {
    const path = this.project.current()?.path;
    const moving = this.project.movesMesh(this.settings());
    const documents = path && moving ? this.project.openInside([path]) : [];
    return documents.length > 0 ? documents : null;
  });

  protected readonly unsaved = computed(() => this.closes()?.some(document => document.dirty) ?? false);

  protected readonly changed = computed(
    () =>
      this.mesh().trim() !== '' ||
      this.autoUnwrap() !== (this.project.current()?.autoUnwrap ?? true) ||
      this.preservePaint() !== (this.project.current()?.preservePaint ?? true) ||
      this.optionsChanged()
  );

  constructor() {
    effect(() => {
      if (this.project.settingsOpen()) {
        this.project.settingsFailure.set(null);
        this.mesh.set('');
        this.autoUnwrap.set(this.project.current()?.autoUnwrap ?? true);
        this.preservePaint.set(this.project.current()?.preservePaint ?? true);
        this.options.set(sameOrder(this.project.current()?.unwrap ?? DefaultUnwrap));
      }
    });
  }

  /** What Apply sends. */
  private settings() {
    return {
      mesh: this.mesh().trim() || null,
      // Sent only when changed: setting auto unwrap on is what closes documents to unwrap the mesh.
      ...(this.autoUnwrap() === this.project.current()?.autoUnwrap ? {} : { autoUnwrap: this.autoUnwrap() }),
      // Sent only when changed too: new options lay out again the mesh Auto Unwrap wrote, and move its paint.
      ...(this.optionsChanged() ? { unwrap: this.options() } : {}),
      preservePaint: this.preservePaint()
    };
  }

  protected set(change: Partial<UnwrapOptions>): void {
    this.options.update(options => ({ ...options, ...change }));
  }

  protected percent(share: number): string {
    return `${Math.round(share * 100)}%`;
  }

  protected text(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  protected async pickMesh(): Promise<void> {
    this.mesh.set((await desktop()?.openMesh(this.status.meshes())) ?? this.mesh());
  }

  protected async apply(event: Event): Promise<void> {
    event.preventDefault();
    const applied = await this.project.applySettings(this.settings());
    if (applied) {
      this.project.settingsOpen.set(false);
    }
  }
}
