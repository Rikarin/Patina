import { Injectable, effect, inject, signal, untracked } from '@angular/core';
import { messageOf } from '../core/messages';
import { ViewportJump } from '../viewport/viewport-jump';
import { PaintStore } from './paint-store';

/** Where the Server writes a channel shown on the mesh, under the library: its cache, which loses nothing if deleted. */
export const ChannelViewFolder = '.ptcache/channel-view';

/**
 * A channel the material does not show seen on the model on its own (#298): a user channel means nothing to the
 * viewport's shading, so it is shown as its map holds it, a grey for one number and its colors for a color. The Server
 * evaluates the texture set as an export does, into the library's cache, and the viewport's picture view shows the
 * channel's file; each edit writes it again, one at a time, so it keeps up with painting. Choosing another view in the
 * viewport, closing the document or taking the channel off it puts the material back.
 */
@Injectable({ providedIn: 'root' })
export class ChannelOnMesh {
  private readonly paint = inject(PaintStore);
  private readonly jump = inject(ViewportJump);

  /** The channel the viewport shows, by usage, or null for the material. */
  readonly channel = signal<string | null>(null);
  readonly failure = signal<string | null>(null);
  private file: string | null = null;
  private stack: string | null = null;
  private writing = false;
  /** How many writes have been asked for, so one asked during another is written after it. */
  private asked = 0;

  constructor() {
    // Every change to the document, each a new state, writes the channel again.
    effect(() => {
      const channel = this.channel();
      const state = this.paint.state();
      if (channel === null) {
        return;
      }

      if (state?.id !== this.stack || !state.channels.some(held => held.usage === channel)) {
        untracked(() => this.hide());
        return;
      }

      untracked(() => {
        this.asked++;
        void this.write();
      });
    });
    // Another view chosen in the viewport ends this: the channel is not written again over what was asked for.
    effect(() => {
      const shown = this.jump.shownPicture();
      if (this.file !== null && !samePath(shown, this.file) && !this.writing) {
        untracked(() => this.forget());
      }
    });
  }

  /** Shows a channel on the model, or the material again when it is the one shown. */
  show(channel: string | null): void {
    if (channel === null || this.channel() === channel) {
      this.hide();
      return;
    }

    this.failure.set(null);
    this.stack = this.paint.state()?.id ?? null;
    this.file = null;
    this.channel.set(channel);
  }

  /** Puts the material back on the model. */
  hide(): void {
    const showing = this.file !== null && samePath(this.jump.shownPicture(), this.file);
    this.forget();
    if (showing) {
      this.jump.go({ view: 'lit' });
    }
  }

  /** Whether a path the viewport shows is the channel's file, so the view list can name it by the channel. */
  shows(picture: string | null | undefined): boolean {
    return this.file !== null && samePath(picture ?? null, this.file);
  }

  private forget(): void {
    this.channel.set(null);
    this.file = null;
    this.stack = null;
  }

  /** Writes the maps and shows the channel's; asked while a write is under way, it writes once more when that is done. */
  private async write(): Promise<void> {
    const channel = this.channel();
    if (channel === null || this.writing) {
      return;
    }

    this.writing = true;
    const asked = this.asked;
    try {
      const maps = await this.paint.exportMaps(ChannelViewFolder);
      const file = maps?.find(map => map.usage === channel)?.file ?? null;
      if (file && this.channel() === channel) {
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
