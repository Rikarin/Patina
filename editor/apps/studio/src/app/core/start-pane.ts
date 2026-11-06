import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { ProjectStore } from '../library/project-store';
import { LibraryStore } from '../library/library-store';
import { desktop } from './desktop';

const Kinds = [
  { kind: 'texturing', title: 'Texturing', text: 'Paint and layer materials on a mesh.' },
  { kind: 'layered-material', title: 'Layered Material', text: 'Stack materials into a new one, with no mesh.' },
  { kind: 'image-material', title: 'Image Material', text: 'Make a material from one photo.' },
  { kind: 'graph', title: 'Material Graph', text: 'Build a material from nodes.' }
];

/** Where the app starts with nothing open: what can be begun, and what was being worked on. */
@Component({
  selector: 'ptn-start',
  imports: [XuiButtonImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full flex-col items-center justify-center gap-6 overflow-auto p-6' },
  template: `
    @if (library.state().kind === 'ready') {
      <h1 class="text-lg font-semibold">What are you making?</h1>
      <div class="grid w-full max-w-3xl grid-cols-2 gap-3 lg:grid-cols-4">
        @for (item of kinds; track item.kind) {
          <button
            type="button"
            class="well hover:bg-selection flex flex-col gap-1 rounded p-4 text-start"
            [attr.data-testid]="'start-' + item.kind"
            (click)="project.startProject(item.kind)"
          >
            <span class="font-semibold">{{ item.title }}</span>
            <span class="text-foreground-muted text-sm">{{ item.text }}</span>
          </button>
        }
      </div>
      <p class="text-foreground-muted text-sm">Or open a project from the Library.</p>
    } @else {
      <h1 class="text-lg font-semibold">Welcome to Patina</h1>
      <p class="text-foreground-muted max-w-md text-center text-sm">
        A library holds one game's projects and the materials they share.
      </p>
      @if (canPick) {
        <div class="flex gap-2">
          <button type="button" xuiButton color="primary" data-testid="start-new-library" (click)="newLibrary()">
            New Library…
          </button>
          <button type="button" xuiButton data-testid="start-open-library" (click)="openLibrary()">Open Library…</button>
        </div>
      }
      @for (path of recent(); track path) {
        <button type="button" class="hover:bg-selection px-3 py-1 text-sm" data-testid="start-recent" [title]="path" (click)="openRecent(path)">
          {{ path }}
        </button>
      }
    }
  `
})
export class StartPane {
  protected readonly library = inject(LibraryStore);
  protected readonly project = inject(ProjectStore);
  protected readonly kinds = Kinds;
  protected readonly canPick = desktop() !== undefined;
  protected readonly recent = signal<readonly string[]>([]);

  constructor() {
    void desktop()
      ?.recentLibraries()
      .then(paths => this.recent.set(paths));
  }

  protected newLibrary(): void {
    void desktop()?.newLibrary();
  }

  protected openLibrary(): void {
    void desktop()?.openLibrary();
  }

  protected openRecent(path: string): void {
    void desktop()?.openRecentLibrary(path);
  }
}
