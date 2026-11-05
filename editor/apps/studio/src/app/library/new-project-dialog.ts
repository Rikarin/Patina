import { ChangeDetectionStrategy, Component, computed, effect, inject, model, signal } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiDialogImports } from '@xui/dialog';
import { XuiFormFieldImports } from '@xui/form-field';
import { XuiInputImports } from '@xui/input';
import { XuiTagInputImports } from '@xui/tag-input';
import { LibraryStore } from './library-store';

/** Asks for a new project's name. The Server decides whether it can exist, so its refusal is what this shows. */
@Component({
  selector: 'ptn-new-project',
  imports: [
    XuiButtonImports,
    XuiCalloutImports,
    XuiDialogImports,
    XuiFormFieldImports,
    XuiInputImports,
    XuiTagInputImports
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <xui-dialog title="New project" size="sm" [(open)]="open">
      <form (submit)="create($event)">
        <xui-dialog-body class="flex flex-col gap-3">
          <xui-form-field label="Name">
            <input
              class="w-full"
              xuiInput
              cdkFocusInitial
              data-testid="new-project-name"
              autocomplete="off"
              [value]="name()"
              (input)="name.set(text($event))"
            />
          </xui-form-field>
          <xui-form-field label="Folder" helperText="Optional, below projects/. For example props/small.">
            <input
              class="w-full"
              xuiInput
              data-testid="new-project-folder"
              autocomplete="off"
              [value]="folder()"
              (input)="folder.set(text($event))"
            />
          </xui-form-field>
          <xui-form-field label="Tags">
            <xui-tag-input placeholder="Type a tag, then Enter" [(values)]="tags" />
          </xui-form-field>
          @if (refused(); as reason) {
            <xui-callout color="error" data-testid="new-project-refused" compact>{{ reason }}</xui-callout>
          }
        </xui-dialog-body>
        <xui-dialog-footer>
          <button type="button" xuiButton variant="ghost" (click)="open.set(false)">Cancel</button>
          <button type="submit" xuiButton color="primary" [disabled]="!canCreate()">Create</button>
        </xui-dialog-footer>
      </form>
    </xui-dialog>
  `
})
export class NewProjectDialog {
  private readonly store = inject(LibraryStore);

  readonly open = model(false);

  protected readonly name = signal('');
  protected readonly folder = signal('');
  protected readonly tags = signal<string[]>([]);
  protected readonly refused = signal<string | null>(null);
  private readonly saving = signal(false);
  protected readonly canCreate = computed(() => this.name().trim() !== '' && !this.saving());

  constructor() {
    effect(() => {
      if (this.open()) {
        this.refused.set(null); // a refusal is about the last attempt, not this one
      }
    });
  }

  protected text(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  protected async create(event: Event): Promise<void> {
    event.preventDefault();
    if (!this.canCreate()) {
      return;
    }

    this.saving.set(true);
    this.refused.set(null);
    try {
      await this.store.createProject({
        name: this.name().trim(),
        folder: this.folder().trim() || null,
        tags: this.tags()
      });
      this.name.set('');
      this.tags.set([]);
      this.open.set(false);
    } catch (error) {
      this.refused.set(error instanceof Error ? error.message : String(error));
    } finally {
      this.saving.set(false);
    }
  }
}
