import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiDialogImports } from '@xui/dialog';
import { XuiInputImports } from '@xui/input';
import { messageOf } from '../core/messages';
import { ProjectSettingsForm } from './project-settings-form';
import { ProjectStore } from './project-store';
import { ProjectTemplates } from './project-templates';

/**
 * Project Settings, for a project from the Library or the menu, open or not. With one of its documents open the same
 * settings are in Properties, under the document (#201). Its settings, as applied, can be kept as a template that New
 * Project starts from (#144).
 */
@Component({
  selector: 'ptn-project-settings',
  imports: [XuiButtonImports, XuiCalloutImports, XuiDialogImports, XuiInputImports, ProjectSettingsForm],
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
          <ptn-project-settings-form #form place="dialog" [shown]="project.settingsOpen()" />
          <div class="mt-3 flex flex-col gap-1" data-testid="project-template">
            <span class="section-label">Template</span>
            <div class="flex items-center gap-2">
              <input
                class="min-w-0 flex-1"
                xuiInput
                autocomplete="off"
                placeholder="Template name"
                aria-label="Template name"
                data-testid="project-template-name"
                [value]="templateName()"
                (input)="templateName.set(text($event))"
              />
              <button
                type="button"
                xuiButton
                size="sm"
                data-testid="project-template-save"
                title="Keeps this project's settings, as applied, for New Project to start from"
                [disabled]="!templateName().trim() || keeping()"
                (click)="keep()"
              >
                Save as Template
              </button>
            </div>
            @if (kept(); as name) {
              <span class="text-foreground-muted text-xs" data-testid="project-template-kept"
                >Kept as {{ name }}. New Project can start from it.</span
              >
            }
            @if (refused(); as reason) {
              <xui-callout color="error" data-testid="project-template-refused" compact>{{ reason }}</xui-callout>
            }
          </div>
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
  private readonly templates = inject(ProjectTemplates);

  protected readonly templateName = signal('');
  protected readonly keeping = signal(false);
  protected readonly kept = signal<string | null>(null);
  protected readonly refused = signal<string | null>(null);

  constructor() {
    effect(() => {
      if (this.project.settingsOpen()) {
        this.kept.set(null); // what was kept, or refused, was about the last time it was open
        this.refused.set(null);
      }
    });
  }

  protected text(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  protected async apply(event: Event, form: ProjectSettingsForm): Promise<void> {
    event.preventDefault();
    if (await form.apply()) {
      this.project.settingsOpen.set(false);
    }
  }

  /** Keeps the project's settings under the name typed, taken before the Server is asked. */
  protected async keep(): Promise<void> {
    const project = this.project.current()?.path;
    const name = this.templateName().trim();
    if (!project || !name) {
      return;
    }

    this.keeping.set(true);
    this.kept.set(null);
    this.refused.set(null);
    try {
      await this.templates.save(project, name);
      this.kept.set(name);
    } catch (error) {
      this.refused.set(messageOf(error));
    } finally {
      this.keeping.set(false);
    }
  }
}
