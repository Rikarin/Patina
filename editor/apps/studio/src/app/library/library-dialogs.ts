import { ChangeDetectionStrategy, Component, computed, effect, inject, linkedSignal, signal } from '@angular/core';
import { type ProjectMeshInfo, unwrap } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiDialogImports } from '@xui/dialog';
import { XuiFormFieldImports } from '@xui/form-field';
import { XuiInputImports } from '@xui/input';
import { XuiTagInputImports } from '@xui/tag-input';
import { desktop } from '../core/desktop';
import { messageOf } from '../core/messages';
import { SystemStatus } from '../core/system-status';
import { LibraryActions } from './library-actions';
import { LibraryStore } from './library-store';

/** The kinds a project can be given another document of, and what each is for. */
const DocumentKinds = [
  { value: 'layered-material', label: 'Layered Material: layers with no mesh' },
  { value: 'graph', label: 'Graph: nodes' },
  { value: 'image-material', label: 'Image Material: from one photo' },
  { value: 'texturing', label: 'Texturing: paint a mesh of the project' }
];

/**
 * The dialogs every list of the Library shares: rename, tags, delete to the bin, and a new document in a project. Each
 * opens when {@link LibraryActions} names what it is about, and says what the Server refused, if it did.
 */
@Component({
  selector: 'ptn-library-dialogs',
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
    <xui-dialog size="sm" title="Rename" [open]="actions.renaming() !== null" (openChange)="$event || close()">
      <form (submit)="rename($event)">
        <xui-dialog-body class="flex flex-col gap-3">
          <xui-form-field label="Name">
            <input
              class="w-full"
              xuiInput
              cdkFocusInitial
              autocomplete="off"
              data-testid="library-rename-name"
              [value]="name()"
              (input)="name.set(text($event))"
            />
          </xui-form-field>
          <p class="text-foreground-muted text-xs">Its file keeps its name, so nothing that uses it notices.</p>
          @if (actions.failure(); as reason) {
            <xui-callout color="error" compact>{{ reason }}</xui-callout>
          }
        </xui-dialog-body>
        <xui-dialog-footer>
          <button type="button" xuiButton variant="ghost" (click)="close()">Cancel</button>
          <button type="submit" xuiButton color="primary" data-testid="library-rename-save" [disabled]="!name().trim()">
            Rename
          </button>
        </xui-dialog-footer>
      </form>
    </xui-dialog>

    <xui-dialog size="sm" title="Tags" [open]="actions.tagging() !== null" (openChange)="$event || close()">
      <xui-dialog-body class="flex flex-col gap-3" data-testid="library-tags-dialog">
        <p>{{ actions.tagging()?.name }} can be found by these, and the Library can show only what has one.</p>
        <xui-tag-input placeholder="Type a tag, then Enter" data-testid="library-tags-input" [(values)]="tags" />
        @if (actions.failure(); as reason) {
          <xui-callout color="error" compact>{{ reason }}</xui-callout>
        }
      </xui-dialog-body>
      <xui-dialog-footer>
        <button type="button" xuiButton variant="ghost" (click)="close()">Cancel</button>
        <button type="button" xuiButton color="primary" data-testid="library-tags-save" (click)="saveTags()">
          Save
        </button>
      </xui-dialog-footer>
    </xui-dialog>

    <xui-dialog
      size="sm"
      [title]="actions.deleting()?.kind === 'project' ? 'Delete project' : 'Delete document'"
      [open]="actions.deleting() !== null"
      (openChange)="$event || close()"
    >
      <xui-dialog-body data-testid="library-delete">
        <p>
          Move {{ actions.deleting()?.name }} to the bin? It stays there, with its paint, until you restore it or empty
          the bin.
        </p>
        @if (actions.failure(); as reason) {
          <xui-callout class="mt-2" color="error" compact>{{ reason }}</xui-callout>
        }
      </xui-dialog-body>
      <xui-dialog-footer>
        <button type="button" xuiButton variant="ghost" (click)="close()">Cancel</button>
        <button type="button" xuiButton color="error" data-testid="library-delete-confirm" (click)="delete()">
          Move to Bin
        </button>
      </xui-dialog-footer>
    </xui-dialog>

    <xui-dialog size="sm" title="New document" [open]="actions.adding() !== null" (openChange)="$event || close()">
      <form (submit)="add($event)">
        <xui-dialog-body class="flex flex-col gap-3">
          <xui-form-field label="Name">
            <input
              class="w-full"
              xuiInput
              cdkFocusInitial
              autocomplete="off"
              data-testid="new-document-name"
              [value]="newName()"
              (input)="newName.set(text($event))"
            />
          </xui-form-field>
          <label class="flex flex-col gap-1">
            <span class="section-label">Kind</span>
            <select class="well h-7 w-full px-1" data-testid="new-document-kind" (change)="kind.set(text($event))">
              @for (option of kinds; track option.value) {
                <option
                  [value]="option.value"
                  [selected]="option.value === kind()"
                  [disabled]="option.value === 'texturing' && meshes().length === 0"
                >
                  {{ option.label }}
                </option>
              }
            </select>
          </label>
          @if (meshes().length === 0) {
            <p class="text-foreground-muted text-xs">
              Texturing paints a mesh of the project, and this one has none yet: add one in Project Settings.
            </p>
          }
          @if (kind() === 'texturing' && meshes().length > 1) {
            <label class="flex flex-col gap-1">
              <span class="section-label">Mesh</span>
              <select class="well h-7 w-full px-1" data-testid="new-document-mesh" (change)="mesh.set(text($event))">
                @for (choice of meshes(); track choice.id) {
                  <option [value]="choice.id" [selected]="choice.id === mesh()">{{ fileName(choice.path) }}</option>
                }
              </select>
            </label>
          }
          @if (kind() === 'image-material') {
            <div class="flex items-center gap-2">
              <span class="min-w-0 flex-1 truncate">{{ fileName(image()) || 'No picture yet' }}</span>
              <button type="button" xuiButton size="sm" [disabled]="!canPick" (click)="pickImage()">Choose…</button>
            </div>
          }
          @if (actions.failure(); as reason) {
            <xui-callout color="error" compact>{{ reason }}</xui-callout>
          }
        </xui-dialog-body>
        <xui-dialog-footer>
          <button type="button" xuiButton variant="ghost" (click)="close()">Cancel</button>
          <button type="submit" xuiButton color="primary" data-testid="new-document-create" [disabled]="!canAdd()">
            Create
          </button>
        </xui-dialog-footer>
      </form>
    </xui-dialog>
  `
})
export class LibraryDialogs {
  protected readonly actions = inject(LibraryActions);
  private readonly library = inject(LibraryStore);
  private readonly status = inject(SystemStatus);

  protected readonly kinds = DocumentKinds;
  protected readonly canPick = desktop() !== undefined;
  protected readonly name = linkedSignal(() => this.actions.renaming()?.name ?? '');
  protected readonly tags = linkedSignal<string[]>(() => [...(this.actions.tagging()?.tags ?? [])]);
  protected readonly newName = signal('');
  protected readonly kind = signal('layered-material');
  protected readonly image = signal('');
  protected readonly meshes = signal<readonly ProjectMeshInfo[]>([]);
  protected readonly mesh = linkedSignal(() => this.meshes().find(choice => choice.id)?.id ?? '');
  private readonly busy = signal(false);
  protected readonly canAdd = computed(
    () =>
      this.newName().trim() !== '' &&
      !this.busy() &&
      (this.kind() !== 'texturing' || this.mesh() !== '') &&
      (this.kind() !== 'image-material' || this.image() !== '')
  );

  constructor() {
    // A project's meshes are what a texturing document can go on, so they are read when the dialog opens.
    effect(() => {
      const project = this.actions.adding();
      this.meshes.set([]);
      this.newName.set('');
      this.image.set('');
      if (project !== null) {
        void this.readMeshes(project);
      }
    });
  }

  protected text(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  protected fileName(path: string): string {
    return path.split(/[\\/]/u).pop() ?? path;
  }

  protected close(): void {
    this.actions.renaming.set(null);
    this.actions.tagging.set(null);
    this.actions.deleting.set(null);
    this.actions.adding.set(null);
    this.actions.failure.set(null);
  }

  protected async rename(event: Event): Promise<void> {
    event.preventDefault();
    const target = this.actions.renaming();
    const name = this.name().trim();
    if (target && name && (await this.actions.rename(target, name))) {
      this.close();
    }
  }

  protected async saveTags(): Promise<void> {
    const target = this.actions.tagging();
    if (target && (await this.actions.setTags(target, this.tags()))) {
      this.close();
    }
  }

  protected async delete(): Promise<void> {
    const target = this.actions.deleting();
    if (target && (await this.actions.delete(target))) {
      this.close();
    }
  }

  protected async add(event: Event): Promise<void> {
    event.preventDefault();
    const project = this.actions.adding();
    if (project === null || !this.canAdd()) {
      return;
    }

    this.busy.set(true);
    try {
      const done = await this.actions.addDocument(project, this.kind(), this.newName().trim(), {
        ...(this.kind() === 'texturing' ? { mesh: this.mesh() } : {}),
        ...(this.kind() === 'image-material' ? { image: this.image() } : {})
      });
      if (done) {
        this.close();
      }
    } finally {
      this.busy.set(false);
    }
  }

  protected async pickImage(): Promise<void> {
    const image = await desktop()?.openImage(this.status.pictures());
    if (image) {
      this.image.set(image);
    }
  }

  private async readMeshes(project: string): Promise<void> {
    try {
      const client = await this.library.api();
      const contents = unwrap(await client.GET('/api/v1/library/project', { params: { query: { path: project } } }));
      if (this.actions.adding() === project) {
        this.meshes.set(contents.meshes.filter(choice => choice.id));
      }
    } catch (failure) {
      this.actions.failure.set(messageOf(failure));
    }
  }
}
