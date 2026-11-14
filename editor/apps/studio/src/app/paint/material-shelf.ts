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
import { provideIcons } from '@ng-icons/core';
import { matBookmarkBorder, matStar, matStarBorder } from '@ng-icons/material-icons/baseline';
import { type MaterialInfo, type StackTerm, unwrap } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiInputImports } from '@xui/input';
import { XuiMenuImports } from '@xui/menu';
import { XuiNonIdealStateImports } from '@xui/non-ideal-state';
import { XuiSegmentedControlImports, type XuiSegmentedOption } from '@xui/segmented-control';
import { XuiSelectImports } from '@xui/select';
import { Commands } from '../core/commands';
import { IconImports } from '../core/icons';
import { labelIn } from '../core/labels';
import { messageOf } from '../core/messages';
import { Preferences, type ShelfSearch, sameSearch } from '../core/preferences';
import { LibraryStore } from '../library/library-store';
import { ProjectStore } from '../library/project-store';
import { LibraryThumbnails } from '../library/thumbnails';
import { BrushStore } from './brush-store';
import { MaterialUse } from './material-use';

const SearchQuiet = 200;

export type ShelfView = 'list' | 'small' | 'large';

const Views: readonly XuiSegmentedOption<ShelfView>[] = [
  { value: 'list', label: 'List' },
  { value: 'small', label: 'Small' },
  { value: 'large', label: 'Large' }
];
const Everything = 'all';
/** The origin that is no place: the materials starred on this machine, from wherever they are. */
const Favourites = 'favourites';
/** The type of a brush or tool preset, which a click takes up rather than adds (#124). */
const Preset = 'preset';

/**
 * What a layer can be filled with, from anywhere in the Library, each with its picture. Click to add it, or drag it
 * into the stack (ADR 0012). The pictures are the Server's: it draws each into the preview cache and gives a hash.
 */
