import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { desktop } from '../core/desktop';
import { labelIn } from '../core/labels';
import { ProjectStore } from '../library/project-store';
import { PaintStore } from './paint-store';

/**
 * The document's own settings, at the top of Properties whatever tool or layer is chosen (#201): what it is and the
 * mesh it is painted on, its size, and its channels, with the one to paint and Add Channel. Nothing here depends on
 * the brush or the layer, so choosing a fill no longer hides Add Channel.
 *
 * **A setting of the document or its texture set goes here**, as a row of the `dl`: a label in `dt`, the control in
 * `dd`. Brush settings go in {@link BrushPanel}, and a layer's in {@link LayerPanel}.
 */
@Component({
  selector: 'ptn-document-section',
  imports: [XuiButtonImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex flex-col gap-1.5', role: 'group', 'aria-label': 'Document', 'data-testid': 'paint-document' },
  template: `
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

      <dl class="grid grid-cols-[auto_1fr] items-center gap-x-3 gap-y-1 text-xs">
        @if (state.set) {
          <dt class="text-foreground-muted">Texture set</dt>
          <dd class="truncate" data-testid="paint-document-set">{{ state.set }}</dd>
        }
        <dt class="text-foreground-muted">Size</dt>
        <dd class="readout" data-testid="paint-document-size">{{ state.width }} × {{ state.height }}</dd>
      </dl>

      <div class="flex flex-wrap items-center gap-1" role="group" aria-label="Channels">
        @for (channel of state.channels; track channel.usage) {
          <button
            type="button"
            xuiButton
            size="sm"
            [variant]="channel.usage === store.channel() ? 'default' : 'ghost'"
            [attr.aria-pressed]="channel.usage === store.channel()"
            [attr.data-testid]="'paint-channel-' + channel.usage"
            (click)="store.showChannel(channel.usage)"
          >
            {{ nameOf(channel.usage) }}
          </button>
        }
        @if (addable().length > 0) {
          <select
            class="well h-7 min-w-0 px-1 text-sm"
            aria-label="Add a channel to this document"
            data-testid="paint-add-channel"
            (change)="addChannel($event)"
          >
            <option value="">Add Channel…</option>
            @for (usage of addable(); track usage) {
              <option [value]="usage">{{ nameOf(usage) }}</option>
            }
          </select>
        }
      </div>
    }
  `
})
export class DocumentSection {
  /** A channel by the label the vocabulary carries for it. */
  protected nameOf(usage: string): string {
    return labelIn(this.store.vocabulary()?.usages, usage);
  }
  protected readonly store = inject(PaintStore);
  protected readonly project = inject(ProjectStore);
  protected readonly canPick = desktop() !== undefined;

  /** The channels the vocabulary has that this document does not. */
  protected readonly addable = computed(() => {
    const held = this.store.state()?.channels.map(channel => channel.usage) ?? [];
    return (this.store.vocabulary()?.usages ?? []).map(term => term.value).filter(usage => !held.includes(usage));
  });

  protected addChannel(event: Event): void {
    const select = event.target as HTMLSelectElement;
    const usage = select.value;
    select.value = '';
    if (usage) {
      void this.store.edit({ op: 'addChannel', usage });
    }
  }
}
