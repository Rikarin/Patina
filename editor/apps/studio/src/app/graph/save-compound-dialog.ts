import { ChangeDetectionStrategy, Component, computed, effect, inject, model, signal } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiDialogImports } from '@xui/dialog';
import { XuiFormFieldImports } from '@xui/form-field';
import { XuiInputImports } from '@xui/input';
import { GraphStore } from './graph-store';

/**
 * Asks what to call a compound made of the selected nodes. The Server decides whether it can exist, so its refusal is
 * what this shows. Folders in the name, as in `Masks/Rust Edge`, are where the palette files it.
 */
@Component({
  selector: 'ptn-save-compound',
  imports: [XuiButtonImports, XuiCalloutImports, XuiDialogImports, XuiFormFieldImports, XuiInputImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <xui-dialog title="Save as compound" size="sm" [(open)]="open">
      <form (submit)="save($event)">
        <xui-dialog-body class="flex flex-col gap-3">
          <xui-form-field label="Name" helperText="Goes to Compounds, beside this graph. Undo keeps the file.">
            <input
              class="w-full"
              xuiInput
              cdkFocusInitial
              data-testid="save-compound-name"
              autocomplete="off"
              [value]="name()"
              (input)="name.set(text($event))"
            />
          </xui-form-field>
          @if (refused(); as reason) {
            <xui-callout color="error" data-testid="save-compound-refused" compact>{{ reason }}</xui-callout>
          }
        </xui-dialog-body>
        <xui-dialog-footer>
          <button type="button" xuiButton variant="ghost" (click)="open.set(false)">Cancel</button>
          <button type="submit" xuiButton color="primary" data-testid="save-compound-save" [disabled]="!canSave()">
            Save
          </button>
        </xui-dialog-footer>
      </form>
    </xui-dialog>
  `
})
export class SaveCompoundDialog {
  private readonly store = inject(GraphStore);
  private readonly saving = signal(false);

  readonly open = model(false);

  protected readonly name = signal('');
  protected readonly refused = signal<string | null>(null);
  protected readonly canSave = computed(() => this.name().trim() !== '' && !this.saving());

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

  protected async save(event: Event): Promise<void> {
    event.preventDefault();
    if (!this.canSave()) {
      return;
    }

    this.saving.set(true);
    const refusal = await this.store.saveSelectionAsCompound(this.name().trim());
    this.saving.set(false);
    this.refused.set(refusal);
    if (refusal === null) {
      this.name.set('');
      this.open.set(false);
    }
  }
}