@Component({
  selector: 'ptn-material-shelf',
  imports: [
    IconImports,
    XuiButtonImports,
    XuiCalloutImports,
    XuiInputImports,
    XuiMenuImports,
    XuiNonIdealStateImports,
    XuiSegmentedControlImports,
    XuiSelectImports
  ],
  providers: [provideIcons({ matBookmarkBorder, matStar, matStarBorder })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col' },
  template: `
    <div class="flex shrink-0 flex-col gap-1.5 p-2">
      <div class="flex min-w-0 items-center gap-1">
        <input
          class="min-w-0 flex-1"
          xuiInput
          size="sm"
          type="search"
          autocomplete="off"
          aria-label="Search materials"
          data-testid="shelf-search"
          placeholder="Search by name, tag, type, origin or path"
          [value]="search()"
          (input)="search.set(text($event))"
        />
        <button
          type="button"
          xuiButton
          size="sm"
          variant="ghost"
          aria-label="Kept searches"
          title="Searches kept to come back to"
          data-testid="shelf-searches"
          [xuiMenuTriggerFor]="searchMenu"
        >
          <ng-icon xui size="sm" name="matBookmarkBorder" />
        </button>
      </div>
      <div class="flex min-w-0 items-center gap-1.5">
        <xui-select
          class="min-w-0 flex-1 [&_button]:text-xs"
          size="sm"
          aria-label="Type"
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
          aria-label="From"
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
        aria-label="Picture size"
        data-testid="shelf-view"
        [options]="views"
        [value]="view()"
        (valueChange)="choose($event ?? 'list')"
      />
    </div>
    @if (chosen().size > 0) {
      <div class="chrome-bar flex shrink-0 items-center gap-1.5 px-2 py-1 text-xs" data-testid="shelf-chosen">
        <span class="min-w-0 flex-1 truncate">{{ chosenSaid() }}</span>
        <button type="button" xuiButton size="sm" data-testid="shelf-add-chosen" (click)="addChosen()">Add All</button>
        <button type="button" xuiButton size="sm" variant="ghost" (click)="chosen.set(empty)">Clear</button>
      </div>
    }
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
        @let starred = favourites().has(material.source);
        <li class="group relative flex min-w-0 items-center">
          <button
            type="button"
            data-testid="shelf-material"
            [attr.data-source]="material.source"
            [attr.draggable]="material.role !== 'empty' && material.role !== 'preset'"
            [class]="
              view() === 'list'
                ? 'hover:bg-selection flex w-full min-w-0 items-center gap-2 px-2 py-1 text-start text-sm'
                : 'hover:bg-selection flex w-full min-w-0 flex-col gap-1 rounded-sm p-1 text-start text-xs'
            "
            [disabled]="material.role === 'empty'"
            [attr.aria-selected]="chosen().has(material.source)"
            [class.bg-selection]="chosen().has(material.source)"
            [title]="
              material.role === 'empty'
                ? 'This graph has no Output yet.'
                : material.name +
                  ' · ' +
                  describe(material) +
                  (material.role === 'preset'
                    ? '\\nClick to paint with this preset'
                    : '\\nClick to add, Ctrl-click to choose several, drag into the stack, or Alt-click to paint with it')
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
          @if (material.role !== 'empty') {
            <button
              type="button"
              data-testid="shelf-star"
              [class]="
                'text-foreground-muted hover:text-foreground focus-visible:opacity-100 ' +
                (view() === 'list'
                  ? 'shrink-0 px-1'
                  : 'chrome-bar absolute end-1.5 top-1.5 rounded-sm p-0.5 leading-none')
              "
              [class.opacity-0]="!starred"
              [class.group-hover:opacity-100]="!starred"
              [attr.aria-label]="(starred ? 'Unstar ' : 'Star ') + material.name"
              [attr.aria-pressed]="starred"
              [title]="
                starred ? 'Takes the star away' : 'Keeps this material at the top of the shelf, and in Favourites'
              "
              (click)="preferences.toggleFavouriteMaterial(material.source)"
            >
              <ng-icon xui size="sm" [name]="starred ? 'matStar' : 'matStarBorder'" />
            </button>
          }
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

    <ng-template #searchMenu>
      <xui-menu aria-label="Kept searches" data-testid="shelf-searches-menu">
        @for (kept of preferences.shelfSearches(); track $index) {
          <button
            type="button"
            xuiMenuItem
            data-testid="shelf-kept-search"
            [title]="'Searches for this again'"
            (triggered)="apply(kept)"
          >
            {{ searchName(kept) }}
          </button>
        } @empty {
          <span class="text-foreground-muted px-2 py-1 text-xs">None kept yet</span>
        }
        <xui-menu-divider />
        @if (keptNow()) {
          <button type="button" xuiMenuItem data-testid="shelf-forget-search" (triggered)="forget()">
            Forget This Search
          </button>
        } @else {
          <button
            type="button"
            xuiMenuItem
            data-testid="shelf-keep-search"
            [disabled]="!searching()"
            (triggered)="keep()"
          >
            Keep This Search
          </button>
        }
      </xui-menu>
    </ng-template>
  `
})
export class MaterialShelf {
  private readonly library = inject(LibraryStore);
  protected readonly project = inject(ProjectStore);
  protected readonly preferences = inject(Preferences);
  protected readonly commands = inject(Commands);
  protected readonly using = inject(MaterialUse);
  private readonly brushes = inject(BrushStore);
  private readonly materials = signal<readonly MaterialInfo[]>([]);
  /** The pictures, from the one thumbnail service the Library's grid uses too. */
  protected readonly pictures = inject(LibraryThumbnails);

  /** The types and origins the Server names, which it also searches by, so no list of them is kept here. */
  private readonly roleTerms = signal<readonly StackTerm[]>([]);
  private readonly originTerms = signal<readonly StackTerm[]>([]);

  protected readonly everything = Everything;
  protected readonly roles = computed(() => [Everything, ...this.roleTerms().map(term => term.value)]);
  protected readonly origins = computed(() => [Everything, Favourites, ...this.originTerms().map(term => term.value)]);
  protected readonly favourites = computed(() => new Set(this.preferences.favouriteMaterials()));
  protected readonly views = Views;
  protected readonly roleLabel = (role: string): string =>
    role === Everything ? 'All types' : labelIn(this.roleTerms(), role);
  protected readonly originLabel = (origin: string): string =>
    origin === Everything ? 'Everywhere' : origin === Favourites ? 'Favourites' : labelIn(this.originTerms(), origin);

  protected readonly search = signal('');
  protected readonly role = signal(Everything);
  protected readonly origin = signal(Everything);
  /** How large the pictures are, which the person's preferences keep. */
  protected readonly view = this.preferences.shelfView;
  protected readonly failure = signal<string | null>(null);
  private searchTimer: ReturnType<typeof setTimeout> | undefined;
  protected readonly empty: ReadonlySet<string> = new Set();
  /** Materials chosen with Ctrl or the platform key held, by source, to add together (#203). */
  protected readonly chosen = signal<ReadonlySet<string>>(this.empty);
  protected readonly chosenSaid = computed(() => {
    const count = this.chosen().size;
    return `${count} ${count === 1 ? 'material' : 'materials'} chosen`;
  });
  /** The search, type and origin as they stand, which the shelf can keep. */
  private readonly current = computed<ShelfSearch>(() => ({
    search: this.search().trim(),
    role: this.role(),
    origin: this.origin()
  }));
  /** Whether anything narrows the shelf, so that there is a search to keep. */
  protected readonly searching = computed(() => {
    const now = this.current();
    return now.search !== '' || now.role !== Everything || now.origin !== Everything;
  });
  /** Whether the search as it stands is one kept. */
  protected readonly keptNow = computed(() =>
    this.preferences.shelfSearches().some(kept => sameSearch(kept, this.current()))
  );
  /**
   * What the Server found for the search, narrowed to a type and an origin, the starred ones first (#203). The search
   * itself is the Server's (#230).
   */
  protected readonly shown = computed(() => {
    const [role, origin, favourites] = [this.role(), this.origin(), this.favourites()];
    const found = this.materials().filter(
      material =>
        (role === Everything || material.role === role) &&
        (origin === Everything ||
          material.origin === origin ||
          (origin === Favourites && favourites.has(material.source)))
    );
    return [
      ...found.filter(material => favourites.has(material.source)),
      ...found.filter(material => !favourites.has(material.source))
    ];
  });

  constructor() {
    effect(() => {
      const project = this.project.current()?.path;
      const search = this.search().trim();
      this.library.changes();
      clearTimeout(this.searchTimer);
      if (project) {
        // A search reaches materials past what the list holds, so it asks the Server, though not at every key.
        this.searchTimer = setTimeout(() => untracked(() => void this.load(project, search)), search ? SearchQuiet : 0);
      }
    });
    inject(DestroyRef).onDestroy(() => {
      clearTimeout(this.searchTimer);
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

  /** A kept search as the menu names it: its words, then its type and origin when it narrows by them. */
  protected searchName(kept: ShelfSearch): string {
    const words = kept.search ? `“${kept.search}”` : '';
    const narrowed = [kept.role, kept.origin]
      .map((value, index) =>
        value === Everything ? '' : index === 0 ? this.roleLabel(value) : this.originLabel(value)
      )
      .filter(Boolean);
    return [words, ...narrowed].filter(Boolean).join(' · ');
  }

  /** Searches the shelf as a kept search says. */
  protected apply(kept: ShelfSearch): void {
    this.search.set(kept.search);
    this.role.set(kept.role);
    this.origin.set(kept.origin);
  }

  /** Keeps the search as it stands, last in the menu. */
  protected keep(): void {
    this.preferences.set({ shelfSearches: [...this.preferences.shelfSearches(), this.current()] });
  }

  /** Forgets the search as it stands. */
  protected forget(): void {
    const now = this.current();
    this.preferences.set({ shelfSearches: this.preferences.shelfSearches().filter(kept => !sameSearch(kept, now)) });
  }

  /** Adds every chosen material, in the shelf's order, one after the other, as clicks on each would. */
  protected async addChosen(): Promise<void> {
    const chosen = this.chosen();
    this.chosen.set(this.empty);
    for (const material of this.shown().filter(item => chosen.has(item.source))) {
      await this.using.use(material);
    }
  }

  protected originOf(material: MaterialInfo): string {
    return material.project ?? this.originLabel(material.origin);
  }

  /** Its type and origin, then what its graph says it makes and the words it is tagged with, when it says. */
  protected describe(material: MaterialInfo): string {
    const tags = material.tags?.length ? `\nTags: ${material.tags.join(', ')}` : '';
    const about = material.description ? `\n${material.description}` : '';
    return `${this.roleLabel(material.role)}, ${this.originOf(material)}${about}${tags}`;
  }

  /**
   * A click adds a material; with Alt held, a material the brush can lay becomes the brush's (#125); with Ctrl or the
   * platform key held, it is chosen, or no longer chosen, to add with others (#203).
   */
  protected use(material: MaterialInfo, event?: MouseEvent): void {
    if ((event?.ctrlKey || event?.metaKey) && material.role !== Preset) {
      const next = new Set(this.chosen());
      if (!next.delete(material.source)) {
        next.add(material.source);
      }

      this.chosen.set(next);
      return;
    }

    if (material.role === Preset) {
      void this.brushes.useSource(material.source);
      return;
    }

    void (event?.altKey ? this.using.paintWith(material) : this.using.use(material));
  }

  protected carry(event: DragEvent, material: MaterialInfo): void {
    if (material.role === Preset) {
      event.preventDefault();
      return;
    }

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
      // A preset is a brush and not a picture, so it has none to draw.
      this.pictures.want(
        materials.filter(material => material.role !== 'empty' && material.role !== Preset).map(m => m.source)
      );
    } catch (failure) {
      this.failure.set(messageOf(failure));
    }
  }
}
