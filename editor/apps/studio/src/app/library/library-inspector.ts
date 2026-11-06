import { CdkVirtualScrollViewport, ScrollingModule } from '@angular/cdk/scrolling';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  untracked,
  viewChild
} from '@angular/core';
import type { AssetSummary, ProjectSummary } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiDescriptionsImports } from '@xui/descriptions';
import { XuiNonIdealStateImports } from '@xui/non-ideal-state';
import { XuiSkeletonImports } from '@xui/skeleton';
import { XuiSpinnerImports } from '@xui/spinner';
import { XuiTagImports } from '@xui/tag';
import { GraphStore } from '../graph/graph-store';
import { LibrarySelection } from './library-selection';
import { LibraryStore } from './library-store';
import { ProjectStore } from './project-store';
import { counted } from './list-keys';
import { ViewportFit } from './viewport-fit';

const DependentHeight = 28;

/** A project, as far as the index knows it. Its contents load when it opens. */
@Component({
  selector: 'ptn-project-details',
  imports: [XuiButtonImports, XuiDescriptionsImports, XuiTagImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block overflow-auto p-3' },
  template: `
    @let item = project();
    <h2 class="truncate text-sm font-semibold" data-testid="inspector-title" [title]="item.name">{{ item.name }}</h2>
    <p class="section-label mb-3">Project</p>
    <button
      class="mb-3 w-full"
      type="button"
      xuiButton
      size="sm"
      color="primary"
      data-testid="open-project"
      (click)="opening.open(item.path)"
    >
      Open
    </button>
    <xui-descriptions column="1">
      <xui-descriptions-item label="Folder">
        <span class="break-all select-text">{{ item.path }}</span>
      </xui-descriptions-item>
      <xui-descriptions-item label="Shared assets">
        <span class="readout" data-testid="inspector-references">{{ item.referenceCount }}</span>
      </xui-descriptions-item>
      <xui-descriptions-item label="Tags">
        @for (tag of item.tags; track tag) {
          <xui-tag class="me-1 mb-1" minimal>{{ tag }}</xui-tag>
        } @empty {
          <span class="text-foreground-muted">None</span>
        }
      </xui-descriptions-item>
    </xui-descriptions>
  `
})
export class ProjectDetails {
  protected readonly opening = inject(ProjectStore);
  readonly project = input.required<ProjectSummary>();
}

/** A shared asset and what saving it would touch: "affects 412 projects, 3 pinned" (plan §5). */
@Component({
  selector: 'ptn-asset-impact',
  imports: [
    ScrollingModule,
    ViewportFit,
    XuiButtonImports,
    XuiCalloutImports,
    XuiDescriptionsImports,
    XuiNonIdealStateImports,
    XuiSkeletonImports,
    XuiSpinnerImports,
    XuiTagImports
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col' },
  template: `
    @let item = asset();
    <div class="shrink-0 p-3">
      <h2 class="truncate text-sm font-semibold" data-testid="inspector-title" [title]="item.name">{{ item.name }}</h2>
      <p class="section-label mb-3">{{ item.kind }}</p>
      @if (item.kind === 'graph') {
        <button
          class="mb-3 w-full"
          type="button"
          xuiButton
          size="sm"
          color="primary"
          data-testid="open-graph"
          (click)="open(item)"
        >
          Open
        </button>
      }
      <xui-descriptions column="1">
        <xui-descriptions-item label="File">
          <span class="break-all select-text">{{ item.path }}</span>
        </xui-descriptions-item>
        <xui-descriptions-item label="Tags">
          @for (tag of item.tags; track tag) {
            <xui-tag class="me-1 mb-1" minimal>{{ tag }}</xui-tag>
          } @empty {
            <span class="text-foreground-muted">None</span>
          }
        </xui-descriptions-item>
      </xui-descriptions>
    </div>

    <div class="chrome-bar flex h-8 shrink-0 items-center gap-2 px-3" data-testid="impact-summary">
      @if (impact().impact(); as answer) {
        <span class="readout truncate font-medium">Affects {{ projects(answer.projects) }}</span>
        <span class="readout text-foreground-muted shrink-0">{{ answer.pinned.toLocaleString('en') }} pinned</span>
      } @else if (!list().error()) {
        <xui-spinner size="xs" aria-label="Working out the impact" />
      }
    </div>

    @if (impact().impact()?.unreadableDocuments; as unreadable) {
      <xui-callout class="m-2 shrink-0" color="warning" compact>
        {{ documents(unreadable) }} could not be read, so these counts may be low.
      </xui-callout>
    }

    <div class="well relative min-h-0 flex-1 overflow-hidden">
      <cdk-virtual-scroll-viewport
        ptnViewportFit
        class="size-full"
        role="list"
        aria-label="Dependent projects"
        data-testid="impact-list"
        minBufferPx="200"
        maxBufferPx="400"
        [itemSize]="rowHeight"
      >
        <div
          *cdkVirtualFor="let project of list(); let index = index; trackBy: byIndex"
          class="border-hairline flex items-center gap-2 border-b px-3"
          role="listitem"
          [style.height.px]="rowHeight"
        >
          @if (project) {
            <span class="min-w-0 flex-1 truncate" [title]="project.path">{{ project.name }}</span>
            @if (project.pinned) {
              <xui-tag color="warning" minimal>Pinned</xui-tag>
            }
          } @else {
            <xui-skeleton class="h-2.5 w-2/3" />
          }
        </div>
      </cdk-virtual-scroll-viewport>

      @if (list().error(); as reason) {
        <xui-non-ideal-state
          class="absolute inset-0 m-auto h-fit"
          title="The impact did not load"
          [description]="reason"
        >
          <button action type="button" xuiButton size="sm" (click)="list().retry()">Try again</button>
        </xui-non-ideal-state>
      } @else if (list().total() === 0) {
        <xui-non-ideal-state class="absolute inset-0 m-auto h-fit" description="No project depends on this asset." />
      }
    </div>
  `
})
export class AssetImpactView {
  private readonly store = inject(LibraryStore);
  private readonly graph = inject(GraphStore);
  private readonly viewport = viewChild(CdkVirtualScrollViewport);

  readonly asset = input.required<AssetSummary>();

  protected readonly rowHeight = DependentHeight;
  protected readonly impact = computed(() => this.store.impactOf(this.asset()));
  protected readonly list = computed(() => this.impact().list);

  constructor() {
    effect(onCleanup => {
      const list = this.list();
      untracked(this.viewport)?.scrollToIndex(0);
      onCleanup(() => {
        list.cancel();
      });
    });

    let seen = this.store.changes();
    effect(() => {
      const changes = this.store.changes();
      if (changes !== seen) {
        seen = changes;
        untracked(() => {
          this.list().refresh();
        });
      }
    });
  }

  /** An asset's path is below shared/, and the Server opens paths relative to the library. */
  protected open(asset: AssetSummary): void {
    void this.graph.open(`shared/${asset.path}`);
  }

  protected projects(count: number): string {
    return counted(count, 'project');
  }

  protected documents(count: number): string {
    return counted(count, 'document');
  }

  protected byIndex(index: number): number {
    return index;
  }
}

/** The Inspector pane while the Library has the selection. */
@Component({
  selector: 'ptn-library-inspector',
  imports: [AssetImpactView, ProjectDetails],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex h-full min-h-0 flex-col' },
  template: `
    @let item = selection.item();
    @if (item === null) {
      <p class="text-foreground-muted p-3">Nothing selected.</p>
    } @else if (item.kind === 'project') {
      <ptn-project-details [project]="item.project" />
    } @else {
      <ptn-asset-impact [asset]="item.asset" />
    }
  `
})
export class LibraryInspector {
  protected readonly selection = inject(LibrarySelection);
}
