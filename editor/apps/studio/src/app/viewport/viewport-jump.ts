import { Injectable, signal } from '@angular/core';
import type { ViewportCameraState } from '@patina/api-client';

/** Where a pane outside the viewport asks it to go: a view that shows something, and a camera that looks at it. */
export interface ViewportTarget {
  readonly view?: string | null;
  readonly camera?: ViewportCameraState | null;
}

/**
 * A way into the viewport for panes that do not hold it, such as Checks going to a problem (#159). The viewport's
 * client is its pane's own, so it takes each target from here and sends it to the Server as if it were its own input.
 */
@Injectable({ providedIn: 'root' })
export class ViewportJump {
  /** The newest target, numbered so the same place asked for twice is gone to twice. */
  readonly target = signal<{ readonly at: number; readonly to: ViewportTarget } | null>(null);
  private asked = 0;

  /** Asks the viewport to show a view and look through a camera, either of which may be left as it is. */
  go(to: ViewportTarget): void {
    this.target.set({ at: ++this.asked, to });
  }
}
