import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { XuiDialogImports } from '@xui/dialog';
import { XuiInputImports } from '@xui/input';
import { ProjectStore } from '../library/project-store';
import { PaintStore } from '../paint/paint-store';
import { FileConflictDialog } from './file-conflict-dialog';

/**
 * The questions a session asks now and then: what to do with unsaved changes as a document, a project or the app
 * closes, what to call a copy, whether to restore work never saved, and whether to write over a file changed elsewhere.
 * They are seldom open, so the app loads them the first time one is asked (#301), which keeps them out of what it
 * loads to start.
 */
@Component({
  selector: 'ptn-session-dialogs',
  imports: [DatePipe, FileConflictDialog, XuiButtonImports, XuiDialogImports, XuiInputImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <xui-dialog
      size="sm"
      title="Unsaved changes"
      [open]="project.closing() !== null"
      (openChange)="$event || project.resolveClosing('cancel')"
    >
      <xui-dialog-body data-testid="project-leaving">This document has unsaved changes.</xui-dialog-body>
      <xui-dialog-footer>
        <button type="button" xuiButton variant="ghost" (click)="project.resolveClosing('cancel')">Cancel</button>
        <button
          type="button"
          xuiButton
          variant="ghost"
          data-testid="project-discard"
          (click)="project.resolveClosing('discard')"
        >
          Don't Save
        </button>
        <button
          type="button"
          xuiButton
          color="primary"
          data-testid="project-save"
          (click)="project.resolveClosing('save')"
        >
          Save
        </button>
      </xui-dialog-footer>
    </xui-dialog>

    <ptn-file-conflict
      [name]="project.active()?.name ?? ''"
      [open]="paint.saveConflict()"
      (cancelled)="paint.saveConflict.set(false)"
      (overwrite)="paint.act('save', true)"
    />

    <xui-dialog
      size="sm"
      [title]="project.copying() ? 'Save a Copy' : 'Save As'"
      [open]="project.savingAs()"
      (openChange)="project.savingAs.set($event)"
    >
      <form (submit)="saveAs($event, saveName.value)">
        <xui-dialog-body>
          <input
            #saveName
            class="w-full"
            xuiInput
            aria-label="New name"
            autocomplete="off"
            data-testid="save-as-name"
          />
        </xui-dialog-body>
        <xui-dialog-footer>
          <button type="button" xuiButton variant="ghost" (click)="project.savingAs.set(false)">Cancel</button>
          <button type="submit" xuiButton color="primary" data-testid="save-as-save">Save</button>
        </xui-dialog-footer>
      </form>
    </xui-dialog>

    <xui-dialog
      size="sm"
      title="Recover unsaved work?"
      [open]="project.recoverable() !== null"
      (openChange)="$event || project.dismissRecovery()"
    >
      <xui-dialog-body data-testid="app-recovery">
        Patina kept work on this document that was never saved, from {{ project.recoverable()?.saved | date: 'short' }}.
        Closing this keeps it, and asks again next time.
      </xui-dialog-body>
      <xui-dialog-footer>
        <button
          type="button"
          xuiButton
          variant="ghost"
          data-testid="recovery-discard"
          (click)="project.resolveRecovery(false)"
        >
          Discard
        </button>
        <button
          type="button"
          xuiButton
          color="primary"
          data-testid="recovery-restore"
          (click)="project.resolveRecovery(true)"
        >
          Restore
        </button>
      </xui-dialog-footer>
    </xui-dialog>

    <xui-dialog
      size="sm"
      title="Quit Patina?"
      [open]="project.quitting()"
      (openChange)="$event || project.resolveQuitting('cancel')"
    >
      <xui-dialog-body data-testid="app-quitting"
        >{{ project.unsavedTabs().length }} document(s) have unsaved changes.</xui-dialog-body
      >
      <xui-dialog-footer>
        <button type="button" xuiButton variant="ghost" (click)="project.resolveQuitting('cancel')">Cancel</button>
        <button type="button" xuiButton variant="ghost" (click)="project.resolveQuitting('discard')">Don't Save</button>
        <button type="button" xuiButton color="primary" (click)="project.resolveQuitting('save')">Save All</button>
      </xui-dialog-footer>
    </xui-dialog>

    <xui-dialog
      size="sm"
      title="Close Project?"
      [open]="project.closingProject() !== null"
      (openChange)="$event || project.resolveClosingProject('cancel')"
    >
      <xui-dialog-body data-testid="project-closing"
        >Some documents of this project have unsaved changes.</xui-dialog-body
      >
      <xui-dialog-footer>
        <button type="button" xuiButton variant="ghost" (click)="project.resolveClosingProject('cancel')">
          Cancel
        </button>
        <button type="button" xuiButton variant="ghost" (click)="project.resolveClosingProject('discard')">
          Don't Save
        </button>
        <button type="button" xuiButton color="primary" (click)="project.resolveClosingProject('save')">
          Save All
        </button>
      </xui-dialog-footer>
    </xui-dialog>
  `
})
export class SessionDialogs {
  protected readonly project = inject(ProjectStore);
  protected readonly paint = inject(PaintStore);

  protected saveAs(event: Event, name: string): void {
    event.preventDefault();
    if (name.trim()) {
      this.project.savingAs.set(false);
      void this.project.saveAs(name.trim(), this.project.copying());
    }
  }
}
