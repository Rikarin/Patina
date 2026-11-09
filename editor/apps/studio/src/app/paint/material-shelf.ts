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
import { type MaterialInfo, type StackTerm, unwrap } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiInputImports } from '@xui/input';
import { XuiNonIdealStateImports } from '@xui/non-ideal-state';
import { type XuiSegmentedOption, XuiSegmentedControlImports } from '@xui/segmented-control';
import { XuiSelectImports } from '@xui/select';
import { Commands } from '../core/commands';
import { labelIn } from '../core/labels';
import { messageOf } from '../core/messages';
import { Preferences } from '../core/preferences';
import { LibraryStore } from '../library/library-store';
import { ProjectStore } from '../library/project-store';
import { LibraryThumbnails } from '../library/thumbnails';
import { MaterialUse } from './material-use';

const SearchQuiet = 200;

export type ShelfView = 'list' | 'small' | 'large';

const Views: readonly XuiSegmentedOption<ShelfView>[] = [
  { value: 'list', label: 'List' },
  { value: 'small', label: 'Small' },
  { value: 'large', label: 'Large' }
];
const Everything = 'all';

/**
 * What a layer can be filled with, from anywhere in the Library, each with its picture. Click to add it, or drag it
 * into the stack (ADR 0012). The pictures are the Server's: it draws each into the preview cache and gives a hash.
 */
