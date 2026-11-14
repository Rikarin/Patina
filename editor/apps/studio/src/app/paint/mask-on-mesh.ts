import { Injectable, effect, inject, signal, untracked } from '@angular/core';
import { messageOf } from '../core/messages';
import { ViewportJump } from '../viewport/viewport-jump';
import { PaintStore } from './paint-store';

/** Where the Server writes a mask shown on the mesh, under the library: its cache, which loses nothing if deleted. */
export const MaskViewFolder = '.ptcache/mask-view';

/**
 * A layer's mask seen on the model on its own, white where the layer shows and black where it does not (#109). The
 * Server evaluates the mask alone, as Export Mask Alone does, into the library's cache, and the viewport's picture view
 * shows that file; each edit to the stack writes it again, one at a time, so it keeps up with painting. Choosing another
 * view in the viewport, closing the stack or removing the layer puts the material back.
 */
@Injectable({ providedIn: 'root' })
export class MaskOnMesh {
  private readonly paint = inject(PaintStore);
  private readonly jump = inject(ViewportJump);

  /** The layer whose mask the viewport shows, or null for the material. */
  readonly layer = signal<string | null>(null);
  readonly failure = signal<string | null>(null);
  private file: string | null = null;
  private stack: string | null = null;
  private writing = false;
  /** How many writes have been asked for, so one asked during another is written after it. */
  private asked = 0;

  constructor() {
    // Every change to the stack, each a new state, writes the mask again.
    effect(() => {
      const layer = this.layer();
      const state = this.paint.state();
      if (layer === null) {
        return;
      }

      if (state?.id !== this.stack || !state.layers.some(held => held.id === layer)) {
        untracked(() => this.hide());
        return;
      }

      untracked(() => {
        this.asked++;
        void this.write();
      });
    });
    // Another view chosen in the viewport ends this: the mask is not written again over what was asked for.
    effect(() => {
      const shown = this.jump.shownPicture();
      if (this.file !== null && !samePath(shown, this.file) && !this.writing) {
        untracked(() => this.forget());
      }
    });
  }

  /** Shows a layer's mask on the model, or the material again when it is the one shown. */
  toggle(layer: string): void {
    if (this.layer() === layer) {
      this.hide();
      return;
    }

    this.failure.set(null);
    this.stack = this.paint.state()?.id ?? null;
    this.file = null;
    this.layer.set(layer);
  }

  /** Puts the material back on the model. */
  hide(): void {
    const showing = this.file !== null && samePath(this.jump.shownPicture(), this.file);
    this.forget();
    if (showing) {
      this.jump.go({ view: 'lit' });
    }
  }

  private forget(): void {
    this.layer.set(null);
    this.file = null;
    this.stack = null;
  }

  /** Writes the mask and shows it; asked while a write is under way, it writes once more when that one is done. */
  private async write(): Promise<void> {
    const layer = this.layer();
    if (layer === null) {
      return;
    }

    if (this.writing) {
      return;
    }

    this.writing = true;
    const asked = this.asked;
    try {
      const maps = await this.paint.exportLayer(layer, MaskViewFolder, true);
      const file = maps?.[0]?.file ?? null;
      if (file && this.layer() === layer) {
        this.file = file;
        this.jump.go({ view: 'picture', picture: file });
      }
    } catch (failure) {
      this.failure.set(messageOf(failure));
      this.forget();
    } finally {
      this.writing = false;
    }

    if (this.asked !== asked) {
      await this.write();
    }
  }
}

/** Whether two paths the Server gave name one file: Windows writes either slash and any case. */
function samePath(one: string | null, other: string): boolean {
  const plain = (path: string) => path.replaceAll('\\', '/').toLowerCase();
  return one !== null && plain(one) === plain(other);
}
