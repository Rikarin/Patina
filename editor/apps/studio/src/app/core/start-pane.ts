import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { LibraryStore } from '../library/library-store';
import { ProjectStore } from '../library/project-store';
import { desktop } from './desktop';
import { messageOf } from './messages';
import { Preferences, type RecentDocument } from './preferences';

/** How many recent documents Start offers. */
const RecentShown = 8;

/** How many recent projects Start offers (#162). */
const RecentProjectsShown = 6;

/** A project worked on lately, by its path below the library and the name its folder gives it. */
export interface RecentProject {
  readonly path: string;
  readonly name: string;
}

/** The projects of recent documents, each once, newest first: a project is as recent as its newest document. */
export function recentProjectsOf(documents: readonly RecentDocument[], shown = RecentProjectsShown): RecentProject[] {
  const seen = new Set<string>();
  const found: RecentProject[] = [];
  for (const document of documents) {
    if (!seen.has(document.project) && found.length < shown) {
      seen.add(document.project);
      found.push({ path: document.project, name: document.project.split('/').at(-1) ?? document.project });
    }
  }

  return found;
}

const Kinds = [
  { kind: 'texturing', title: 'Texturing', text: 'Paint and layer materials on a mesh.' },
  { kind: 'layered-material', title: 'Layered Material', text: 'Layer materials into a new one, with no mesh.' },
  { kind: 'image-material', title: 'Image Material', text: 'Make a material from one photo.' },
  { kind: 'graph', title: 'Graph', text: 'Build a material, a mask or a brush from nodes.' }
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
      @if (recentDocuments().length > 0) {
        <section class="flex w-full max-w-3xl flex-col gap-1" aria-labelledby="start-recent-documents-title">
          <h2 id="start-recent-documents-title" class="text-foreground-muted text-sm font-semibold">
            Recent documents
          </h2>
          @for (recent of recentDocuments(); track recent.path) {
            <button
              type="button"
              class="hover:bg-selection flex items-baseline gap-2 rounded px-3 py-1 text-start text-sm"
              data-testid="start-recent-document"
              [title]="recent.path"
              (click)="project.openRecent(recent)"
            >
              <span class="font-semibold">{{ recent.name }}</span>
              <span class="text-foreground-muted truncate">{{ recent.project }}</span>
            </button>
          }
        </section>
      }
      @if (recentProjects().length > 0) {
        <section class="flex w-full max-w-3xl flex-col gap-1" aria-labelledby="start-recent-projects-title">
          <h2 id="start-recent-projects-title" class="text-foreground-muted text-sm font-semibold">Recent projects</h2>
          <div class="flex flex-wrap gap-2">
            @for (recent of recentProjects(); track recent.path) {
              <button
                type="button"
                xuiButton
                size="sm"
                data-testid="start-recent-project"
                [title]="recent.path"
                (click)="project.open(recent.path)"
              >
                {{ recent.name }}
              </button>
            }
          </div>
        </section>
      }
      @if (samples().length > 0) {
        <section class="flex w-full max-w-3xl flex-col gap-1" aria-labelledby="start-samples-title">
          <h2 id="start-samples-title" class="text-foreground-muted text-sm font-semibold">Samples</h2>
          <div class="flex flex-wrap gap-2">
            @for (sample of samples(); track sample) {
              <button
                type="button"
                xuiButton
                size="sm"
                title="Copy this sample into the library as a project of your own, and open it"
                [attr.data-testid]="'start-sample-' + sample"
                [disabled]="making()"
                (click)="openSample(sample)"
              >
                {{ sample }}
              </button>
            }
          </div>
          @if (sampleFailure(); as failure) {
            <p class="text-error text-sm" data-testid="start-sample-failure">{{ failure }}</p>
          }
        </section>
      }
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
          <button type="button" xuiButton data-testid="start-open-library" (click)="openLibrary()">
            Open Library…
          </button>
        </div>
      }
      @for (path of recent(); track path) {
        <button
          type="button"
          class="hover:bg-selection px-3 py-1 text-sm"
          data-testid="start-recent"
          [title]="path"
          (click)="openRecent(path)"
        >
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
  private readonly preferences = inject(Preferences);
  /** This library's documents worked on last, newest first. */
  protected readonly recentDocuments = computed<readonly RecentDocument[]>(() => {
    const state = this.library.state();
    return state.kind === 'ready'
      ? this.preferences
          .recentDocuments()
          .filter(recent => recent.library === state.info.id)
          .slice(0, RecentShown)
      : [];
  });

  /** This library's projects worked on last, newest first (#162). */
  protected readonly recentProjects = computed(() => {
    const state = this.library.state();
    return state.kind === 'ready'
      ? recentProjectsOf(this.preferences.recentDocuments().filter(recent => recent.library === state.info.id))
      : [];
  });

  /** The samples Patina ships, to start from (#190). */
  protected readonly samples = signal<readonly string[]>([]);
  protected readonly making = signal(false);
  protected readonly sampleFailure = signal<string | null>(null);

  constructor() {
    void desktop()
      ?.recentLibraries()
      .then(paths => this.recent.set(paths));
    // Once a library is ready, not on every poll of it.
    const ready = computed(() => this.library.state().kind === 'ready');
    effect(() => {
      if (ready()) {
        void this.library.samples().then(names => this.samples.set(names));
      }
    });
  }

  /** Makes a project of a sample and opens it, so a first look has something to take apart. */
  protected async openSample(name: string): Promise<void> {
    this.making.set(true);
    this.sampleFailure.set(null);
    try {
      const project = await this.library.makeSample(name);
      await this.project.open(project.path);
    } catch (failure) {
      this.sampleFailure.set(messageOf(failure));
    } finally {
      this.making.set(false);
    }
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
