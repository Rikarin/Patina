import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { XuiDialogImports } from '@xui/dialog';

/** A folder the tree would delete, as its question names it. */
export interface TreeDeleting {
  readonly folder: { readonly name: string; readonly projectCount: number };
}

/** A move or rename that would close open documents, as its question names them. */
export interface TreeClosing {
  readonly what: string;
  readonly documents: readonly { readonly name: string; readonly path: string }[];
}

/**
 * The tree's two questions, deleting a folder and closing what a move would close. They are seldom asked, so the tree
 * loads them the first time one is (#301), which keeps them out of what the app loads to start.
 */
@Component({
  selector: 'ptn-library-tree-dialogs',
  imports: [XuiButtonImports, XuiDialogImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <xui-dialog
      size="sm"
      title="Delete folder"
      [open]="deleting() !== null"
      (openChange)="$event || deleteCancelled.emit()"
    >
      <xui-dialog-body data-testid="tree-delete">
        @if (deleting()?.folder; as folder) {
          @if (folder.projectCount > 0) {
            {{ folder.name }} holds {{ folder.projectCount }} {{ folder.projectCount === 1 ? 'project' : 'projects' }}.
            They move up a level, and nothing is deleted but the folder.
          } @else {
            Delete {{ folder.name }} and any empty folders in it?
          }
        }
      </xui-dialog-body>
      <xui-dialog-footer>
        <button type="button" xuiButton variant="ghost" (click)="deleteCancelled.emit()">Cancel</button>
        <button
          type="button"
          xuiButton
          color="error"
          data-testid="tree-delete-confirm"
          (click)="deleteConfirmed.emit()"
        >
          Delete Folder
        </button>
      </xui-dialog-footer>
    </xui-dialog>

    <xui-dialog
      size="sm"
      title="Close open documents?"
      [open]="closing() !== null"
      (openChange)="$event || closingCancelled.emit()"
    >
      <xui-dialog-body data-testid="tree-closing">
        @if (closing(); as ask) {
          <p>
            {{ ask.what }} closes
            {{ ask.documents.length === 1 ? ask.documents[0]?.name : ask.documents.length + ' open documents' }}, and
            {{ ask.documents.length === 1 ? 'its' : 'their' }} undo history is lost. Saved work is kept.
          </p>
          @if (ask.documents.length > 1) {
            <ul class="text-foreground-muted mt-1 list-inside list-disc text-xs">
              @for (document of ask.documents; track document.path) {
                <li>{{ document.name }}</li>
              }
            </ul>
          }
        }
      </xui-dialog-body>
      <xui-dialog-footer>
        <button type="button" xuiButton variant="ghost" (click)="closingCancelled.emit()">Cancel</button>
        <button
          type="button"
          xuiButton
          color="warning"
          data-testid="tree-closing-confirm"
          (click)="closingConfirmed.emit()"
        >
          Close and Continue
        </button>
      </xui-dialog-footer>
    </xui-dialog>
  `
})
export class LibraryTreeDialogs {
  readonly deleting = input<TreeDeleting | null>(null);
  readonly closing = input<TreeClosing | null>(null);
  readonly deleteCancelled = output();
  readonly deleteConfirmed = output();
  readonly closingCancelled = output();
  readonly closingConfirmed = output();
}
