import { ChangeDetectionStrategy, Component, effect, inject, model, signal, untracked } from '@angular/core';
import { type components, unwrap } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiDialogImports } from '@xui/dialog';
import { XuiNumericInputImports } from '@xui/numeric-input';
import { XuiSegmentedControlImports, type XuiSegmentedOption } from '@xui/segmented-control';
import { XuiSwitchImports } from '@xui/switch';
import { LibraryStore } from '../library/library-store';
import { Commands } from './commands';
import { desktop } from './desktop';
import { messageOf } from './messages';
import { bytes } from './numbers';
import type { CameraSpeed, LibraryTabChoice, LibraryViewChoice, ShelfViewChoice, Theme } from './preferences';
import { Preferences } from './preferences';

type AutosavePreferences = components['schemas']['AutosavePreferences'];
type CacheInfo = components['schemas']['CacheInfo'];

const Themes: readonly XuiSegmentedOption<Theme>[] = [
  { value: 'dark', label: 'Dark' },
  { value: 'light', label: 'Light' },
  { value: 'system', label: 'System' }
];
const LibraryViews: readonly XuiSegmentedOption<LibraryViewChoice>[] = [
  { value: 'list', label: 'List' },
  { value: 'grid', label: 'Grid' }
];
const LibraryTabs: readonly XuiSegmentedOption<LibraryTabChoice>[] = [
  { value: 'projects', label: 'Projects' },
  { value: 'assets', label: 'Materials' }
];
const CameraSpeedOptions: readonly XuiSegmentedOption<CameraSpeed>[] = [
  { value: 'slow', label: 'Slow' },
  { value: 'normal', label: 'Normal' },
  { value: 'fast', label: 'Fast' }
];
const ShelfViews: readonly XuiSegmentedOption<ShelfViewChoice>[] = [
  { value: 'list', label: 'List' },
  { value: 'small', label: 'Small' },
  { value: 'large', label: 'Large' }
];

/** The longest wait the Server takes, a day, in minutes and seconds as the fields show them. */
const MostMinutes = 1440;
const MostSeconds = 86_400;
const MostKept = 20;

/**
 * The person's settings for the whole app (#189), not a project's. What only this app shows is kept by the app; what
 * the Server does for every client, autosave, is the Server's, and the dialog says so.
 */
