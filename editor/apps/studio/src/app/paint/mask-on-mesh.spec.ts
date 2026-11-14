import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { StackState } from '@patina/api-client';
import { describe, expect, it, vi } from 'vitest';
import { ViewportJump } from '../viewport/viewport-jump';
import { MaskOnMesh, MaskViewFolder } from './mask-on-mesh';
import { PaintStore } from './paint-store';

function setup() {
  const state = signal<StackState | null>({
    id: 's1',
    revision: 1,
    layers: [{ id: 'l1' }, { id: 'l2' }]
  } as unknown as StackState);
  const exportLayer = vi.fn((layer: string) => Promise.resolve([{ usage: 'mask', file: `C:\\lib\\${layer}.png` }]));
  TestBed.configureTestingModule({ providers: [{ provide: PaintStore, useValue: { state, exportLayer } }] });
  const shown = TestBed.inject(MaskOnMesh);
  const jump = TestBed.inject(ViewportJump);
  // The viewport's client does this when it takes a target.
  const take = () => jump.shownPicture.set(jump.target()?.to.picture ?? null);
  return { state, exportLayer, shown, jump, take };
}

describe('MaskOnMesh (#109)', () => {
  it('writes the mask alone and shows it in the picture view, again on each change', async () => {
    const { state, exportLayer, shown, jump, take } = setup();
    shown.toggle('l1');
    TestBed.tick();
    await vi.waitFor(() => expect(jump.target()?.to.picture).toBe('C:\\lib\\l1.png'));
    expect(exportLayer).toHaveBeenCalledWith('l1', MaskViewFolder, true);
    expect(jump.target()?.to.view).toBe('picture');
    take();

    state.update(held => (held ? { ...held, revision: 2 } : held));
    TestBed.tick();
    await vi.waitFor(() => expect(exportLayer).toHaveBeenCalledTimes(2));
    await vi.waitFor(() => expect(jump.target()?.at).toBe(2));
    take();

    // The same layer again puts the material back.
    shown.toggle('l1');
    expect(shown.layer()).toBeNull();
    expect(jump.target()?.to.view).toBe('lit');
  });

  it('lets go when another view is chosen, or the layer goes', async () => {
    const { state, exportLayer, shown, jump, take } = setup();
    shown.toggle('l2');
    TestBed.tick();
    await vi.waitFor(() => expect(jump.target()?.to.picture).toBe('C:\\lib\\l2.png'));
    take();
    jump.shownPicture.set(null);
    TestBed.tick();
    expect(shown.layer()).toBeNull();

    shown.toggle('l2');
    TestBed.tick();
    await vi.waitFor(() => expect(exportLayer).toHaveBeenCalledTimes(2));
    state.update(held => (held ? { ...held, revision: 3, layers: [] } : held));
    TestBed.tick();
    expect(shown.layer()).toBeNull();
  });
});
