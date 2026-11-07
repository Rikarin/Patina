import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { XuiDialogImports } from '@xui/dialog';

/** A document's file changed behind its session. Saving would replace that, so it is the user's to say. */
@Component({
  selector: 'ptn-file-conflict',
  imports: [XuiButtonImports, XuiDialogImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <xui-dialog size="sm" title="The file changed" [open]="open()" (openChange)="$event || cancelled.emit()">
      <xui-dialog-body data-testid="file-conflict">
        {{ name() }} changed on disk after it was opened. Saving replaces what is there.
      </xui-dialog-body>
      <xui-dialog-footer>
        <button type="button" xuiButton variant="ghost" (click)="cancelled.emit()">Cancel</button>
        <button type="button" xuiButton color="primary" data-testid="file-overwrite" (click)="overwrite.emit()">
          Overwrite
        </button>
      </xui-dialog-footer>
    </xui-dialog>
  `
})
export class FileConflictDialog {
  readonly name = input.required<string>();
  readonly open = input.required<boolean>();
  readonly overwrite = output();
  readonly cancelled = output();
}
