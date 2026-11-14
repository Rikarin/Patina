import { ChangeDetectionStrategy, Component, inject, input, linkedSignal, output } from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { XuiDialogImports } from '@xui/dialog';
import { XuiFormFieldImports } from '@xui/form-field';
import { XuiInputImports } from '@xui/input';
import { PaintStore } from './paint-store';

/** Layers, or one layer's mask, to save to the project's shelf, and the name it starts with. */
export interface ShelfSave {
  readonly layers: readonly string[];
  readonly mask: boolean;
  readonly name: string;
  readonly project: string;
}

/** Asks what to call layers or a mask saved to the shelf (#135), starting from the layer's own name. */
@Component({
  selector: 'ptn-shelf-save-dialog',
  imports: [XuiButtonImports, XuiDialogImports, XuiFormFieldImports, XuiInputImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <xui-dialog
      size="sm"
      [title]="asked()?.mask ? 'Save as Smart Mask' : 'Save as Smart Material'"
      [open]="asked() !== null"
      (openChange)="$event || closed.emit()"
    >
      <form (submit)="save($event)">
        <xui-dialog-body class="flex flex-col gap-3" data-testid="shelf-save-dialog">
          <xui-form-field label="Name">
            <input
              class="w-full"
              xuiInput
              cdkFocusInitial
              autocomplete="off"
              data-testid="shelf-save-name"
              [value]="name()"
              (input)="name.set(text($event))"
            />
          </xui-form-field>
          <p class="text-foreground-muted text-xs">
            It goes on this project's shelf. Painted pixels stay behind; everything else comes with it.
          </p>
        </xui-dialog-body>
        <xui-dialog-footer>
          <button type="button" xuiButton variant="ghost" (click)="closed.emit()">Cancel</button>
          <button type="submit" xuiButton color="primary" data-testid="shelf-save-confirm" [disabled]="!name().trim()">
            Save
          </button>
        </xui-dialog-footer>
      </form>
    </xui-dialog>
  `
})
export class ShelfSaveDialog {
  private readonly store = inject(PaintStore);

  /** What to save, or null while the dialog is closed. */
  readonly asked = input<ShelfSave | null>(null);
  /** The dialog closed, saved or not. */
  readonly closed = output();
  protected readonly name = linkedSignal(() => this.asked()?.name ?? '');

  protected save(event: Event): void {
    event.preventDefault();
    const asked = this.asked();
    const name = this.name().trim();
    if (!asked || !name) {
      return;
    }

    this.closed.emit();
    void this.store.saveToShelf(asked.layers, asked.mask, name, asked.project);
  }

  protected text(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }
}
