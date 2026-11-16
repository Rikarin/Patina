import { ChangeDetectionStrategy, Component, input, model } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { matChevronRight } from '@ng-icons/material-icons/baseline';
import { XuiButtonImports } from '@xui/button';
import { XuiCollapseImports } from '@xui/collapse';
import { XuiIconImports } from '@xui/icon';

/**
 * A compact disclosure for a dense pane: an xUI ghost button that opens and closes an xUI collapse under it (#195).
 * xUI's accordion is a card with roomy rows, which suits a page but not a line in a dialog or a dock pane, so this
 * composes the two xUI parts instead of the native details element the app used to draw. The content leaves the page while
 * it is closed, so nothing hidden can take the focus.
 */
@Component({
  selector: 'ptn-disclosure',
  imports: [NgIcon, XuiButtonImports, XuiCollapseImports, XuiIconImports],
  viewProviders: [provideIcons({ matChevronRight })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    <button
      type="button"
      xuiButton
      size="sm"
      variant="ghost"
      class="text-foreground-muted -ms-1 px-1"
      data-disclosure-toggle
      [attr.aria-expanded]="open()"
      (click)="open.set(!open())"
    >
      <ng-icon xui size="sm" name="matChevronRight" class="transition-transform" [class.rotate-90]="open()" />
      {{ label() }}
    </button>
    <xui-collapse [open]="open()">
      <ng-content />
    </xui-collapse>
  `
})
export class Disclosure {
  /** What the button says: the name of what opens under it. */
  readonly label = input.required<string>();

  /** Whether the content is shown. Closed to start with, as a native details element is. */
  readonly open = model(false);
}
