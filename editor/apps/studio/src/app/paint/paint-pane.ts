import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { XuiNonIdealStateImports } from '@xui/non-ideal-state';
import { BrushPanel } from './brush-panel';
import { DocumentSection } from './document-section';
import { LayerPanel } from './layer-panel';
import { PaintStore } from './paint-store';

/**
 * Properties: the document, then the brush, then the chosen layer, each in its own section, so that what belongs to the
 * document never hides behind a tool or a layer (#201). The picture under the brush is the Server's: the same plan that
 * exports, so nothing here composites anything, and a stroke shows when its stamps have come back as a new map. With a
 * mesh bound a stroke here carries on over the seams it crosses, and the Viewport pane paints the mesh itself.
 */
@Component({
  selector: 'ptn-paint',
  imports: [BrushPanel, DocumentSection, LayerPanel, XuiNonIdealStateImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col' },
  template: `
    <div class="flex min-h-0 flex-1 flex-col gap-2 overflow-x-hidden overflow-y-auto p-3">
      @if (store.state()) {
        <ptn-document-section />
        <hr class="border-hairline" />

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
  protected readonly store = inject(PaintStore);
  /** A brush matters while something can be painted. */
  protected readonly brushes = this.store.canPaint;
}
