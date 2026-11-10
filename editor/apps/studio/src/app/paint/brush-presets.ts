import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { provideIcons } from '@ng-icons/core';
import { matStar, matStarBorder } from '@ng-icons/material-icons/baseline';
import type { BrushPresetInfo } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiInputImports } from '@xui/input';
import { IconImports } from '../core/icons';
import { BrushStore } from './brush-store';

/** Where a preset comes from, said shortly beside its name. */
const Origins: Readonly<Record<string, string>> = { shipped: 'Starter', project: 'This project', library: 'Library' };

/**
 * The brush and tool presets (#124): favourites first, then the starters, the project's and the library's. Clicking one
 * takes it up; the star makes it a favourite; Save keeps the brush as it stands as a new preset of the project, and
 * Update rewrites the one taken up from it. The files are the project's documents, so the library tree moves them.
 */
@Component({
  selector: 'ptn-brush-presets',
  viewProviders: [provideIcons({ matStar, matStarBorder })],
  imports: [IconImports, XuiButtonImports, XuiInputImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex flex-col gap-1' },
  template: `
    <div class="flex items-center gap-1">
      <span class="section-label flex-1">Presets</span>
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        data-testid="brush-preset-update"
        title="Rewrites the preset taken up from the brush as it stands"
        [disabled]="!brushes.current() || brushes.current()?.origin === 'shipped'"
        (click)="brushes.updateChosen()"
      >
        Update
      </button>
      <button
        type="button"
        xuiButton
        size="sm"
        variant="ghost"
        data-testid="brush-preset-save"
        title="Keeps the brush as it stands as a new preset of this project"
        [disabled]="brushes.project() === null"
        (click)="naming.set(!naming())"
      >
        Save…
      </button>
    </div>
    @if (naming()) {
      <div class="flex items-center gap-1" data-testid="brush-preset-naming">
        <input
          class="min-w-0 flex-1"
          xuiInput
          size="sm"
          autocomplete="off"
          aria-label="Preset name"
          data-testid="brush-preset-name"
          [value]="name()"
          (input)="name.set(text($event))"
          (keydown.enter)="save()"
          (keydown.escape)="naming.set(false)"
        />
        <label class="flex items-center gap-1 text-xs" title="The preset paints in this color when taken up">
          <input
            type="checkbox"
            data-testid="brush-preset-colour"
            [checked]="withColour()"
            (change)="withColour.set(checked($event))"
          />
          Color
        </label>
        <button
          type="button"
          xuiButton
          size="sm"
          data-testid="brush-preset-confirm"
          [disabled]="!name().trim()"
          (click)="save()"
        >
          Save
        </button>
      </div>
    }
    <ul class="well flex max-h-40 flex-col overflow-y-auto" data-testid="brush-presets" aria-label="Brush presets">
      @for (preset of brushes.presets(); track preset.source) {
        <li class="flex items-center gap-1 px-1" [class.bg-selection]="brushes.chosen() === preset.source">
          <button
            type="button"
            class="text-foreground-muted grid size-5 shrink-0 place-items-center"
            [attr.aria-label]="(preset.preset.favourite ? 'Unfavourite ' : 'Favourite ') + preset.name"
            [attr.aria-pressed]="preset.preset.favourite"
            [attr.data-testid]="'brush-preset-favourite-' + preset.name"
            (click)="brushes.toggleFavourite(preset)"
          >
            <ng-icon xui size="sm" [name]="preset.preset.favourite ? 'matStar' : 'matStarBorder'" />
          </button>
          <button
            type="button"
            class="min-w-0 flex-1 truncate py-0.5 text-start text-xs"
            [attr.data-testid]="'brush-preset-' + preset.name"
            [title]="describe(preset)"
            (click)="brushes.use(preset)"
          >
            {{ preset.name }}
          </button>
          <span class="text-foreground-muted shrink-0 text-[10px]">{{ origin(preset) }}</span>
        </li>
      }
    </ul>
    @if (brushes.failure(); as failure) {
      <p class="text-error text-xs" role="alert" data-testid="brush-preset-failure">{{ failure }}</p>
    }
  `
})
export class BrushPresets {
  protected readonly brushes = inject(BrushStore);
  protected readonly naming = signal(false);
  protected readonly name = signal('');
  protected readonly withColour = signal(false);

  protected async save(): Promise<void> {
    if (!this.name().trim()) {
      return;
    }

    await this.brushes.saveAs(this.name(), this.withColour());
    if (!this.brushes.failure()) {
      this.naming.set(false);
      this.name.set('');
    }
  }

  protected origin(preset: BrushPresetInfo): string {
    const where = Origins[preset.origin] ?? preset.origin;
    return preset.project ? `${where}: ${preset.project}` : where;
  }

  protected describe(preset: BrushPresetInfo): string {
    return `${preset.name}: ${preset.preset.tool}, size ${String(Math.round(preset.preset.brush.radius))} (${preset.preset.brush.sizeSpace})`;
  }

  protected text(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  protected checked(event: Event): boolean {
    return (event.target as HTMLInputElement).checked;
  }
}
