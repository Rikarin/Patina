import { ChangeDetectionStrategy, Component, computed, effect, inject, input, model, signal } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiDialogImports } from '@xui/dialog';
import { XuiFormFieldImports } from '@xui/form-field';
import { XuiInputImports } from '@xui/input';
import { XuiTagInputImports } from '@xui/tag-input';
import { desktop } from '../core/desktop';
import { LibraryStore } from './library-store';
import { LibraryTreeStore } from './library-tree-store';
import { ProjectStore } from './project-store';

/** Asks for a new project's name. The Server decides whether it can exist, so its refusal is what this shows. */
@Component({
  selector: 'ptn-new-project',
  imports: [
    XuiButtonImports,
    XuiCalloutImports,
    XuiDialogImports,
    XuiFormFieldImports,
    XuiInputImports,
    XuiTagInputImports
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <xui-dialog title="New project" size="sm" [(open)]="open">
      <form (submit)="create($event)">
        <xui-dialog-body class="flex flex-col gap-3">
          <xui-form-field label="Name">
            <input
              class="w-full"
              xuiInput
              cdkFocusInitial
              data-testid="new-project-name"
              autocomplete="off"
              [value]="name()"
              (input)="name.set(text($event))"
            />
          </xui-form-field>
          <div class="flex flex-col gap-1">
            <span class="section-label">Mesh</span>
            <div class="flex items-center gap-2">
              @if (canPick) {
                <span class="min-w-0 flex-1 truncate" data-testid="new-project-mesh-name">{{ meshName() || 'None yet' }}</span>
                <button type="button" xuiButton size="sm" data-testid="new-project-mesh" (click)="pickMesh()">Choose…</button>
              } @else {
                <input
                  class="w-full"
                  xuiInput
                  autocomplete="off"
                  aria-label="Mesh file on the Server"
                  data-testid="new-project-mesh-path"
                  [value]="mesh()"
                  (input)="mesh.set(text($event))"
                />
              }
            </div>
          </div>
          <label class="flex flex-col gap-1">
            <span class="section-label">Resolution</span>
            <select
              class="well h-7 w-full px-1"
              data-testid="new-project-resolution"
              (change)="resolution.set(number($event))"
            >
              @for (side of sides; track side) {
                <option [value]="side" [selected]="side === resolution()">{{ side }} × {{ side }}</option>
              }
            </select>
          </label>
          @if (folder()) {
            <p class="text-foreground-muted text-xs" data-testid="new-project-folder">In {{ folder() }}</p>
          }
          <div class="flex flex-col gap-1">
            <span class="section-label">Tags</span>
            <xui-tag-input placeholder="Type a tag, then Enter" [(values)]="tags" />
          </div>
          @if (refused(); as reason) {
            <xui-callout color="error" data-testid="new-project-refused" compact>{{ reason }}</xui-callout>
          }
        </xui-dialog-body>
        <xui-dialog-footer>
          <button type="button" xuiButton variant="ghost" (click)="open.set(false)">Cancel</button>
          <button type="submit" xuiButton color="primary" [disabled]="!canCreate()">Create</button>
        </xui-dialog-footer>
      </form>
    </xui-dialog>
  `
})
export class NewProjectDialog {
  protected async pickMesh(): Promise<void> {
    const mesh = await desktop()?.openMesh();
    if (mesh) {
      this.mesh.set(mesh);
    }
  }

  protected number(event: Event): number {
    return Number((event.target as HTMLSelectElement).value);
  }

  private readonly store = inject(LibraryStore);
  private readonly tree = inject(LibraryTreeStore);
  private readonly project = inject(ProjectStore);

  readonly open = model(false);
  /** The folder it goes in, where empty is the top of the library. */
  readonly folder = input('');

  protected readonly name = signal('');
  protected readonly canPick = desktop() !== undefined;
  protected readonly sides = [512, 1024, 2048, 4096];
  protected readonly mesh = signal('');
  protected readonly resolution = signal(2048);
  protected readonly meshName = computed(() => this.mesh().split(/[\\/]/).pop() ?? '');
  protected readonly tags = signal<string[]>([]);
  protected readonly refused = signal<string | null>(null);
  private readonly saving = signal(false);
  protected readonly canCreate = computed(() => this.name().trim() !== '' && !this.saving());

  constructor() {
    effect(() => {
      if (this.open()) {
        this.refused.set(null); // a refusal is about the last attempt, not this one
      }
    });
  }

  protected text(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  protected async create(event: Event): Promise<void> {
    event.preventDefault();
    if (!this.canCreate()) {
      return;
    }

    this.saving.set(true);
    this.refused.set(null);
    try {
      const project = await this.store.createProject({
        name: this.name().trim(),
        folder: this.folder() || null,
        tags: this.tags(),
        mesh: this.mesh().trim() || null,
        resolution: this.resolution()
      });
      this.name.set('');
      this.mesh.set('');
      this.tags.set([]);
      this.open.set(false);
      await this.tree.reload();
      this.tree.choose(project.path);
      await this.project.open(project.path);
    } catch (error) {
      this.refused.set(error instanceof Error ? error.message : String(error));
    } finally {
      this.saving.set(false);
    }
  }
}
