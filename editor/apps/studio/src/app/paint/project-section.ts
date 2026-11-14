import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { ProjectSettingsForm } from '../library/project-settings-form';
import { ProjectStore } from '../library/project-store';

/**
 * The project's settings in Properties, under the document (#201): the mesh every texturing document of the project is
 * on, how it unwraps and whether paint moves with a new one, with Apply and Revert of its own. Project Settings shows the
 * same form for a project with nothing open.
 */
@Component({
  selector: 'ptn-project-section',
  imports: [XuiButtonImports, ProjectSettingsForm],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form
      class="flex flex-col gap-2"
      aria-label="Project settings"
      data-testid="paint-project-settings"
      (submit)="apply($event, form)"
    >
      <ptn-project-settings-form #form />
      <div class="flex justify-end gap-1">
        <button
          type="button"
          xuiButton
          size="sm"
          variant="ghost"
          data-testid="paint-project-revert"
          [disabled]="!form.changed()"
          (click)="form.reset()"
        >
          Revert
        </button>
        <button
          type="submit"
          xuiButton
          size="sm"
          color="primary"
          data-testid="paint-project-apply"
          [disabled]="project.unwrapping() || !form.changed()"
        >
          {{ form.applyLabel() }}
        </button>
      </div>
    </form>
  `
})
export class ProjectSection {
  protected readonly project = inject(ProjectStore);

  protected async apply(event: Event, form: ProjectSettingsForm): Promise<void> {
    event.preventDefault();
    await form.apply();
  }
}
