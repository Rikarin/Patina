import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { XuiDialogImports } from '@xui/dialog';
import { ProjectSettingsForm } from './project-settings-form';
import { ProjectStore } from './project-store';

/**
 * Project Settings, for a project from the Library or the menu, open or not. With one of its documents open the same
 * settings are in Properties, under the document (#201).
 */
@Component({
  selector: 'ptn-project-settings',
  imports: [XuiButtonImports, XuiDialogImports, ProjectSettingsForm],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <xui-dialog
      size="sm"
      title="Project Settings"
      [open]="project.settingsOpen()"
      (openChange)="project.settingsOpen.set($event)"
    >
      <form (submit)="apply($event, form)">
        <xui-dialog-body data-testid="project-settings">
          <ptn-project-settings-form #form [shown]="project.settingsOpen()" />
        </xui-dialog-body>
        <xui-dialog-footer>
          <button type="button" xuiButton variant="ghost" (click)="project.settingsOpen.set(false)">Cancel</button>
          <button
            type="submit"
            xuiButton
            color="primary"
            data-testid="project-settings-apply"
            [disabled]="project.unwrapping() || !form.changed()"
          >
            {{ form.applyLabel() }}
          </button>
        </xui-dialog-footer>
      </form>
    </xui-dialog>
  `
})
export class ProjectSettingsDialog {
  protected readonly project = inject(ProjectStore);

  protected async apply(event: Event, form: ProjectSettingsForm): Promise<void> {
    event.preventDefault();
    if (await form.apply()) {
      this.project.settingsOpen.set(false);
    }
  }
}
