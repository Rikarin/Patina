import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiDialogImports } from '@xui/dialog';
import { XuiInputImports } from '@xui/input';
import { XuiSwitchImports } from '@xui/switch';
import { desktop } from '../core/desktop';
import { ProjectStore } from './project-store';

function fileName(path: string | null | undefined): string {
  return path?.split(/[\\/]/u).pop() ?? '';
}

/** What a whole project shares. The mesh is here once, and every texturing document in the project is on it. */
@Component({
  selector: 'ptn-project-settings',
  imports: [XuiButtonImports, XuiCalloutImports, XuiDialogImports, XuiInputImports, XuiSwitchImports],
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
          </div>
          <label class="flex items-center justify-between gap-2">
            <span class="flex flex-col">
              <span class="text-sm">Auto Unwrap</span>
              <span class="text-foreground-muted text-xs">Gives a mesh with no UVs a layout. The original file stays.</span>
            </span>
            <xui-switch ariaLabel="Auto Unwrap" data-testid="project-settings-unwrap" [(checked)]="autoUnwrap" />
          </label>
          <label class="flex items-center justify-between gap-2">
            <span class="flex flex-col">
              <span class="text-sm">Preserve Paint</span>
              <span class="text-foreground-muted text-xs">
                Moves paint to a new mesh's UVs by where it sits on the surface. Open documents close and lose their undo.
              </span>
            </span>
            <xui-switch ariaLabel="Preserve Paint" data-testid="project-settings-preserve" [(checked)]="preservePaint" />
          </label>
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
            {{ project.unwrapping() ? 'Applying…' : 'Apply' }}
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
  protected readonly changed = computed(
    () =>
      this.mesh().trim() !== '' ||
      this.autoUnwrap() !== (this.project.current()?.autoUnwrap ?? true) ||
      this.preservePaint() !== (this.project.current()?.preservePaint ?? true)
  );

  constructor() {
    effect(() => {
      if (this.project.settingsOpen()) {
        this.project.settingsFailure.set(null);
        this.mesh.set('');
        this.autoUnwrap.set(this.project.current()?.autoUnwrap ?? true);
        this.preservePaint.set(this.project.current()?.preservePaint ?? true);
      }
    });
  }

  protected text(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  protected async pickMesh(): Promise<void> {
    this.mesh.set((await desktop()?.openMesh()) ?? this.mesh());
  }

  protected async apply(event: Event): Promise<void> {
    event.preventDefault();
    const applied = await this.project.applySettings({
      mesh: this.mesh().trim() || null,
      // Sent only when changed: setting auto unwrap on is what closes documents to unwrap the mesh.
      ...(this.autoUnwrap() === this.project.current()?.autoUnwrap ? {} : { autoUnwrap: this.autoUnwrap() }),
      preservePaint: this.preservePaint()
    });
    if (applied) {
      this.project.settingsOpen.set(false);
    }
  }
}
