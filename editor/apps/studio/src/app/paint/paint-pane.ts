import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { XuiNonIdealStateImports } from '@xui/non-ideal-state';
import { ProjectStore } from '../library/project-store';
import { BrushPanel } from './brush-panel';
import { DocumentSection } from './document-section';
import { LayerPanel } from './layer-panel';
import { PaintStore } from './paint-store';
import { ProjectSection } from './project-section';
import { SymmetrySection } from './symmetry-section';

/** The sections of Properties that fold, so the layer's settings and its mask can come up the pane (#201). */
type Fold = 'document' | 'project' | 'symmetry' | 'brush';
const Folds: readonly string[] = ['document', 'project', 'symmetry', 'brush'];

const FoldKey = 'patina.properties.folded';

/**
 * What starts folded: the document's texture set settings and the project's, as they change seldom and would push the
 * layer's settings down the pane (#298). The document's name, mesh and channels stay in view either way.
 */
const StartFolded: readonly Fold[] = ['document', 'project'];

/** Which sections this machine last folded. A choice of the viewer's, so it stays in this browser. */
function savedFolds(): ReadonlySet<Fold> {
  try {
    const saved = globalThis.localStorage.getItem(FoldKey);
    const held = (saved === null ? StartFolded : JSON.parse(saved)) as unknown;
    return new Set(Array.isArray(held) ? held.filter((fold): fold is Fold => Folds.includes(fold as string)) : []);
  } catch {
    return new Set(StartFolded);
  }
}

/**
 * Properties: the document, then its project's settings, then symmetry, then the brush, then the chosen layer, each in
 * its own section, so that what belongs to the document never hides behind a tool or a layer (#201). Every section but
 * the layer's folds to its heading, and stays folded on this machine, so a layer's settings and its mask need not sit
 * below the fold; the document folds to its name, mesh and channels. Symmetry is apart from the brush, so a fill whose
 * decal or path follows it can still reach it (#298). The
 * picture under the brush is the Server's: the same plan that exports, so nothing here composites anything, and a stroke
 * shows when its stamps have come back as a new map. With a mesh bound a stroke here carries on over the seams it
 * crosses, and the Viewport pane paints the mesh itself.
 */
@Component({
  selector: 'ptn-paint',
  imports: [
    BrushPanel,
    DocumentSection,
    LayerPanel,
    NgTemplateOutlet,
    ProjectSection,
    SymmetrySection,
    XuiNonIdealStateImports
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col' },
  template: `
    <div class="flex min-h-0 flex-1 flex-col gap-2 overflow-x-hidden overflow-y-auto p-3">
      @if (store.state()) {
        <ng-container
          *ngTemplateOutlet="heading; context: { fold: 'document', name: 'Document', id: 'paint-fold-document' }"
        />
        <ptn-document-section [settings]="!folded().has('document')" />
        <hr class="border-hairline" />

        <!-- Hidden while Project Settings is open, so that the one form is the one being changed. -->
        @if (project.document()?.kind === 'texturing' && !project.settingsOpen()) {
          <ng-container
            *ngTemplateOutlet="heading; context: { fold: 'project', name: 'Project', id: 'paint-fold-project' }"
          />
          @if (!folded().has('project')) {
            <ptn-project-section />
          }
          <hr class="border-hairline" />
        }

        @if (store.state()?.model?.trim()) {
          <ng-container
            *ngTemplateOutlet="heading; context: { fold: 'symmetry', name: 'Symmetry', id: 'paint-fold-symmetry' }"
          />
          @if (!folded().has('symmetry')) {
            <ptn-symmetry-section />
          }
          <hr class="border-hairline" />
        }

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
  protected readonly project = inject(ProjectStore);
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
