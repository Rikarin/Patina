import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { unwrap } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiDialogImports } from '@xui/dialog';
import { XuiSelectImports } from '@xui/select';
import { LibraryStore } from './library-store';
import { ProjectStore } from './project-store';

/** What a set on a vanished material is offered: a new material no other set holds, or staying as it is. */
const Keep = '';

/**
 * Reload Mesh, for a mesh file that changed (#89). It says what reloading costs, and when the new mesh's materials are
 * not the old one's, offers each texture set on a material that vanished the new materials no set holds, so a renamed
 * material keeps its set's layers, paint and bake rather than leaving the set behind and making an empty one. Loaded
 * when first asked for, since most sessions never reload.
 */
@Component({
  selector: 'ptn-mesh-reload-dialog',
  imports: [XuiButtonImports, XuiDialogImports, XuiSelectImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <xui-dialog
      size="sm"
      title="Reload Mesh"
      [open]="project.reloading() !== null"
      (openChange)="$event || project.resolveReloading(false)"
    >
      <xui-dialog-body data-testid="mesh-reload">
        <p>
          The mesh file changed. Open documents close and lose their undo history. Bakes made on the old mesh go out of
          date.
        </p>
        @if (vanished().length > 0 && appeared().length > 0) {
          <p class="text-foreground-muted mt-2 text-xs">
            These texture sets are on materials the new mesh does not have. Choose the new material each one becomes.
          </p>
          <dl class="mt-1 grid grid-cols-[auto_1fr] items-center gap-x-2 gap-y-1 text-sm" data-testid="mesh-reassign">
            @for (set of vanished(); track set) {
              <dt class="truncate">{{ set }}</dt>
              <dd>
                <xui-select
                  size="sm"
                  [ariaLabel]="'New material for ' + set"
                  [attr.data-testid]="'mesh-reassign-' + set"
                  [filterable]="false"
                  [items]="choices()"
                  [itemText]="choiceText"
                  [value]="chosen()[set] ?? keep"
                  (valueChange)="choose(set, $event)"
                />
              </dd>
            }
          </dl>
        }
      </xui-dialog-body>
      <xui-dialog-footer>
        <button type="button" xuiButton variant="ghost" (click)="project.resolveReloading(false)">Cancel</button>
        <button type="button" xuiButton color="primary" data-testid="mesh-reload-confirm" (click)="reload()">
          Reload
        </button>
      </xui-dialog-footer>
    </xui-dialog>
  `
})
export class MeshReloadDialog {
  protected readonly project = inject(ProjectStore);
  private readonly library = inject(LibraryStore);
  protected readonly keep = Keep;

  /** The new mesh's materials, once read. */
  private readonly materials = signal<readonly string[]>([]);

  /** The new material each vanished set becomes, by the set's material. */
  protected readonly chosen = signal<Readonly<Record<string, string>>>({});

  /** The sets' materials the new mesh lacks. */
  protected readonly vanished = computed(() => {
    const now = this.materials();
    return now.length === 0
      ? []
      : this.project
          .textureSets()
          .map(set => set.material ?? '')
          .filter(material => material && !now.includes(material));
  });

  /** The new mesh's materials no set is on. */
  protected readonly appeared = computed(() => {
    const held = new Set(this.project.textureSets().map(set => set.material ?? ''));
    return this.materials().filter(material => !held.has(material));
  });

  protected readonly choices = computed(() => [Keep, ...this.appeared()]);
  protected readonly choiceText = (choice: string): string => (choice === Keep ? 'Keep as it is' : choice);

  constructor() {
    effect(onCleanup => {
      const source = this.project.reloading();
      let current = true;
      onCleanup(() => (current = false));
      this.materials.set([]);
      this.chosen.set({});
      if (source) {
        void this.read(source, () => current);
      }
    });
  }

  protected choose(set: string, material: string | null): void {
    this.chosen.update(all => {
      const next = { ...all };
      if (material) {
        next[set] = material;
      } else {
        delete next[set];
      }

      return next;
    });
  }

  /** Reloads, sending each set only to a material no other set was sent to, as the engine would refuse the second. */
  protected reload(): void {
    const taken = new Set<string>();
    const reassign: Record<string, string> = {};
    for (const [set, material] of Object.entries(this.chosen())) {
      if (!taken.has(material)) {
        taken.add(material);
        reassign[set] = material;
      }
    }

    void this.project.resolveReloading(true, reassign);
  }

  private async read(path: string, current: () => boolean): Promise<void> {
    try {
      const client = await this.library.api();
      const facts = unwrap(await client.GET('/api/v1/library/mesh', { params: { query: { path } } }));
      if (current()) {
        this.materials.set(facts.materials.map(material => material.name));
      }
    } catch {
      // Without the new mesh's materials there is nothing to offer, and reloading still works as it always did.
    }
  }
}
