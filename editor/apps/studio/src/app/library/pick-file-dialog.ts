import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  signal,
  untracked
} from '@angular/core';
import { type LibraryFile, unwrap } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiDialogImports } from '@xui/dialog';
import { XuiInputImports } from '@xui/input';
import { XuiNonIdealStateImports } from '@xui/non-ideal-state';
import { desktop } from '../core/desktop';
import { FilePicker, type PickRequest } from '../core/file-picker';
import { messageOf } from '../core/messages';
import { SystemStatus } from '../core/system-status';
import { LibraryStore } from './library-store';

/** How many pictures are drawn at once, so that a long list fills in from the top without asking for all of it. */
const DrawnAtOnce = 4;

/**
 * Choose a Picture, or a Mesh: what the library's projects hold, the project in front's first, found by name (#4). The
 * Server lists them, so a Server on another machine can be picked from too; From a File… opens this machine's dialog,
 * where there is one, and the Server brings what is chosen into the project.
 */
@Component({
  selector: 'ptn-pick-file',
  imports: [XuiButtonImports, XuiCalloutImports, XuiDialogImports, XuiInputImports, XuiNonIdealStateImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <xui-dialog
      size="md"
      [open]="!!picker.request()"
      [title]="request()?.title ?? ''"
      (openChange)="$event || cancel()"
    >
      <!-- One test id over the body and the footer, so its buttons are inside what a test finds as the dialog. -->
      <div class="contents" data-testid="pick-file">
        <xui-dialog-body class="flex min-h-80 flex-col gap-2">
          <input
            class="w-full"
            xuiInput
            type="search"
            cdkFocusInitial
            autocomplete="off"
            aria-label="Find by name"
            placeholder="Find by name or project"
            data-testid="pick-file-search"
            [value]="search()"
            (input)="search.set(text($event))"
          />
          @if (failure(); as reason) {
            <xui-callout color="error" compact>{{ reason }}</xui-callout>
          }
          <div class="flex max-h-96 min-h-0 flex-1 flex-col overflow-auto" role="listbox" aria-label="Files">
            @for (group of groups(); track group.project) {
              <span class="section-label mt-2 first:mt-0">{{ group.label }}</span>
              @for (file of group.files; track file.path) {
                <button
                  type="button"
                  role="option"
                  class="hover:bg-selection flex items-center gap-2 rounded px-2 py-1 text-start text-sm"
                  data-testid="pick-file-item"
                  [attr.aria-selected]="chosen() === file.path"
                  [class.bg-selection]="chosen() === file.path"
                  [title]="file.path"
                  (click)="chosen.set(file.path)"
                  (dblclick)="choose(file.path)"
                >
                  @if (request()?.kind === 'picture') {
                    <span class="well grid size-10 shrink-0 place-items-center overflow-hidden rounded-sm">
                      @if (pictures().get(file.path); as url) {
                        <img class="size-full object-cover" alt="" [src]="url" />
                      }
                    </span>
                  }
                  <span class="min-w-0 flex-1 truncate">{{ file.name }}</span>
                </button>
              }
            } @empty {
              @if (!loading()) {
                <xui-non-ideal-state
                  class="flex-1"
                  [title]="search() ? 'Nothing by that name' : 'The library has none yet'"
                  [description]="
                    canPick ? 'From a File… brings one in from this computer.' : 'Import one into a project first.'
                  "
                />
              }
            }
            @if (more()) {
              <p class="text-foreground-muted mt-2 text-xs">
                More are in the library than are listed: find one by name.
              </p>
            }
          </div>
        </xui-dialog-body>
        <xui-dialog-footer>
          @if (canPick) {
            <button type="button" xuiButton variant="ghost" data-testid="pick-file-machine" (click)="fromMachine()">
              From a File…
            </button>
          }
          <span class="flex-1"></span>
          <button type="button" xuiButton variant="ghost" (click)="cancel()">Cancel</button>
          <button
            type="button"
            xuiButton
            color="primary"
            data-testid="pick-file-choose"
            [disabled]="!chosen()"
            (click)="choose(chosen())"
          >
            Choose
          </button>
        </xui-dialog-footer>
      </div>
    </xui-dialog>
  `
})
export class PickFileDialog {
  protected readonly picker = inject(FilePicker);
  private readonly library = inject(LibraryStore);
  private readonly status = inject(SystemStatus);
  protected readonly canPick = desktop() !== undefined;

  protected readonly request = signal<PickRequest | null>(null);
  protected readonly files = signal<readonly LibraryFile[]>([]);
  protected readonly more = signal(false);
  protected readonly loading = signal(false);
  protected readonly failure = signal<string | null>(null);
  protected readonly search = signal('');
  protected readonly chosen = signal<string | null>(null);
  /** Each picture's object URL, once drawn. */
  protected readonly pictures = signal<ReadonlyMap<string, string>>(new Map());
  private generation = 0;

  /** The files that hold every word typed, by project: the one in front first, as the Server lists them. */
  protected readonly groups = computed(() => {
    const words = this.search().toLocaleLowerCase().split(/\s+/u).filter(Boolean);
    const own = this.request()?.project ?? null;
    const groups = new Map<string, LibraryFile[]>();
    for (const file of this.files()) {
      const said = `${file.name} ${file.project}`.toLocaleLowerCase();
      if (words.every(word => said.includes(word))) {
        groups.set(file.project, [...(groups.get(file.project) ?? []), file]);
      }
    }

    return [...groups].map(([project, files]) => ({
      project,
      label: project === own ? 'This project' : (project.split('/').pop() ?? project),
      files
    }));
  });

  constructor() {
    effect(() => {
      const request = this.picker.request();
      untracked(() => void this.open(request));
    });
    inject(DestroyRef).onDestroy(() => this.forget());
  }

  protected text(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  protected choose(path: string | null): void {
    if (path) {
      this.picker.answer({ library: path });
    }
  }

  protected cancel(): void {
    if (this.picker.request()) {
      this.picker.answer(null);
    }
  }

  protected async fromMachine(): Promise<void> {
    const bridge = desktop();
    const kind = this.request()?.kind;
    const file = await (kind === 'mesh'
      ? bridge?.openMesh(this.status.meshes())
      : bridge?.openImage(this.status.pictures()));
    if (file) {
      this.picker.answer({ machine: file });
    }
  }

  private async open(request: PickRequest | null): Promise<void> {
    const generation = ++this.generation;
    this.forget();
    this.request.set(request);
    this.search.set('');
    this.chosen.set(null);
    this.files.set([]);
    this.more.set(false);
    this.failure.set(null);
    if (!request) {
      return;
    }

    this.loading.set(true);
    try {
      const client = await this.library.api();
      const query = { kind: request.kind, ...(request.project ? { project: request.project } : {}) };
      const listed = unwrap(await client.GET('/api/v1/library/files', { params: { query } }));
      if (generation === this.generation) {
        this.files.set(listed.files);
        this.more.set(listed.more);
        if (request.kind === 'picture') {
          void this.draw(
            listed.files.map(file => file.path),
            generation
          );
        }
      }
    } catch (failure) {
      if (generation === this.generation) {
        this.failure.set(messageOf(failure));
      }
    } finally {
      if (generation === this.generation) {
        this.loading.set(false);
      }
    }
  }

  /** Draws the pictures a few at a time, top first, as their files: the library serves them, never in JSON. */
  private async draw(paths: readonly string[], generation: number): Promise<void> {
    const client = await this.library.api();
    const queue = [...paths];
    const next = async (): Promise<void> => {
      for (let path = queue.shift(); path !== undefined; path = queue.shift()) {
        try {
          const blob = unwrap(
            await client.GET('/api/v1/library/picture', { params: { query: { path } }, parseAs: 'blob' })
          );
          if (generation !== this.generation) {
            return;
          }

          const url = URL.createObjectURL(blob);
          this.pictures.update(drawn => new Map(drawn).set(path, url));
        } catch {
          // A picture that will not load keeps its name only.
        }
      }
    };
    await Promise.all(Array.from({ length: DrawnAtOnce }, next));
  }

  private forget(): void {
    for (const url of this.pictures().values()) {
      URL.revokeObjectURL(url);
    }

    this.pictures.set(new Map());
  }
}
