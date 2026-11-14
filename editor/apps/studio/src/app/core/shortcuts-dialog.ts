import { ChangeDetectionStrategy, Component, computed, inject, model, signal } from '@angular/core';
import { provideIcons } from '@ng-icons/core';
import { matClose } from '@ng-icons/material-icons/baseline';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiDialogImports } from '@xui/dialog';
import { XuiSelectImports } from '@xui/select';
import { type Chord, chordOf, chordText, isMac } from './chords';
import { type CommandSpec, FixedKeys, KeyPresets, Sections, reservedKeys } from './command-table';
import { Commands } from './commands';
import { desktop } from './desktop';
import { IconImports } from './icons';

/** A key the dialog could not give, and what has it: a command, which can swap, or the app menu or a control. */
interface Refused {
  id: string;
  chord: Chord;
  taken: CommandSpec | null;
  label: string;
  where: string | null;
}

/**
 * Keyboard Shortcuts: every command by section, each with its keys, and the keys controls answer themselves. Clicking a
 * command's key listens for the next chord, modifiers and all; a key another command has where this one's keys work is
 * refused, with the offer to swap, and one the app menu or a control holds is refused outright.
 */
@Component({
  selector: 'ptn-shortcuts',
  imports: [IconImports, XuiButtonImports, XuiCalloutImports, XuiDialogImports, XuiSelectImports],
  viewProviders: [provideIcons({ matClose })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <xui-dialog size="md" title="Keyboard Shortcuts" [(open)]="open">
      <xui-dialog-body>
        <div class="flex flex-col gap-3" data-testid="shortcuts">
          <div class="flex items-center gap-2 text-sm">
            <span class="text-foreground-muted">Preset</span>
            <xui-select
              class="min-w-44"
              aria-label="Preset"
              data-testid="shortcuts-preset"
              title="A preset is where keys start. Choosing one drops keys changed since."
              [filterable]="false"
              [itemText]="presetName"
              [items]="presets"
              [value]="commands.settings().preset"
              (valueChange)="$event && choosePreset($event)"
            />
            <span class="flex-1"></span>
            <button type="button" xuiButton size="sm" variant="ghost" (click)="reset()">Reset to Preset</button>
          </div>

          @if (refused(); as refusal) {
            <xui-callout color="warning" data-testid="shortcuts-taken" compact>
              <span class="flex flex-wrap items-center gap-2">
                <span class="min-w-0 flex-1"
                  >{{ text(refusal.chord) }} is already {{ refusal.label
                  }}{{ refusal.where ? ', in ' + refusal.where : '' }}. One key does one thing.</span
                >
                @if (refusal.taken) {
                  <button type="button" xuiButton size="sm" data-testid="shortcuts-swap" (click)="swap(refusal)">
                    Swap Keys
                  </button>
                }
                <button type="button" xuiButton size="sm" variant="ghost" (click)="refused.set(null)">
                  {{ refusal.taken ? 'Keep' : 'OK' }}
                </button>
              </span>
            </xui-callout>
          }

          @for (section of sections; track section.name) {
            <section class="flex flex-col gap-0.5" [attr.aria-label]="section.name">
              <h3 class="section-label">{{ section.name }}</h3>
              @for (command of section.commands; track command.id) {
                <div class="flex items-center gap-2" [attr.data-command]="command.id">
                  <span class="min-w-0 flex-1 truncate text-sm">{{ command.label }}</span>
                  <button
                    type="button"
                    xuiButton
                    size="sm"
                    class="min-w-20 justify-end font-mono"
                    data-testid="shortcut-key"
                    [variant]="listening() === command.id ? 'default' : 'ghost'"
                    [attr.aria-label]="'Change the key for ' + command.label"
                    [title]="'Change the key for ' + command.label"
                    (click)="listening.set(command.id)"
                    (keydown)="bind($event, command.id)"
                    (blur)="listening() === command.id && listening.set(null)"
                  >
                    {{ listening() === command.id ? 'Press a key…' : keysOf(command.id) || '—' }}
                  </button>
                  <button
                    type="button"
                    xuiButton
                    size="sm"
                    variant="ghost"
                    class="w-6 px-0"
                    [attr.aria-label]="'Remove the key for ' + command.label"
                    [title]="'Remove the key for ' + command.label"
                    [disabled]="!keysOf(command.id)"
                    (click)="commands.clear(command.id)"
                  >
                    <ng-icon xui size="sm" name="matClose" />
                  </button>
                </div>
              }
            </section>
          }

          <section class="flex flex-col gap-0.5" aria-label="Mouse">
            <h3 class="section-label">Mouse in the viewport</h3>
            @for (row of mouse(); track row.label) {
              <div class="flex items-center gap-2 text-sm">
                <span class="min-w-0 flex-1">{{ row.label }}</span>
                <span class="readout">{{ row.keys }}</span>
              </div>
            }
          </section>

          @for (section of fixed; track section.name) {
            <section class="flex flex-col gap-0.5" [attr.aria-label]="section.name">
              <h3 class="section-label">{{ section.name }}</h3>
              @for (row of section.rows; track row.label) {
                <div class="flex items-center gap-2 text-sm">
                  <span class="min-w-0 flex-1">{{ row.label }}</span>
                  <span class="readout">{{ row.keys }}</span>
                </div>
              }
            </section>
          }
        </div>
      </xui-dialog-body>
    </xui-dialog>
  `
})
export class ShortcutsDialog {
  protected readonly commands = inject(Commands);
  readonly open = model(false);
  /** The command whose next key press becomes its key. */
  protected readonly listening = signal<string | null>(null);
  protected readonly refused = signal<Refused | null>(null);
  private readonly mac = isMac(desktop()?.platform);

  protected readonly presets = Object.keys(KeyPresets);
  protected readonly presetName = (id: string): string =>
    Object.entries(KeyPresets).find(([key]) => key === id)?.[1].label ?? id;

  protected readonly sections = Sections.map(name => ({
    name,
    commands: this.commands.table.filter(command => command.section === name)
  })).filter(section => section.commands.length > 0);

  protected readonly mouse = computed(() => {
    const navigation = this.commands.navigation();
    return [
      { label: 'Orbit', keys: navigation.orbit },
      { label: 'Pan', keys: navigation.pan },
      { label: 'Zoom', keys: navigation.zoom },
      { label: 'Straight line from the last stroke', keys: 'Shift+click, Mod to snap to 5°' }
    ].map(row => ({ ...row, keys: this.words(row.keys) }));
  });

  protected readonly fixed = [
    ...[...new Set(FixedKeys.map(row => row.section))].map(name => ({
      name,
      rows: FixedKeys.filter(row => row.section === name).map(row => ({ label: row.label, keys: this.words(row.keys) }))
    })),
    {
      name: 'App menu',
      rows: reservedKeys(this.mac).map(key => ({ label: key.label, keys: chordText(key.chord, this.mac) }))
    }
  ];

  protected keysOf(id: string): string {
    return (this.commands.keys().get(id) ?? []).map(chord => this.text(chord)).join(', ');
  }

  protected text(chord: Chord): string {
    return chordText(chord, this.mac);
  }

  /**
   * The next key pressed on a listening row becomes that command's: modifiers count, so ⌘X is ⌘X and never X. Escape
   * stops listening, and a modifier alone waits for the rest of the chord.
   */
  protected bind(event: KeyboardEvent, id: string): void {
    if (this.listening() !== id) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    if (event.key === 'Escape') {
      this.listening.set(null);
      return;
    }

    const chord = chordOf(event, this.mac);
    if (chord === null) {
      return;
    }

    this.listening.set(null);
    const done = this.commands.bind(id, chord);
    this.refused.set(done.done ? null : { id, chord, taken: done.taken, label: done.label, where: done.where });
  }

  protected swap(refusal: Refused): void {
    if (refusal.taken) {
      this.commands.swap(refusal.id, refusal.taken.id, refusal.chord);
    }

    this.refused.set(null);
  }

  protected choosePreset(preset: string): void {
    this.commands.usePreset(preset);
    this.refused.set(null);
  }

  protected reset(): void {
    this.commands.usePreset(this.commands.settings().preset);
    this.refused.set(null);
  }

  /** `Mod` in the table's words is the platform's command key. */
  private words(text: string): string {
    return text.replace(/\bMod\b/gu, this.mac ? '⌘' : 'Ctrl');
  }
}
