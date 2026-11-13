import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { XuiNonIdealStateImports } from '@xui/non-ideal-state';
import { BrushPanel } from './brush-panel';
import { DocumentSection } from './document-section';
import { LayerPanel } from './layer-panel';
import { PaintStore } from './paint-store';

/** The sections of Properties that fold, so the layer's settings and its mask can come up the pane (#201). */
type Fold = 'document' | 'brush';

const FoldKey = 'patina.properties.folded';

/** Which sections this machine last folded. A choice of the viewer's, so it stays in this browser. */
function savedFolds(): ReadonlySet<Fold> {
  try {
    const held = JSON.parse(globalThis.localStorage.getItem(FoldKey) ?? '[]') as unknown;
    return new Set(
      Array.isArray(held) ? held.filter((fold): fold is Fold => fold === 'document' || fold === 'brush') : []
    );
  } catch {
    return new Set();
  }
}

/**
 * Properties: the document, then the brush, then the chosen layer, each in its own section, so that what belongs to the
 * document never hides behind a tool or a layer (#201). The document and the brush each fold to their heading, and stay
 * folded on this machine, so a layer's settings and its mask need not sit below the fold. The picture under the brush is
 * the Server's: the same plan that exports, so nothing here composites anything, and a stroke shows when its stamps have
 * come back as a new map. With a mesh bound a stroke here carries on over the seams it crosses, and the Viewport pane
 * paints the mesh itself.
 */
@Component({
  selector: 'ptn-paint',
  imports: [BrushPanel, DocumentSection, LayerPanel, NgTemplateOutlet, XuiNonIdealStateImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col' },
  template: `
    <div class="flex min-h-0 flex-1 flex-col gap-2 overflow-x-hidden overflow-y-auto p-3">
      @if (store.state()) {
        <ng-container
          *ngTemplateOutlet="heading; context: { fold: 'document', name: 'Document', id: 'paint-fold-document' }"
        />
        @if (!folded().has('document')) {
          <ptn-document-section />
        }
        <hr class="border-hairline" />

        @if (brushes()) {
          <ng-container
            *ngTemplateOutlet="heading; context: { fold: 'brush', name: 'Brush', id: 'paint-fold-brush' }"
          />
          @if (!folded().has('brush')) {
            <ptn-brush-panel />
          }
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

    <ng-template #heading let-fold="fold" let-name="name" let-id="id">
      <button
        type="button"
        class="section-label hover:text-foreground flex items-center gap-1 text-left"
        [attr.aria-expanded]="!folded().has(fold)"
        [attr.data-testid]="id"
        [title]="folded().has(fold) ? 'Show this section' : 'Fold this section to its heading'"
        (click)="toggle(fold)"
      >
        <span aria-hidden="true" class="w-3">{{ folded().has(fold) ? '▸' : '▾' }}</span>
        {{ name }}
      </button>
    </ng-template>
  `
})
export class PaintPane {
  protected readonly store = inject(PaintStore);
  /** A brush matters while something can be painted. */
  protected readonly brushes = this.store.canPaint;
  /** The sections folded to their heading. */
  protected readonly folded = signal<ReadonlySet<Fold>>(savedFolds());

  protected toggle(fold: Fold): void {
    const next = new Set(this.folded());
    if (!next.delete(fold)) {
      next.add(fold);
    }

    this.folded.set(next);
    try {
      globalThis.localStorage.setItem(FoldKey, JSON.stringify([...next]));
    } catch {
      // Only this machine's choice is lost.
    }
  }
}
