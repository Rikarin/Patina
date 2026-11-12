import { Injectable, inject, signal } from '@angular/core';
import { type ViewportIsolation, unwrap } from '@patina/api-client';
import { messageOf } from '../core/messages';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';

const Whole: ViewportIsolation = { parts: [], materials: [] };

/**
 * What the viewport draws alone (#136): named meshes and materials, a texture set being one. The Server owns it, so
 * every client sees the same model; this mirrors it for the controls that change it.
 */
@Injectable({ providedIn: 'root' })
export class IsolationStore {
  private readonly connection = inject(SERVER_CONNECTION);
  private readonly createClient = inject(API_CLIENT_FACTORY);

  readonly isolation = signal<ViewportIsolation>(Whole);
  readonly error = signal<string | null>(null);

  /** Draws a material alone, or the whole model again if it is the one drawn alone. */
  toggleMaterial(material: string): Promise<void> {
    const held = this.isolation();
    const alone = held.parts.length === 0 && held.materials.length === 1 && held.materials[0] === material;
    return this.set(alone ? Whole : { parts: [], materials: [material] });
  }

  /** Adds a named mesh to what is drawn alone, or takes it away. */
  togglePart(part: string): Promise<void> {
    const held = this.isolation();
    const parts = held.parts.includes(part) ? held.parts.filter(name => name !== part) : [...held.parts, part];
    return this.set({ parts, materials: [] });
  }

  showAll(): Promise<void> {
    return this.set(Whole);
  }

  /** Draws these named meshes alone, hiding the rest of the model; none at all is the whole model. */
  showParts(parts: readonly string[]): Promise<void> {
    return this.set({ parts: [...parts], materials: [] });
  }

  /**
   * What the Server says it draws alone, read when the viewport connects and again when it reconnects. The Server keeps
   * isolation in memory only, so after a restart it draws the whole model and the controls say so, rather than claiming
   * parts are drawn alone that are not.
   */
  heard(isolation: ViewportIsolation | null | undefined): void {
    this.isolation.set(isolation ?? Whole);
  }

  private async set(isolation: ViewportIsolation): Promise<void> {
    this.isolation.set(isolation);
    try {
      const client = this.createClient(await this.connection());
      const state = unwrap(await client.PUT('/api/v1/viewport/isolation', { body: isolation }));
      this.isolation.set(state.isolation ?? Whole);
      this.error.set(null);
    } catch (failure) {
      this.error.set(messageOf(failure));
    }
  }
}
