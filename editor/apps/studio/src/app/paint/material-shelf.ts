import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { type MaterialInfo, unwrap } from '@patina/api-client';
import { XuiCalloutImports } from '@xui/callout';
import { XuiInputImports } from '@xui/input';
import { LibraryStore } from '../library/library-store';
import { ProjectStore } from '../library/project-store';
import { PaintStore } from './paint-store';

const OriginNames: Readonly<Record<string, string>> = {
  shipped: 'Starter',
  project: 'This project',
  shared: 'Shared'
};

/** What a layer can be filled with. Clicking one puts it on top of the stack (ADR 0012). */
@Component({
  selector: 'ptn-material-shelf',
  imports: [XuiCalloutImports, XuiInputImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col' },
  template: `
    <div class="shrink-0 p-2">
      <input
        class="w-full"
        xuiInput
        size="sm"
        type="search"
        autocomplete="off"
        aria-label="Search materials"
        data-testid="shelf-search"
        placeholder="Search materials"
        [value]="search()"
        (input)="search.set(text($event))"
      />
    </div>
    @if (failure(); as reason) {
      <xui-callout color="error" compact data-testid="shelf-failure">{{ reason }}</xui-callout>
    }
    <ul class="min-h-0 flex-1 overflow-auto" aria-label="Materials">
      @for (material of shown(); track material.source) {
        <li>
          <button
            type="button"
            class="hover:bg-selection flex w-full items-center gap-2 px-2 py-1 text-start text-sm"
            data-testid="shelf-material"
            [disabled]="material.role === 'empty'"
            [title]="material.role === 'empty' ? 'This graph has no Output yet.' : 'Add to the stack'"
            (click)="use(material)"
          >
            <span class="min-w-0 flex-1 truncate">{{ material.name }}</span>
            <span class="text-foreground-muted shrink-0 text-xs">{{ originOf(material) }}</span>
          </button>
        </li>
      } @empty {
        <li class="text-foreground-muted p-2 text-sm">Nothing matches.</li>
      }
    </ul>
  `
})
export class MaterialShelf {
  private readonly library = inject(LibraryStore);
  private readonly paint = inject(PaintStore);
  private readonly project = inject(ProjectStore);
  private readonly materials = signal<readonly MaterialInfo[]>([]);

  protected readonly search = signal('');
  protected readonly failure = signal<string | null>(null);
  protected readonly shown = computed(() => {
    const words = this.search().trim().toLowerCase();
    return this.materials().filter(material => material.name.toLowerCase().includes(words));
  });

  constructor() {
    effect(() => {
      const project = this.project.current()?.path;
      this.library.changes();
      if (project) {
        untracked(() => void this.load(project));
      }
    });
  }

  protected text(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  protected originOf(material: MaterialInfo): string {
    return OriginNames[material.origin] ?? material.origin;
  }

  protected async use(material: MaterialInfo): Promise<void> {
    const project = this.project.current();
    if (!project) {
      return;
    }

    this.failure.set(null);
    try {
      if (material.kind === 'graph') {
        const client = await this.library.api();
        const body = { project: project.path, source: material.source };
        const { graph } = unwrap(await client.POST('/api/v1/library/materials/use', { body }));
        await this.paint.addGraphLayer(material.name, graph);
      } else {
        await this.paint.edit({ op: 'addLayer', kind: 'material', name: material.name, material: material.source });
      }
    } catch (failure) {
      this.failure.set(failure instanceof Error ? failure.message : String(failure));
    }
  }

  private async load(project: string): Promise<void> {
    try {
      const client = await this.library.api();
      const list = unwrap(await client.GET('/api/v1/library/materials', { params: { query: { project } } }));
      // A stack cannot be a layer of itself.
      const open = this.project.active()?.path;
      this.materials.set(list.items.filter(material => material.source !== open));
    } catch (failure) {
      this.failure.set(failure instanceof Error ? failure.message : String(failure));
    }
  }
}
