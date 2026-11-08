import { signal } from '@angular/core';
import { describe, expect, it } from 'vitest';
import { LayerDraft } from './live-value';

describe('LayerDraft', () => {
  it('commits what was typed only to the layer it was typed for', () => {
    const chosen = signal({ id: 'base', name: 'Base' });
    const name = new LayerDraft(() => chosen().name);
    name.edit('base', 'Ground');
    expect(name.shown()).toBe('Ground');
    expect(name.take('base')).toBe('Ground');
    expect(name.shown()).toBe('Base'); // the Server's again until it echoes the rename
    expect(name.take('base')).toBeNull(); // nothing typed since
  });

  it('writes nothing onto the old layer when another row is chosen before the field loses focus', () => {
    const chosen = signal({ id: 'base', name: 'Base' });
    const name = new LayerDraft(() => chosen().name);
    name.edit('base', 'Ground');

    // Pressing the Paint row chooses it, and the panel lets go of the draft, before the blur commits for Base.
    chosen.set({ id: 'paint', name: 'Paint' });
    name.release();
    expect(name.shown()).toBe('Paint');
    expect(name.take('base')).toBeNull();
    expect(name.take('paint')).toBeNull();
  });
});
