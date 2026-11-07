import { describe, expect, it } from 'vitest';
import { DefaultCamera, nudge, orbit, pan, zoom } from './orbit';

describe('nudge', () => {
  it('turns on the arrows as a drag would', () => {
    expect(nudge(DefaultCamera, 'ArrowRight', false)).toEqual(orbit(DefaultCamera, 24, 0));
    expect(nudge(DefaultCamera, 'ArrowUp', false)).toEqual(orbit(DefaultCamera, 0, -24));
  });

  it('pans on the arrows with shift held', () => {
    expect(nudge(DefaultCamera, 'ArrowLeft', true)).toEqual(pan(DefaultCamera, -24, 0));
  });

  it('zooms on plus and minus, with or without shift on the plus key', () => {
    expect(nudge(DefaultCamera, '+', true)).toEqual(zoom(DefaultCamera, -120));
    expect(nudge(DefaultCamera, '=', false)).toEqual(zoom(DefaultCamera, -120));
    expect(nudge(DefaultCamera, '-', false)).toEqual(zoom(DefaultCamera, 120));
  });

  it('leaves other keys to the rest of the app', () => {
    expect(nudge(DefaultCamera, 'b', false)).toBeUndefined();
  });
});