@Component({
  selector: 'ptn-material-shelf',
  imports: [
    XuiButtonImports,
    XuiCalloutImports,
    XuiInputImports,
    XuiNonIdealStateImports,
    XuiSegmentedControlImports,
    XuiSelectImports
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col' },
  template: `
    <div class="flex shrink-0 flex-col gap-1.5 p-2">
      <input
        class="w-full"
        xuiInput
        size="sm"
        type="search"
        autocomplete="off"
        aria-label="Search materials"
        data-testid="shelf-search"
        placeholder="Search by name, type, origin or path"
        [value]="search()"
        (input)="search.set(text($event))"
      />
      <div class="flex min-w-0 items-center gap-1.5">
        <xui-select
          class="min-w-0 flex-1 [&_button]:text-xs"
          size="sm"
          ariaLabel="Type"
          data-testid="shelf-role"
          [filterable]="false"
          [itemText]="roleLabel"
          [items]="roles()"
          [value]="role()"
          (valueChange)="role.set($event ?? everything)"
        />
        <xui-select
          class="min-w-0 flex-1 [&_button]:text-xs"
          size="sm"
          ariaLabel="From"
          data-testid="shelf-origin"
          [filterable]="false"
          [itemText]="originLabel"
          [items]="origins()"
          [value]="origin()"
          (valueChange)="origin.set($event ?? everything)"
        />
      </div>
      <xui-segmented-control
        class="min-w-0"
        size="sm"
        ariaLabel="Picture size"
        data-testid="shelf-view"
        [options]="views"
        [value]="view()"
        (valueChange)="choose($event ?? 'list')"
      />
    </div>
    @if (failure() ?? using.failure(); as reason) {
      <xui-callout color="error" compact data-testid="shelf-failure">{{ reason }}</xui-callout>
    }
    <ul
      aria-label="Materials"
      data-testid="shelf-materials"
      [attr.data-view]="view()"
      [class]="
        view() === 'list'
          ? 'min-h-0 flex-1 overflow-auto'
          : 'grid min-h-0 flex-1 content-start gap-1 overflow-auto px-2 pb-2 ' +
            (view() === 'small'
              ? 'grid-cols-[repeat(auto-fill,minmax(4.5rem,1fr))]'
              : 'grid-cols-[repeat(auto-fill,minmax(8rem,1fr))]')
      "
    >
      @for (material of shown(); track material.source) {
        @let picture = pictures.url(material.source);
        <li class="min-w-0">
          <button
            type="button"
            data-testid="shelf-material"
            [attr.data-source]="material.source"
            [attr.draggable]="material.role !== 'empty'"
            [class]="
              view() === 'list'
                ? 'hover:bg-selection flex w-full items-center gap-2 px-2 py-1 text-start text-sm'
                : 'hover:bg-selection flex w-full min-w-0 flex-col gap-1 rounded-sm p-1 text-start text-xs'
            "
            [disabled]="material.role === 'empty'"
            [title]="
              material.role === 'empty'
                ? 'This graph has no Output yet.'
                : material.name +
                  ' · ' +
                  describe(material) +
                  '\\nClick to add, drag into the stack, or Alt-click to paint with it'
            "
            (click)="use(material, $event)"
            (dragstart)="carry($event, material)"
            (dragend)="using.carried.set(null)"
          >
            <span
              class="well block shrink-0 overflow-hidden rounded-sm"
              [class]="view() === 'list' ? 'size-6' : 'aspect-square w-full'"
            >
              @if (picture) {
                <img
                  class="block size-full object-cover"
                  alt=""
                  decoding="async"
                  draggable="false"
                  data-testid="shelf-picture"
                  [src]="picture"
                />
              }
            </span>
            <span class="min-w-0 flex-1 truncate" [class.w-full]="view() !== 'list'">{{ material.name }}</span>
            @if (view() === 'list') {
              <span class="text-foreground-muted shrink-0 text-xs">{{ originOf(material) }}</span>
            }
          </button>
        </li>
      } @empty {
        <li class="col-span-full" data-testid="shelf-empty">
          @if (!project.current()) {
            <xui-non-ideal-state
              title="No project open"
              description="The shelf holds what a layer can be filled with: materials from anywhere in the library, once a project is open."
            >
              <button
                action
                type="button"
                xuiButton
                size="sm"
                [disabled]="!commands.enabled('newProject')"
                (click)="commands.run('newProject')"
              >
                New Project…
              </button>
            </xui-non-ideal-state>
          } @else if (search() || role() !== everything || origin() !== everything) {
            <xui-non-ideal-state
              title="Nothing matches"
              description="No material has every word, type and origin asked for."
            >
              <button action type="button" xuiButton size="sm" (click)="clearFilters()">Show Everything</button>
            </xui-non-ideal-state>
          } @else {
            <xui-non-ideal-state
              title="No materials yet"
              description="Material graphs and layered materials made in any project of this library appear here."
            />
          }
        </li>
      }
    </ul>
  `
})
export class MaterialShelf {
  private readonly library = inject(LibraryStore);
  protected readonly project = inject(ProjectStore);
  private readonly preferences = inject(Preferences);
  protected readonly commands = inject(Commands);
  protected readonly using = inject(MaterialUse);
  private readonly materials = signal<readonly MaterialInfo[]>([]);
  /** The pictures, from the one thumbnail service the Library's grid uses too. */
  protected readonly pictures = inject(LibraryThumbnails);

  /** The types and origins the Server names, which it also searches by, so no list of them is kept here. */
  private readonly roleTerms = signal<readonly StackTerm[]>([]);
  private readonly originTerms = signal<readonly StackTerm[]>([]);

  protected readonly everything = Everything;
  protected readonly roles = computed(() => [Everything, ...this.roleTerms().map(term => term.value)]);
  protected readonly origins = computed(() => [Everything, ...this.originTerms().map(term => term.value)]);
  protected readonly views = Views;
  protected readonly roleLabel = (role: string): string =>
    role === Everything ? 'All types' : labelIn(this.roleTerms(), role);
  protected readonly originLabel = (origin: string): string =>
    origin === Everything ? 'Everywhere' : labelIn(this.originTerms(), origin);

  protected readonly search = signal('');
  protected readonly role = signal(Everything);
  protected readonly origin = signal(Everything);
  /** How large the pictures are, which the person's preferences keep. */
  protected readonly view = this.preferences.shelfView;
  protected readonly failure = signal<string | null>(null);
  private searching: ReturnType<typeof setTimeout> | undefined;
  /** What the Server found for the search, narrowed to a type and an origin. The search itself is the Server's (#230). */
  protected readonly shown = computed(() => {
    const [role, origin] = [this.role(), this.origin()];
    return this.materials().filter(
      material =>
        (role === Everything || material.role === role) && (origin === Everything || material.origin === origin)
    );
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
    inject(DestroyRef).onDestroy(() => {
      clearTimeout(this.searching);
    });
  }

  protected text(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  protected choose(view: ShelfView): void {
    this.preferences.set({ shelfView: view });
  }

  /** Takes the search, type and origin away, so the whole shelf shows. */
  protected clearFilters(): void {
    this.search.set('');
    this.role.set(Everything);
    this.origin.set(Everything);
  }

  protected originOf(material: MaterialInfo): string {
    return material.project ?? this.originLabel(material.origin);
  }

  protected describe(material: MaterialInfo): string {
    return `${this.roleLabel(material.role)}, ${this.originOf(material)}`;
  }

  /** A click adds a material; with Alt held, a material the brush can lay becomes the brush's (#125). */
  protected use(material: MaterialInfo, event?: MouseEvent): void {
    void (event?.altKey ? this.using.paintWith(material) : this.using.use(material));
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
      const materials = list.items.filter(material => material.source !== open);
      this.materials.set(materials);
      this.roleTerms.set(list.roles);
      this.originTerms.set(list.origins);
      this.failure.set(null);
      // In the list's order, a few at a time, so the first rows fill while the rest are drawn.
      this.pictures.want(materials.filter(material => material.role !== 'empty').map(material => material.source));
    } catch (failure) {
      this.failure.set(messageOf(failure));
    }
  }
}
