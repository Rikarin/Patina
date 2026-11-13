import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { type DocumentSnapshot, unwrap } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiInputImports } from '@xui/input';
import { bytes, count } from '../core/numbers';
import { LibraryStore } from './library-store';
import { ProjectStore } from './project-store';

/**
 * The snapshots of the document in front (#209): named checkpoints of its file and paint, which outlive closing it and
 * travel with the library. Taking one saves first; restoring one keeps how the document stood as a snapshot of its own,
 * so nothing a restore replaces is lost.
 */
@Component({
  selector: 'ptn-snapshots',
  imports: [DatePipe, XuiButtonImports, XuiInputImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex min-h-0 flex-col', role: 'group', 'aria-label': 'Snapshots', 'data-testid': 'snapshots' },
  template: `
    <div class="chrome-bar flex h-8 shrink-0 items-center gap-2 px-2">
      <span class="section-label flex-1">Snapshots</span>
    </div>
    @if (path(); as path) {
      <form class="flex items-center gap-1 px-2 py-1" (submit)="take($event, path)">
        <input
          class="min-w-0 flex-1"
          xuiInput
          size="sm"
          aria-label="Snapshot name"
          placeholder="Name"
          autocomplete="off"
          data-testid="snapshot-name"
          [value]="name()"
          (input)="name.set(text($event))"
        />
        <button
          type="submit"
          xuiButton
          size="sm"
          title="Keep the document as it is saved, under this name. Unsaved edits are saved first."
          data-testid="snapshot-take"
          [disabled]="busy()"
        >
          Take Snapshot
        </button>
      </form>
      <ol
        class="min-h-0 flex-1 overflow-y-auto py-1"
        aria-label="Snapshots of this document"
        data-testid="snapshot-list"
      >
        @for (snapshot of items(); track snapshot.id) {
          <li class="hover:bg-hover flex items-center gap-2 px-2 py-0.5 text-sm" data-testid="snapshot">
            <span class="min-w-0 flex-1 leading-tight">
              <span class="block truncate" data-testid="snapshot-label">{{ snapshot.name }}</span>
              <span class="text-foreground-muted block truncate text-xs">
                {{ snapshot.taken | date: 'medium' }} · {{ size(snapshot) }}
              </span>
            </span>
            <button
              type="button"
              xuiButton
              size="sm"
              variant="ghost"
              title="Put the document back as this snapshot holds it. How it stands now is kept as a snapshot first."
              data-testid="snapshot-restore"
              [disabled]="busy()"
              (click)="restore(path, snapshot)"
            >
              Restore
            </button>
            <button
              type="button"
              xuiButton
              size="sm"
              variant="ghost"
              title="Forget this snapshot. The document is not changed."
              data-testid="snapshot-delete"
              [disabled]="busy()"
              (click)="forget(path, snapshot)"
            >
              Delete
            </button>
          </li>
        } @empty {
          <li class="text-foreground-muted px-2 py-1 text-xs">
            None yet. A snapshot keeps the document as it is saved, to come back to later.
          </li>
        }
      </ol>
    }
  `
})
export class SnapshotList {
  private readonly project = inject(ProjectStore);
  private readonly library = inject(LibraryStore);
  protected readonly name = signal('');
  protected readonly busy = signal(false);
  protected readonly items = signal<readonly DocumentSnapshot[]>([]);

  /** The document in front, whose snapshots show. */
  protected readonly path = computed(() => this.project.active()?.path ?? null);

  constructor() {
    effect(() => {
      const path = this.path();
      untracked(() => void this.read(path));
    });
  }

  protected text(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  protected size(snapshot: DocumentSnapshot): string {
    return snapshot.files === 1 ? bytes(snapshot.bytes) : `${bytes(snapshot.bytes)} in ${count(snapshot.files)} files`;
  }

  protected async take(event: Event, path: string): Promise<void> {
    event.preventDefault();
    await this.act(path, async () => {
      if (await this.project.takeSnapshot(path, this.name().trim())) {
        this.name.set('');
      }
    });
  }

  protected async restore(path: string, snapshot: DocumentSnapshot): Promise<void> {
    await this.act(path, () => this.project.restoreSnapshot(path, snapshot.id));
  }

  protected async forget(path: string, snapshot: DocumentSnapshot): Promise<void> {
    await this.act(path, () => this.project.deleteSnapshot(path, snapshot.id));
  }

  /**
   * Does one thing at a time, then reads the list again, whatever came of it. Each thing says itself why it was
   * refused (#270).
   */
  private async act(path: string, work: () => Promise<unknown>): Promise<void> {
    this.busy.set(true);
    try {
      await work();
    } catch {
      // The list read next shows what is true.
    } finally {
      this.busy.set(false);
      await this.read(path);
    }
  }

  private reading: string | null = null;

  private async read(path: string | null): Promise<void> {
    this.reading = path;
    if (path === null) {
      this.items.set([]);
      return;
    }

    try {
      const client = await this.library.api();
      const list = unwrap(await client.GET('/api/v1/library/snapshots', { params: { query: { path } } }));
      if (this.reading === path) {
        this.items.set(list.items);
      }
    } catch {
      if (this.reading === path) {
        this.items.set([]);
      }
    }
  }
}
