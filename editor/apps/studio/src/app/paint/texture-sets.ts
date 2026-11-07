import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ProjectStore } from '../library/project-store';
import { PaintStore } from './paint-store';

/** The model's texture sets: a document per material, of which one is being painted. */
@Component({
  selector: 'ptn-texture-sets',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col overflow-auto p-1' },
  template: `
    <div role="listbox" aria-label="Texture sets" data-testid="texture-sets">
      @for (set of sets(); track set.path) {
        <button
          type="button"
          role="option"
          class="border-hairline hover:bg-selection flex h-7 w-full items-center gap-2 border-b px-2 text-start text-sm"
          [attr.aria-selected]="set.path === front()"
          [attr.data-testid]="'texture-set-' + set.name"
          [class.outline]="set.path === front()"
          (click)="choose(set.path)"
        >
          <span class="min-w-0 flex-1 truncate">{{ set.name }}</span>
          @if (set.path === front() && size(); as side) {
            <span class="readout text-foreground-muted shrink-0 text-xs" data-testid="texture-set-size">{{ side }}</span>
          }
        </button>
      } @empty {
        <p class="text-foreground-muted p-2 text-sm">Nothing open.</p>
      }
    </div>
  `
})
export class TextureSets {
  private readonly project = inject(ProjectStore);
  private readonly store = inject(PaintStore);
  protected readonly front = computed(() => this.project.document()?.path);
  /** A mesh with one material has one set, which is the document itself. */
  protected readonly sets = computed(() => {
    const named = this.project.textureSets();
    const document = this.project.document();
    return named.length > 0
      ? named.map(set => ({ path: set.path, name: set.material ?? set.name }))
      : document?.kind === 'texturing'
        ? [{ path: document.path, name: document.name }]
        : [];
  });
  protected readonly size = computed(() => {
    const state = this.store.state();
    return state ? `${state.width}×${state.height}` : null;
  });

  protected choose(path: string): void {
    const project = this.project.current();
    if (project) {
      void this.project.openDocument(project.path, path);
    }
  }
}
