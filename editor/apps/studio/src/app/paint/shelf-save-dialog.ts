import { ChangeDetectionStrategy, Component, computed, inject, input, linkedSignal, output } from '@angular/core';
import type { StackLayer } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiDialogImports } from '@xui/dialog';
import { XuiFormFieldImports } from '@xui/form-field';
import { XuiInputImports } from '@xui/input';
import { PaintStore } from './paint-store';

/**
 * Layers, or one layer's mask, to save to the project's shelf, and the name it starts with; or a graph fill to keep as
 * a material preset (#124).
 */
export interface ShelfSave {
  readonly layers: readonly string[];
  readonly mask: boolean;
  readonly name: string;
  readonly project: string;
  readonly preset?: StackLayer;
}

/** Asks what to call layers, a mask or a material preset saved to the shelf (#135), starting from the layer's name. */
@Component({
  selector: 'ptn-shelf-save-dialog',
  imports: [XuiButtonImports, XuiDialogImports, XuiFormFieldImports, XuiInputImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <xui-dialog size="sm" [title]="title()" [open]="asked() !== null" (openChange)="$event || closed.emit()">
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
            @if (asked()?.preset) {
              It goes on this project's shelf among the materials: the layer's material with its values as they are now.
            } @else {
              It goes on this project's shelf. Painted pixels stay behind; everything else comes with it.
            }
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
  protected readonly title = computed(() => {
    const asked = this.asked();
    return asked?.preset ? 'Save as Material Preset' : asked?.mask ? 'Save as Smart Mask' : 'Save as Smart Material';
  });

  protected save(event: Event): void {
    event.preventDefault();
    const asked = this.asked();
    const name = this.name().trim();
    if (!asked || !name) {
      return;
    }

    this.closed.emit();
    void (asked.preset
      ? this.store.saveMaterialPreset(asked.preset, name, asked.project)
      : this.store.saveToShelf(asked.layers, asked.mask, name, asked.project));
  }

  protected text(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }
}