@Component({
  selector: 'ptn-preferences',
  imports: [
    XuiButtonImports,
    XuiCalloutImports,
    XuiDialogImports,
    XuiNumericInputImports,
    XuiSegmentedControlImports,
    XuiSwitchImports
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <xui-dialog size="md" title="Preferences" [(open)]="open">
      <xui-dialog-body>
        <div class="flex flex-col gap-4" data-testid="preferences">
          <section class="flex flex-col gap-2">
            <h3 class="section-label">Appearance</h3>
            <div class="flex items-center gap-3">
              <span class="w-36 shrink-0">Theme</span>
              <xui-segmented-control
                class="min-w-0 flex-1"
                size="sm"
                fill
                aria-label="Theme"
                data-testid="preferences-theme"
                [options]="themes"
                [value]="preferences.theme()"
                (valueChange)="$event && preferences.set({ theme: $event })"
              />
            </div>
          </section>

          <section class="flex flex-col gap-2">
            <h3 class="section-label">Interface</h3>
            <div class="flex items-center gap-3">
              <span class="w-36 shrink-0">Library opens on</span>
              <xui-segmented-control
                class="min-w-0 flex-1"
                size="sm"
                fill
                aria-label="Library opens on"
                data-testid="preferences-library-tab"
                [options]="libraryTabs"
                [value]="preferences.libraryTab()"
                (valueChange)="$event && preferences.set({ libraryTab: $event })"
              />
            </div>
            <div class="flex items-center gap-3">
              <span class="w-36 shrink-0">Library shows</span>
              <xui-segmented-control
                class="min-w-0 flex-1"
                size="sm"
                fill
                aria-label="Library shows"
                data-testid="preferences-library-view"
                [options]="libraryViews"
                [value]="preferences.libraryView()"
                (valueChange)="$event && preferences.set({ libraryView: $event })"
              />
            </div>
            <div class="flex items-center gap-3">
              <span class="w-36 shrink-0">Shelf pictures</span>
              <xui-segmented-control
                class="min-w-0 flex-1"
                size="sm"
                fill
                aria-label="Shelf pictures"
                data-testid="preferences-shelf-view"
                [options]="shelfViews"
                [value]="preferences.shelfView()"
                (valueChange)="$event && preferences.set({ shelfView: $event })"
              />
            </div>
            <label class="flex items-center gap-3">
              <span class="w-36 shrink-0">Render statistics</span>
              <xui-switch
                aria-label="Render statistics"
                data-testid="preferences-stats"
                [checked]="preferences.stats()"
                (checkedChange)="preferences.set({ stats: $event })"
              />
            </label>
          </section>

          <section class="flex flex-col gap-2" data-testid="preferences-camera">
            <h3 class="section-label">Viewport camera</h3>
            <div class="flex items-center gap-3">
              <span class="w-36 shrink-0">Orbit speed</span>
              <xui-segmented-control
                class="min-w-0 flex-1"
                size="sm"
                fill
                aria-label="Orbit speed"
                data-testid="preferences-orbit-speed"
                [options]="cameraSpeeds"
                [value]="preferences.all().orbitSpeed"
                (valueChange)="$event && preferences.set({ orbitSpeed: $event })"
              />
            </div>
            <div class="flex items-center gap-3">
              <span class="w-36 shrink-0">Zoom speed</span>
              <xui-segmented-control
                class="min-w-0 flex-1"
                size="sm"
                fill
                aria-label="Zoom speed"
                data-testid="preferences-zoom-speed"
                [options]="cameraSpeeds"
                [value]="preferences.all().zoomSpeed"
                (valueChange)="$event && preferences.set({ zoomSpeed: $event })"
              />
            </div>
            <label class="flex items-center gap-3">
              <span class="w-36 shrink-0">Invert the wheel</span>
              <xui-switch
                aria-label="Invert the wheel"
                data-testid="preferences-invert-zoom"
                [checked]="preferences.all().invertZoom"
                (checkedChange)="preferences.set({ invertZoom: $event })"
              />
              <span class="text-foreground-muted text-xs">rolled away from you, the camera moves out</span>
            </label>
            <div class="flex items-center gap-3" data-testid="preferences-viewport-start">
              <span class="w-36 shrink-0">A new project's display</span>
              <span class="readout min-w-0 flex-1 truncate text-xs">
                {{ preferences.viewportStart() ? 'Your own' : 'Patina’s' }}
              </span>
              @if (preferences.viewportStart()) {
                <button
                  type="button"
                  xuiButton
                  size="sm"
                  variant="ghost"
                  data-testid="preferences-viewport-start-reset"
                  (click)="preferences.set({ viewportStart: null })"
                >
                  Use Patina’s
                </button>
              }
            </div>
            <p class="text-foreground-muted text-xs">
              Make Default in the viewport's Display keeps its light, background, grid and finish for every project that
              has kept none of its own.
            </p>
          </section>

          <section class="flex flex-col gap-2">
            <h3 class="section-label">Keys</h3>
            <div class="flex items-center gap-3">
              <span class="text-foreground-muted min-w-0 flex-1 text-xs">Every key, and presets from other apps.</span>
              <button type="button" xuiButton size="sm" data-testid="preferences-keys" (click)="showKeys()">
                {{ commands.label('shortcuts') }}
              </button>
            </div>
          </section>

          <section class="flex flex-col gap-2" data-testid="preferences-autosave">
            <h3 class="section-label">Autosave</h3>
            <p class="text-foreground-muted text-xs">
              The engine keeps open, unsaved work in the library's cache, never over a document, and offers it back
              after a crash or a restart. These settings are the engine's, so every app attached to it uses them.
            </p>
            @if (autosave(); as held) {
              <label class="flex items-center gap-3">
                <span class="w-36 shrink-0">Autosave</span>
                <xui-switch
                  aria-label="Autosave"
                  data-testid="preferences-autosave-on"
                  [checked]="held.intervalSeconds > 0"
                  (checkedChange)="saveAutosave({ intervalSeconds: $event ? 60 : 0 })"
                />
              </label>
              @if (held.intervalSeconds > 0) {
                <label class="flex items-center gap-3">
                  <span class="w-36 shrink-0">At least every</span>
                  <xui-numeric-input
                    class="w-28"
                    size="sm"
                    aria-label="Autosave interval in minutes"
                    data-testid="preferences-autosave-interval"
                    [min]="1"
                    [max]="mostMinutes"
                    [stepSize]="1"
                    [value]="minutes(held.intervalSeconds)"
                    (valueChange)="saveAutosave({ intervalSeconds: Math.max(1, $event ?? 1) * 60 })"
                  />
                  <span class="text-foreground-muted text-xs">minutes while editing goes on</span>
                </label>
                <label class="flex items-center gap-3">
                  <span class="w-36 shrink-0">After a pause of</span>
                  <xui-numeric-input
                    class="w-28"
                    size="sm"
                    aria-label="Autosave after a pause, in seconds"
                    data-testid="preferences-autosave-idle"
                    [min]="0"
                    [max]="mostSeconds"
                    [stepSize]="1"
                    [value]="held.idleSeconds"
                    (valueChange)="saveAutosave({ idleSeconds: $event ?? 0 })"
                  />
                  <span class="text-foreground-muted text-xs">seconds; 0 waits for the interval</span>
                </label>
                <label class="flex items-center gap-3">
                  <span class="w-36 shrink-0">Keep the last</span>
                  <xui-numeric-input
                    class="w-28"
                    size="sm"
                    aria-label="Autosaves kept of each document"
                    data-testid="preferences-autosave-keep"
                    [min]="1"
                    [max]="mostKept"
                    [stepSize]="1"
                    [value]="held.keep"
                    (valueChange)="saveAutosave({ keep: $event ?? 1 })"
                  />
                  <span class="text-foreground-muted text-xs">autosaves of each document</span>
                </label>
              }
            } @else if (!autosaveFailure()) {
              <p class="text-foreground-muted text-xs">Asking the engine…</p>
            }
            @if (autosaveFailure(); as reason) {
              <xui-callout color="error" compact data-testid="preferences-autosave-failure">{{ reason }}</xui-callout>
            }
          </section>

          <section class="flex flex-col gap-2" data-testid="preferences-cache">
            <h3 class="section-label">Cache</h3>
            @if (cache(); as held) {
              <p class="text-foreground-muted text-xs">
                Previews, bakes, compiled kernels and autosaves of the open library. Deleting it loses nothing but the
                time to make them again.
              </p>
              <div class="flex items-center gap-3">
                <span class="min-w-0 flex-1 truncate text-xs select-text" [title]="held.path">{{ held.path }}</span>
                <span class="readout shrink-0" data-testid="preferences-cache-size">{{ size(held.bytes) }}</span>
                @if (canReveal) {
                  <button type="button" xuiButton size="sm" variant="ghost" (click)="reveal(held.path)">
                    Show in File Browser
                  </button>
                }
              </div>
            } @else {
              <p class="text-foreground-muted text-xs" data-testid="preferences-cache-none">
                {{ cacheFailure() ?? 'No library is open, so there is no cache to show.' }}
              </p>
            }
          </section>
        </div>
      </xui-dialog-body>
      <xui-dialog-footer>
        <button type="button" xuiButton color="primary" data-testid="preferences-done" (click)="open.set(false)">
          Done
        </button>
      </xui-dialog-footer>
    </xui-dialog>
  `
})
export class PreferencesDialog {
  readonly open = model(false);
  protected readonly preferences = inject(Preferences);
  protected readonly commands = inject(Commands);
  private readonly library = inject(LibraryStore);

  protected readonly themes = Themes;
  protected readonly libraryViews = LibraryViews;
  protected readonly libraryTabs = LibraryTabs;
  protected readonly shelfViews = ShelfViews;
  protected readonly cameraSpeeds = CameraSpeedOptions;
  protected readonly mostMinutes = MostMinutes;
  protected readonly mostSeconds = MostSeconds;
  protected readonly mostKept = MostKept;
  protected readonly Math = Math;
  protected readonly canReveal = desktop() !== undefined;

  protected readonly autosave = signal<AutosavePreferences | null>(null);
  protected readonly autosaveFailure = signal<string | null>(null);
  protected readonly cache = signal<CacheInfo | null>(null);
  protected readonly cacheFailure = signal<string | null>(null);

  constructor() {
    effect(() => {
      if (this.open()) {
        untracked(() => void this.read());
      }
    });
  }

  protected minutes(seconds: number): number {
    return Math.max(1, Math.round(seconds / 60));
  }

  protected size(count: number): string {
    return bytes(count);
  }

  protected showKeys(): void {
    this.open.set(false);
    this.commands.run('shortcuts');
  }

  protected reveal(path: string): void {
    void desktop()?.reveal(path);
  }

  /** Changes autosave on the Server, which keeps it in the person's preferences file. */
  protected async saveAutosave(change: Partial<AutosavePreferences>): Promise<void> {
    const held = this.autosave();
    if (!held) {
      return;
    }

    const next = { ...held, ...change };
    this.autosave.set(next);
    try {
      const client = await this.library.api();
      const saved = unwrap(await client.PUT('/api/v1/preferences', { body: { autosave: next } }));
      this.autosave.set(saved.autosave);
      this.autosaveFailure.set(null);
    } catch (failure) {
      this.autosave.set(held);
      this.autosaveFailure.set(messageOf(failure));
    }
  }

  private async read(): Promise<void> {
    const client = await this.library.api();
    try {
      this.autosave.set(unwrap(await client.GET('/api/v1/preferences')).autosave);
      this.autosaveFailure.set(null);
    } catch (failure) {
      this.autosaveFailure.set(messageOf(failure));
    }

    try {
      this.cache.set(unwrap(await client.GET('/api/v1/library/cache')));
      this.cacheFailure.set(null);
    } catch (failure) {
      this.cache.set(null);
      this.cacheFailure.set(this.library.state().kind === 'none' ? null : messageOf(failure));
    }
  }
}
