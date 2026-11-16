import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { StackState } from '@patina/api-client';
import { describe, expect, it, vi } from 'vitest';
import { isScalarChannel, isUnshadedChannel } from '../core/labels';
import { ViewportJump } from '../viewport/viewport-jump';
import { ChannelOnMesh, ChannelViewFolder } from './channel-on-mesh';
import { PaintStore } from './paint-store';

function setup() {
  const state = signal<StackState | null>({
    id: 's1',
    revision: 1,
    channels: [{ usage: 'baseColor' }, { usage: 'user0' }, { usage: 'user1', colour: true }]
  } as unknown as StackState);
  const exportMaps = vi.fn(() =>
    Promise.resolve([
      { usage: 'baseColor', file: 'C:\\lib\\.ptcache\\channel-view\\Crate_baseColor.png' },
      { usage: 'user0', file: 'C:\\lib\\.ptcache\\channel-view\\Crate_user0.png' }
    ])
  );
  TestBed.configureTestingModule({ providers: [{ provide: PaintStore, useValue: { state, exportMaps } }] });
  const shown = TestBed.inject(ChannelOnMesh);
  const jump = TestBed.inject(ViewportJump);
  // The viewport's client does this when it takes a target.
  const take = () => jump.shownPicture.set(jump.target()?.to.picture ?? null);
  return { state, exportMaps, shown, jump, take };
}

describe('ChannelOnMesh (#298)', () => {
  it('writes the maps and shows the channel’s in the picture view, again on each change', async () => {
    const { state, exportMaps, shown, jump, take } = setup();
    shown.show('user0');
    TestBed.tick();
    await vi.waitFor(() => expect(jump.target()?.to.picture).toBe('C:\\lib\\.ptcache\\channel-view\\Crate_user0.png'));
    expect(exportMaps).toHaveBeenCalledWith(ChannelViewFolder);
    expect(jump.target()?.to.view).toBe('picture');
    take();
    expect(shown.shows('c:/lib/.ptcache/channel-view/crate_user0.png')).toBe(true);

    state.update(held => (held ? { ...held, revision: 2 } : held));
    TestBed.tick();
    await vi.waitFor(() => expect(exportMaps).toHaveBeenCalledTimes(2));

    // The same channel again puts the material back.
    shown.show('user0');
    expect(shown.channel()).toBeNull();
    expect(jump.target()?.to.view).toBe('lit');
  });

  it('lets go when another view is chosen, or the channel leaves the document', async () => {
    const { state, exportMaps, shown, jump, take } = setup();
    shown.show('user0');
    TestBed.tick();
    await vi.waitFor(() => expect(jump.target()?.to.picture).toContain('user0'));
    take();
    jump.shownPicture.set(null);
    TestBed.tick();
    expect(shown.channel()).toBeNull();

    shown.show('user0');
    TestBed.tick();
    await vi.waitFor(() => expect(exportMaps).toHaveBeenCalledTimes(2));
    state.update(held => (held ? { ...held, revision: 3, channels: [] } : held));
    TestBed.tick();
    expect(shown.channel()).toBeNull();
  });
});

describe('isScalarChannel (#298)', () => {
  it('reads a standard channel by its usage and a user channel by its color flag', () => {
    const channels = [{ usage: 'user0' }, { usage: 'user1', colour: true }, { usage: 'roughness' }];
    expect(isScalarChannel('roughness', channels)).toBe(true);
    expect(isScalarChannel('baseColor', channels)).toBe(false);
    expect(isScalarChannel('user0', channels)).toBe(true);
    expect(isScalarChannel('user1', channels)).toBe(false);
    expect(isScalarChannel('user2', [])).toBe(true);
  });
});

describe('OpenPBR channels (#142)', () => {
  it('are seen on the mesh as their maps hold them, a weight as one number and the sheen as a color', () => {
    for (const usage of ['coat', 'coatRoughness', 'sheen', 'sheenColor', 'sheenRoughness', 'transmission']) {
      expect(isUnshadedChannel(usage)).toBe(true);
      expect(isScalarChannel(usage, [])).toBe(usage !== 'sheenColor');
    }

    for (const usage of ['specularWeight', 'specularColor', 'anisotropy', 'coatColor', 'coatNormal']) {
      expect(isUnshadedChannel(usage)).toBe(true);
      expect(isScalarChannel(usage, [])).toBe(usage === 'specularWeight' || usage === 'anisotropy');
    }

    for (const usage of ['anisotropyRotation', 'subsurface', 'subsurfaceColor', 'subsurfaceRadius', 'thinFilm']) {
      expect(isUnshadedChannel(usage)).toBe(true);
      expect(isScalarChannel(usage, [])).toBe(usage !== 'subsurfaceColor');
    }

    expect(isUnshadedChannel('user3')).toBe(true);
    expect(isUnshadedChannel('roughness')).toBe(false);
  });
});
