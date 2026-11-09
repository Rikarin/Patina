import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { provideIcons } from '@ng-icons/core';
import { XuiButtonImports } from '@xui/button';
import { Commands } from '../core/commands';
import { IconImports } from '../core/icons';
import { PaintStore } from './paint-store';
import { PaintToolIcons, PaintTools } from './paint-tools';

/**
 * The painting tools beside the viewport, whatever Properties shows (#127): choosing a fill layer used to hide the
 * tools, which lived only in the brush panel. Each button runs the tool's command, so its key and its tooltip are the
 * table's, and the one in hand is pressed.
 */
@Component({
  selector: 'ptn-tool-strip',
  viewProviders: [provideIcons(PaintToolIcons)],
  imports: [IconImports, XuiButtonImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'pane flex flex-col gap-0.5 p-0.5 shadow-sm',
    role: 'toolbar',
    'aria-label': 'Painting tools',
    'aria-orientation': 'vertical',
    'data-testid': 'tool-strip',
    '[attr.data-tool]': 'paint.tool()'
  },
  template: `
    @for (tool of tools; track tool.id) {
      <button
        type="button"
        xuiButton
        size="sm"
        [attr.data-testid]="'tool-strip-' + tool.id"
        [attr.aria-label]="commands.spec(tool.command)?.label"
        [active]="paint.tool() === tool.id"
        [title]="commands.tooltip(tool.command)"
        [variant]="paint.tool() === tool.id ? 'default' : 'ghost'"
        [disabled]="!paint.state()"
        (click)="paint.tool.set(tool.id)"
      >
        <ng-icon xui size="sm" [name]="tool.icon" />
      </button>
    }
  `
})
export class ToolStrip {
  protected readonly paint = inject(PaintStore);
  protected readonly commands = inject(Commands);
  protected readonly tools = PaintTools;
}
