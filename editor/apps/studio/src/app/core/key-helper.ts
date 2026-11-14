import { ChangeDetectionStrategy, Component, computed, inject, model } from '@angular/core';
import { chordText, isMac } from './chords';
import { Sections } from './command-table';
import { Commands } from './commands';
import { desktop } from './desktop';

/** A command with keys that would run now, as the helper lists it. */
interface HelperLine {
  readonly id: string;
  readonly label: string;
  readonly keys: string;
}

/**
 * The key helper (#190): over the app, every key that would do something now, by section, and how to move a view
 * when one shows. It reads what the Keyboard Shortcuts dialog reads, after the person's own keys, and lists only the
 * commands that something would answer, so it changes with what is in front. Its key, or Escape, or a click puts it
 * away; commands keep their keys while it shows.
 */
@Component({
  selector: 'ptn-key-helper',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown.escape)': 'open.set(false)' },
  template: `
    @if (open()) {
      <div
        class="fixed inset-0 z-40 flex items-center justify-center bg-black/40 p-6"
        role="presentation"
        data-testid="key-helper-backdrop"
        (click)="$event.target === $event.currentTarget && open.set(false)"
      >
        <div
          class="pane flex max-h-full w-full max-w-5xl flex-col gap-3 overflow-y-auto p-4 text-sm"
          role="dialog"
          aria-label="Keys that work now"
          data-testid="key-helper"
        >
          <div class="flex items-baseline gap-3">
            <h2 class="section-label flex-1">Keys that work now</h2>
            <span class="text-foreground-muted text-xs">{{ closing() }}</span>
          </div>
          <div class="columns-1 gap-6 sm:columns-2 lg:columns-3">
            @for (section of sections(); track section.name) {
              <section class="mb-3 break-inside-avoid" [attr.data-testid]="'key-helper-' + section.name">
                <h3 class="text-foreground-muted mb-1 text-xs font-semibold">{{ section.name }}</h3>
                @for (line of section.lines; track line.id) {
                  <div class="flex items-baseline gap-2 py-0.5" [attr.data-testid]="'key-helper-line-' + line.id">
                    <span class="min-w-0 flex-1 truncate">{{ line.label }}</span>
                    <span class="readout shrink-0">{{ line.keys }}</span>
                  </div>
                }
              </section>
            }
            @if (moving(); as navigation) {
              <section class="mb-3 break-inside-avoid" data-testid="key-helper-navigation">
                <h3 class="text-foreground-muted mb-1 text-xs font-semibold">Moving the view</h3>
                <div class="flex items-baseline gap-2 py-0.5">
                  <span class="min-w-0 flex-1">Orbit</span><span class="readout">{{ navigation.orbit }}</span>
                </div>
                <div class="flex items-baseline gap-2 py-0.5">
                  <span class="min-w-0 flex-1">Pan</span><span class="readout">{{ navigation.pan }}</span>
                </div>
                <div class="flex items-baseline gap-2 py-0.5">
                  <span class="min-w-0 flex-1">Zoom</span><span class="readout">{{ navigation.zoom }}</span>
                </div>
              </section>
            }
          </div>
        </div>
      </div>
    }
  `
})
export class KeyHelper {
  readonly open = model(false);
  private readonly commands = inject(Commands);
  private readonly mac = isMac(desktop()?.platform);

  /** The commands that would run on a key now, by section, in the Keyboard Shortcuts dialog's order. */
  protected readonly sections = computed(() => {
    const keys = this.commands.keys();
    const lines = this.commands.table
      .filter(spec => (keys.get(spec.id)?.length ?? 0) > 0 && this.commands.enabled(spec.id))
      .map(spec => ({
        section: spec.section,
        line: {
          id: spec.id,
          label: this.commands.label(spec.id),
          keys: (keys.get(spec.id) ?? []).map(chord => chordText(chord, this.mac)).join(' or ')
        } satisfies HelperLine
      }));
    return Sections.map(name => ({
      name,
      lines: lines.filter(found => found.section === name).map(found => found.line)
    })).filter(section => section.lines.length > 0);
  });

  /** How a drag moves a view, while there is a model to move around. */
  protected readonly moving = computed(() => (this.commands.enabled('frameModel') ? this.commands.navigation() : null));

  /** How to put the helper away, with its own key as the person has it. */
  protected readonly closing = computed(() => {
    const key = this.commands.keyText('keyHelper');
    return key ? `${key} or Escape to close` : 'Escape to close';
  });
}
