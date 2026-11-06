import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { type MaterialInfo, unwrap } from '@patina/api-client';
import { XuiCalloutImports } from '@xui/callout';
import { XuiInputImports } from '@xui/input';
import { LibraryStore } from '../library/library-store';
import { ProjectStore } from '../library/project-store';
import { MaterialUse } from './material-use';

const OriginNames: Readonly<Record<string, string>> = {
  shipped: 'Starter',
  project: 'This project'
};
const SearchQuiet = 200;

/** What a layer can be filled with, from anywhere in the Library. Click to add it, or drag it into the stack (ADR 0012). */
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
    @if (failure() ?? using.failure(); as reason) {
      <xui-callout color="error" compact data-testid="shelf-failure">{{ reason }}</xui-callout>
    }
    <ul class="min-h-0 flex-1 overflow-auto" aria-label="Materials">
      @for (material of shown(); track material.source) {
        <li>
          <button
            type="button"
            class="hover:bg-selection flex w-full items-center gap-2 px-2 py-1 text-start text-sm"
            data-testid="shelf-material"
            [attr.draggable]="material.role !== 'empty'"
            [disabled]="material.role === 'empty'"
            [title]="
              material.role === 'empty' ? 'This graph has no Output yet.' : 'Click to add, or drag into the stack'
            "
            (click)="use(material)"
            (dragstart)="carry($event, material)"
            (dragend)="using.carried.set(null)"
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
  private readonly project = inject(ProjectStore);
  protected readonly using = inject(MaterialUse);
  private readonly materials = signal<readonly MaterialInfo[]>([]);

  protected readonly search = signal('');
  protected readonly failure = signal<string | null>(null);
  private searching: ReturnType<typeof setTimeout> | undefined;
  protected readonly shown = computed(() => {
    const words = this.search().trim().toLowerCase();
    return this.materials().filter(material => material.name.toLowerCase().includes(words));
  });

  constructor() {
    effect(() => {
      const project = this.project.current()?.path;
      const search = this.search().trim();
      this.library.changes();
      clearTimeout(this.searching);
      if (project) {
        // A search reaches materials past what the list holds, so it asks the Server, though not at every key.
        this.searching = setTimeout(() => untracked(() => void this.load(project, search)), search ? SearchQuiet : 0);
      }
    });
  }

  protected text(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  protected originOf(material: MaterialInfo): string {
    return material.project ?? OriginNames[material.origin] ?? material.origin;
  }

  protected use(material: MaterialInfo): void {
    void this.using.use(material);
  }

  protected carry(event: DragEvent, material: MaterialInfo): void {
    this.using.carried.set(material);
    event.dataTransfer?.setData('text/plain', material.name);
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = 'copy';
    }
  }

  private async load(project: string, search: string): Promise<void> {
    try {
      const client = await this.library.api();
      const list = unwrap(await client.GET('/api/v1/library/materials', { params: { query: { project, search } } }));
      // A stack cannot be a layer of itself.
      const open = this.project.active()?.path;
      this.materials.set(list.items.filter(material => material.source !== open));
    } catch (failure) {
      this.failure.set(failure instanceof Error ? failure.message : String(failure));
    }
  }
}
