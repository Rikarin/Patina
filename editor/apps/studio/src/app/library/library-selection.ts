import { Injectable, signal } from '@angular/core';
import type { AssetSummary, ProjectSummary } from '@patina/api-client';

export type LibraryItem = { kind: 'project'; project: ProjectSummary } | { kind: 'asset'; asset: AssetSummary };

/** What the Library pane has selected, which the Inspector shows. Two panes share it, so it belongs to neither. */
@Injectable({ providedIn: 'root' })
export class LibrarySelection {
  private readonly mutableItem = signal<LibraryItem | null>(null);

  readonly item = this.mutableItem.asReadonly();

  select(item: LibraryItem): void {
    this.mutableItem.set(item);
  }

  clear(): void {
    this.mutableItem.set(null);
  }

  /** The id of what is selected, or null. */
  id(): string | null {
    const item = this.mutableItem();
    if (item === null) {
      return null;
    }

    return item.kind === 'project' ? item.project.id : item.asset.id;
  }
}
