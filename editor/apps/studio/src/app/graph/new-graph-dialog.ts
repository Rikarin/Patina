import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  linkedSignal,
  model,
  signal
} from '@angular/core';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiDialogImports } from '@xui/dialog';
import { XuiFormFieldImports } from '@xui/form-field';
import { XuiInputImports } from '@xui/input';
import { XuiSelectImports } from '@xui/select';
import { messageOf } from '../core/messages';
import { SystemStatus } from '../core/system-status';
import { LibrarySelection } from '../library/library-selection';
import { GraphStore } from './graph-store';

/**
 * Asks for a new graph's name. It lands in the selected project, or in a project of its own when none is selected. The
 * Server decides whether it can exist, so its refusal is what this shows.
 */
@Component({
  selector: 'ptn-new-graph',
  imports: [
    XuiButtonImports,
    XuiCalloutImports,
    XuiDialogImports,
    XuiFormFieldImports,
    XuiInputImports,
    XuiSelectImports
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <xui-dialog title="New graph" size="sm" [(open)]="open">
      <form (submit)="create($event)">
        <xui-dialog-body class="flex flex-col gap-3">
          <xui-form-field label="Name" [helperText]="'Goes to ' + place()">
            <input
              class="w-full"
              xuiInput
              cdkFocusInitial
              data-testid="new-graph-name"
              autocomplete="off"
              [value]="name()"
              (input)="name.set(text($event))"
            />
          </xui-form-field>
          <xui-form-field label="Size" helperText="You can change it later without changing the picture.">
            <xui-select
              ariaLabel="Size"
              data-testid="new-graph-size"
              [items]="sides()"
              [value]="side()"
              (valueChange)="side.set($event ?? side())"
            />
          </xui-form-field>
          @if (refused(); as reason) {
            <xui-callout color="error" data-testid="new-graph-refused" compact>{{ reason }}</xui-callout>
          }
        </xui-dialog-body>
        <xui-dialog-footer>
          <button type="button" xuiButton variant="ghost" (click)="open.set(false)">Cancel</button>
          <button type="submit" xuiButton color="primary" data-testid="new-graph-create" [disabled]="!canCreate()">
            Create
          </button>
        </xui-dialog-footer>
      </form>
    </xui-dialog>
  `
})
export class NewGraphDialog {
  private readonly store = inject(GraphStore);
  private readonly selection = inject(LibrarySelection);
  private readonly status = inject(SystemStatus);
  private readonly saving = signal(false);

  readonly open = model(false);

  protected readonly name = signal('');
  protected readonly sides = computed(() => this.status.textureSides().map(String));
  protected readonly side = linkedSignal(() => String(this.status.defaultTextureSide() ?? ''));
  protected readonly refused = signal<string | null>(null);
  protected readonly canCreate = computed(() => this.name().trim() !== '' && !this.saving());
  private readonly project = computed(() => {
    const item = this.selection.item();
    return item?.kind === 'project' ? item.project.path : null;
  });

  protected readonly place = computed(() => {
    const project = this.project();
    return project === null ? 'a new project' : `projects/${project}`;
  });

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
      await this.store.create(this.name().trim(), this.project(), Number(this.side()) || null);
      this.name.set('');
      this.open.set(false);
    } catch (error) {
      this.refused.set(messageOf(error));
    } finally {
      this.saving.set(false);
    }
  }
}
