import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { XuiInputImports } from '@xui/input';
import { XuiNonIdealStateImports } from '@xui/non-ideal-state';
import { desktop } from '../core/desktop';
import { channelName } from '../core/labels';
import { ProjectStore } from '../library/project-store';
import { BrushPanel } from './brush-panel';
import { LayerPanel } from './layer-panel';
import { PaintStore } from './paint-store';

/**
 * The stack, the brush, and the atlas to paint on. The picture under the brush is the Server's: the same plan that
 * exports, so nothing here composites anything, and a stroke shows when its stamps have come back as a new map. With a
 * mesh bound a stroke here carries on over the seams it crosses, and the Viewport pane paints the mesh itself.
 */
@Component({
  selector: 'ptn-paint',
  imports: [BrushPanel, LayerPanel, XuiButtonImports, XuiNonIdealStateImports, XuiInputImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col' },
  template: `
    <div class="flex min-h-0 flex-1 flex-col gap-2 overflow-x-hidden overflow-y-auto p-3">
      @if (store.state(); as state) {
        @if (project.current(); as open) {
          <div class="flex items-center gap-2">
            <span class="min-w-0 flex-1 leading-tight">
              <span class="block truncate font-semibold" data-testid="paint-project">{{ open.name }}</span>
              <span class="text-foreground-muted block truncate text-xs" data-testid="paint-mesh-name">
                {{ project.meshName() ?? project.document()?.name }}
              </span>
            </span>
            @if (canPick && project.document()?.kind === 'texturing') {
              <button
                type="button"
                xuiButton
                size="sm"
                variant="ghost"
                data-testid="paint-mesh"
                (click)="project.settingsOpen.set(true)"
              >
                {{ project.meshName() ? 'Change Mesh…' : 'Choose Mesh…' }}
              </button>
            }
          </div>
        }

        <div class="flex flex-wrap items-center gap-1">
          @for (channel of state.channels; track channel.usage) {
            <button
              type="button"
              xuiButton
              size="sm"
              [variant]="channel.usage === store.channel() ? 'default' : 'ghost'"
              [attr.data-testid]="'paint-channel-' + channel.usage"
              (click)="store.showChannel(channel.usage)"
            >
              {{ nameOf(channel.usage) }}
            </button>
          }
        </div>

        @if (brushes()) {
          <ptn-brush-panel />
        }

        <ptn-layer-panel />
      } @else {
        <xui-non-ideal-state
          class="flex-1"
          title="Nothing open"
          description="Open a texturing or layered material document."
          data-testid="paint-none"
        />
      }
    </div>
  `
})
export class PaintPane {
  protected readonly nameOf = channelName;
  protected readonly store = inject(PaintStore);
  protected readonly project = inject(ProjectStore);
  protected readonly canPick = desktop() !== undefined;
  /** A brush matters while something can be painted: a paint layer, or any layer's mask. */
  protected readonly brushes = computed(() => this.store.chosen()?.kind === 'paint' || this.store.masking());
}
