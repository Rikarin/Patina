import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, signal, untracked } from '@angular/core';
import type { BinItem } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiDialogImports } from '@xui/dialog';
import { XuiNonIdealStateImports } from '@xui/non-ideal-state';
import { XuiSpinnerImports } from '@xui/spinner';
import { messageOf } from '../core/messages';
import { bytes } from '../core/numbers';
import { LibraryActions } from './library-actions';
import { ProjectStore } from './project-store';

/**
 * The library's bin: what was deleted, kept in `.trash/` in the library folder until it is emptied here. Restoring puts
 * a thing back where it was; Delete Forever and Empty Bin are the only ways anything is gone for good, and each asks
 * first. It loads the first time it is opened.
 */
@Component({
  selector: 'ptn-bin',
  imports: [DatePipe, XuiButtonImports, XuiCalloutImports, XuiDialogImports, XuiNonIdealStateImports, XuiSpinnerImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <xui-dialog size="md" title="Bin" [open]="actions.binOpen()" (openChange)="$event || closeBin()">
      <xui-dialog-body class="flex min-h-48 flex-col gap-2" data-testid="bin">
        @if (actions.failure(); as reason) {
          <xui-callout color="error" compact>{{ reason }}</xui-callout>
        }
        @if (items() === null) {
          <div class="grid flex-1 place-items-center"><xui-spinner size="md" aria-label="Reading the bin" /></div>
        } @else {
          @for (item of items(); track item.id) {
            <div class="border-hairline flex items-center gap-2 border-b py-1.5" data-testid="bin-item" [attr.data-id]="item.id">
              <span class="min-w-0 flex-1 leading-tight">
                <span class="block truncate">{{ item.name }}</span>
                <span class="text-foreground-muted block truncate text-xs" [title]="item.from">
                  {{ item.kind === 'project' ? 'Project' : 'Document' }} from {{ where(item) }}, deleted
                  {{ item.deleted | date: 'short' }}
                </span>
              </span>
              @if (forgetting() === item.id) {
                <button type="button" xuiButton size="sm" variant="ghost" (click)="forgetting.set(null)">Keep</button>
                <button type="button" xuiButton size="sm" color="error" data-testid="bin-forget-confirm" (click)="forget(item)">
                  Delete Forever
                </button>
              } @else {
                <button type="button" xuiButton size="sm" variant="ghost" data-testid="bin-forget" (click)="forgetting.set(item.id)">
                  Delete…
                </button>
                <button type="button" xuiButton size="sm" data-testid="bin-restore" (click)="restore(item)">Restore</button>
              }
            </div>
          } @empty {
            <xui-non-ideal-state
              class="flex-1"
              title="The bin is empty"
              description="A project or document you delete waits here, with its paint, until you restore it or empty the bin."
            />
          }
        }
      </xui-dialog-body>
      <xui-dialog-footer>
        <span class="readout text-foreground-muted me-auto text-xs" data-testid="bin-size">{{ size() }}</span>
        @if (emptying()) {
          <span class="text-sm">Delete everything in the bin for good?</span>
          <button type="button" xuiButton variant="ghost" (click)="emptying.set(false)">Keep</button>
          <button type="button" xuiButton color="error" data-testid="bin-empty-confirm" (click)="empty()">Empty Bin</button>
        } @else {
          <button
            type="button"
            xuiButton
            variant="ghost"
            data-testid="bin-empty"
            [disabled]="(items()?.length ?? 0) === 0"
            (click)="emptying.set(true)"
          >
            Empty Bin…
          </button>
          <button type="button" xuiButton color="primary" (click)="closeBin()">Done</button>
        }
      </xui-dialog-footer>
    </xui-dialog>
  `
})
export class BinDialog {
  protected readonly actions = inject(LibraryActions);
  private readonly project = inject(ProjectStore);

  protected readonly items = signal<readonly BinItem[] | null>(null);
  protected readonly size = signal('');
  protected readonly forgetting = signal<string | null>(null);
  protected readonly emptying = signal(false);

  constructor() {
    effect(() => {
      if (this.actions.binOpen()) {
        untracked(() => void this.read());
      }
    });
  }

  /** Where a thing lived, as the Library names it: a project's folder, or a document's project. */
  protected where(item: BinItem): string {
    const below = item.from.replace(/^projects\//, '');
    return item.kind === 'project' ? below : below.slice(0, Math.max(0, below.lastIndexOf('/')));
  }

  protected closeBin(): void {
    this.actions.binOpen.set(false);
    this.forgetting.set(null);
    this.emptying.set(false);
    this.actions.failure.set(null);
  }

  protected async restore(item: BinItem): Promise<void> {
    if (await this.actions.restore(item.id)) {
      if (item.kind === 'document') {
        await this.project.refreshContents(this.where(item));
      }

      await this.read();
    }
  }

  protected async forget(item: BinItem): Promise<void> {
    this.forgetting.set(null);
    if (await this.actions.deleteForGood(item.id)) {
      await this.read();
    }
  }

  protected async empty(): Promise<void> {
    this.emptying.set(false);
    if (await this.actions.emptyBin()) {
      await this.read();
    }
  }

  private async read(): Promise<void> {
    try {
      const bin = await this.actions.bin();
      this.items.set(bin.items);
      this.size.set(bin.items.length === 0 ? '' : `${bytes(bin.bytes)} on disk`);
    } catch (failure) {
      this.items.set([]);
      this.actions.failure.set(messageOf(failure));
    }
  }
}
