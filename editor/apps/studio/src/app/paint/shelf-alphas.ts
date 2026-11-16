import { DestroyRef, Injectable, effect, inject, signal, untracked } from '@angular/core';
import { AlphaPrefix } from './material-use';
import { PaintStore } from './paint-store';

/**
 * Pictures of the brush alphas on the shelf (#203): one stamp of the brush in hand with each alpha, drawn as the brush
 * ring's stamp is, for the stack open. With no stack open an alpha shows its name alone.
 */
@Injectable({ providedIn: 'root' })
export class ShelfAlphaStamps {
  private readonly paint = inject(PaintStore);
  /** Each alpha's stamp, by its source on the shelf, as an object URL. */
  private readonly drawn = signal<ReadonlyMap<string, string>>(new Map());
  /** The alphas the shelf lists, and those asked for from the stack open. */
  private readonly wanted = new Set<string>();
  private readonly asked = new Set<string>();
  private stack: string | null = null;

  constructor() {
    // A stamp is drawn with a stack's alphas, so another stack draws them again.
    effect(() => {
      const stack = this.paint.state()?.id ?? null;
      if (stack !== this.stack) {
        untracked(() => {
          this.drop();
          this.stack = stack;
          this.draw();
        });
      }
    });
    inject(DestroyRef).onDestroy(() => this.drop());
  }

  /** An alpha's stamp, or undefined until it is drawn. */
  url(source: string): string | undefined {
    return this.drawn().get(source);
  }

  /** Asks for the stamps of the alphas among these sources, unless they are drawn or on their way. */
  want(sources: readonly string[]): void {
    for (const source of sources.filter(source => source.startsWith(AlphaPrefix))) {
      this.wanted.add(source);
    }

    this.draw();
  }

  private draw(): void {
    const stack = this.stack;
    if (!stack) {
      return;
    }

    const brush = untracked(this.paint.wholeBrush);
    for (const source of [...this.wanted].filter(source => !this.asked.has(source))) {
      this.asked.add(source);
      void this.paint.stampPreview({ ...brush, alpha: source.slice(AlphaPrefix.length) }).then(
        blob => {
          if (blob && this.stack === stack) {
            this.drawn.update(held => new Map([...held, [source, URL.createObjectURL(blob)]]));
          }
        },
        () => this.asked.delete(source) // asked again the next time the shelf is read
      );
    }
  }

  private drop(): void {
    for (const url of this.drawn().values()) {
      URL.revokeObjectURL(url);
    }

    this.drawn.set(new Map());
    this.asked.clear();
  }
}
