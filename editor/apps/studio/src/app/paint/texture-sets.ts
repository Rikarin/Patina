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
import { XuiButtonImports } from '@xui/button';
import { XuiContextMenuImports } from '@xui/context-menu';
import { XuiMenuImports } from '@xui/menu';
import { XuiNonIdealStateImports } from '@xui/non-ideal-state';
import { XuiSwitchImports } from '@xui/switch';
import { Commands } from '../core/commands';
import { MenuKeys } from '../core/menu-keys';
import { ProjectStore } from '../library/project-store';
import { IsolationStore } from '../viewport/isolation-store';
import { PaintStore } from './paint-store';

/**
 * The model's texture sets: a document per material, of which one is being painted, each shown alone on request and
 * marked on the model while it is pointed at. A set's own settings, its size, UV set and height to normal, are in Properties' document section (#201).
 */
@Component({
  selector: 'ptn-texture-sets',
  imports: [
    XuiButtonImports,
    XuiContextMenuImports,
    XuiMenuImports,
    XuiNonIdealStateImports,
    XuiSwitchImports,
    MenuKeys
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col overflow-auto p-1' },
  template: `
    <div
      role="listbox"
      aria-label="Texture sets"
      data-testid="texture-sets"
      ptnMenuKeys
      [xuiContextMenuTriggerFor]="setMenu"
      (aiming)="aim($event)"
      (contextmenu)="aim($event)"
    >
      @for (set of sets(); track set.path) {
        <div
          class="border-hairline flex h-7 w-full items-center border-b"
          [attr.data-set]="set.path"
          (pointerenter)="markSet(set.material)"
          (pointerleave)="isolation.unmark()"
          (focusin)="markSet(set.material)"
          (focusout)="isolation.unmark()"
        >
          <button
            type="button"
            role="option"
            class="hover:bg-selection flex h-full min-w-0 flex-1 items-center gap-2 px-2 text-start text-sm"
            [attr.aria-selected]="set.path === front()"
            [attr.data-testid]="'texture-set-' + set.name"
            [class.outline]="set.path === front()"
            (click)="choose(set.path)"
          >
            <span class="min-w-0 flex-1 truncate">{{ set.name }}</span>
            @if (tileOf(set); as udim) {
              <span
                class="readout text-foreground-muted shrink-0 text-xs"
                data-testid="texture-set-tile"
                title="The UDIM tile this texture set's layout sits on, which its exported files are named by"
                >{{ udim }}</span
              >
            }
            @if (set.path === front() && size(); as side) {
              <span class="readout text-foreground-muted shrink-0 text-xs" data-testid="texture-set-size">{{
                side
              }}</span>
            }
          </button>
          @if (set.material) {
            <button
              type="button"
              class="text-foreground-muted grid size-6 shrink-0 place-items-center text-xs"
              [attr.data-testid]="'texture-set-isolate-' + set.name"
              [attr.aria-label]="'Show ' + set.name + ' alone in the viewport'"
              [attr.aria-pressed]="isolatedMaterial() === set.material"
              [class.bg-selection]="isolatedMaterial() === set.material"
              [title]="isolatedMaterial() === set.material ? 'Show the whole model' : 'Show only this texture set'"
              (click)="isolation.toggleMaterial(set.material)"
            >
              ◎
            </button>
          }
        </div>
      } @empty {
        <xui-non-ideal-state
          title="No texture sets"
          description="A texturing document's mesh has a texture set for each of its materials, each painted on its own. Open a texturing document to see them."
          data-testid="texture-sets-empty"
        />
      }
    </div>
    <ng-template #setMenu>
      <xui-menu data-testid="texture-set-menu" [attr.aria-label]="aimed()?.name ?? 'Texture sets'">
        @if (aimed(); as set) {
          @if (set.path !== front()) {
            <button type="button" xuiMenuItem data-testid="texture-set-menu-open" (triggered)="choose(set.path)">
              Paint {{ set.name }}
            </button>
          }
          @if (set.material) {
            <button
              type="button"
              xuiMenuItem
              data-testid="texture-set-menu-alone"
              (triggered)="isolation.toggleMaterial(set.material)"
            >
              {{ isolatedMaterial() === set.material ? 'Show the Whole Model' : 'Show Alone' }}
            </button>
          }
        }
        @if (isolated() && isolatedMaterial() !== aimed()?.material) {
          <button type="button" xuiMenuItem data-testid="texture-set-menu-all" (triggered)="isolation.showAll()">
            Show the Whole Model
          </button>
        }
        <xui-menu-divider />
        @for (command of setCommands; track command) {
          <button
            type="button"
            xuiMenuItem
            [attr.data-testid]="'texture-set-menu-' + command"
            [disabled]="!commands.enabled(command)"
            (triggered)="commands.run(command)"
          >
            {{ commands.label(command) }}
          </button>
        }
      </xui-menu>
    </ng-template>
    @if (parts().length > 1) {
      <div
        class="flex flex-col px-2 py-1"
        role="group"
        aria-label="Mesh parts to show alone"
        data-testid="texture-set-parts"
      >
        <div class="flex items-center gap-2">
          <span class="text-foreground-muted min-w-0 flex-1 truncate text-xs">Isolate parts</span>
          @if (isolated()) {
            <button
              type="button"
              xuiButton
              size="sm"
              variant="ghost"
              data-testid="texture-set-show-all"
              (click)="isolation.showAll()"
            >
              Show All
            </button>
          }
        </div>
        @for (part of parts(); track part) {
          <div
            class="flex h-7 items-center gap-2 text-xs"
            [attr.data-testid]="'texture-set-part-row-' + part"
            (pointerenter)="isolation.mark({ parts: [part], materials: [] })"
            (pointerleave)="isolation.unmark()"
            (focusin)="isolation.mark({ parts: [part], materials: [] })"
            (focusout)="isolation.unmark()"
          >
            <span class="min-w-0 flex-1 truncate" [title]="part">{{ part }}</span>
            <xui-switch
              [aria-label]="'Show ' + part + ' alone'"
              [attr.data-testid]="'texture-set-part-' + part"
              [checked]="isolation.isolation().parts.includes(part)"
              (checkedChange)="isolation.togglePart(part)"
            />
          </div>
        }
      </div>
    }
  `
})
export class TextureSets {
  private readonly project = inject(ProjectStore);
  protected readonly store = inject(PaintStore);
  protected readonly isolation = inject(IsolationStore);
  protected readonly commands = inject(Commands);
  /** What a set's menu offers besides showing it: the dialogs that work on the sets. */
  protected readonly setCommands = ['bake', 'export', 'projectSettings'] as const;
  /** The set a menu is about. */
  protected readonly aimed = signal<{ path: string; name: string; material: string | null } | null>(null);
  /** The named meshes of the open set, which can be shown alone. */
  protected readonly parts = signal<readonly string[]>([]);
  protected readonly isolated = computed(() => {
    const held = this.isolation.isolation();
    return held.parts.length > 0 || held.materials.length > 0;
  });
  /** The one material shown alone, if that is what the viewport shows. */
  protected readonly isolatedMaterial = computed(() => {
    const held = this.isolation.isolation();
    return held.parts.length === 0 && held.materials.length === 1 ? held.materials[0] : null;
  });

  constructor() {
    // A list that goes while a row is pointed at would leave its mark on the model.
    inject(DestroyRef).onDestroy(() => void this.isolation.unmark());
    effect(() => {
      if (this.store.state()?.id) {
        untracked(
          () =>
            void this.store.meshParts().then(
              parts => this.parts.set(parts),
              () => this.parts.set([])
            )
        );
      } else {
        this.parts.set([]);
      }
    });
  }
  protected readonly front = computed(() => this.project.document()?.path);
  /** A mesh with one material has one set, which is the document itself. */
  protected readonly sets = computed(() => {
    const named = this.project.textureSets();
    const document = this.project.document();
    return named.length > 0
      ? named.map(set => ({
          path: set.path,
          name: set.material ?? set.name,
          material: set.material ?? null,
          udim: set.udim
        }))
      : document?.kind === 'texturing'
        ? [{ path: document.path, name: document.name, material: null, udim: document.udim }]
        : [];
  });
  /** The open set's UDIM tile as its stack has it, which is newer than the project's list while it is open. */
  protected readonly tile = computed(() => this.store.state()?.udim);

  /**
   * A set's UDIM tile, said only when it is not the first, where every set sits until its layout moves: the open set's
   * from its stack, and every other set's from the project's list (#289).
   */
  protected tileOf(set: { path: string; udim?: number }): string | null {
    const udim = set.path === this.front() ? (this.tile() ?? set.udim) : set.udim;
    return udim !== undefined && udim !== 1001 ? `UDIM ${udim}` : null;
  }
  protected readonly size = computed(() => {
    const state = this.store.state();
    return state ? `${state.width}×${state.height}` : null;
  });

  /** Says which set the menu that is about to open is for: the row under the pointer, or the one with focus. */
  protected aim(event: { target: EventTarget | null }): void {
    const path = (event.target as HTMLElement).closest<HTMLElement>('[data-set]')?.dataset['set'] ?? null;
    this.aimed.set(this.sets().find(set => set.path === path) ?? null);
  }

  /**
   * Marks the set pointed at on the model (#136). A model of one material has one set, which is the whole model and
   * needs no mark.
   */
  protected markSet(material: string | null): void {
    void (material ? this.isolation.mark({ parts: [], materials: [material] }) : this.isolation.unmark());
  }

  protected choose(path: string): void {
    const project = this.project.current();
    if (project) {
      void this.project.openDocument(project.path, path);
    }
  }
}
