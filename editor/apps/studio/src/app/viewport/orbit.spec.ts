import { describe, expect, it } from 'vitest';
import { DefaultCamera, Framed, framed, nudge, orbit, padView, pan, snapped, zoom } from './orbit';

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

describe('framing', () => {
  const away = { ...DefaultCamera, yaw: 2, pitch: -0.4, distance: 4, panX: 1, panY: -2, panZ: 0.5 };

  it('brings the whole model back from where the camera looks', () => {
    expect(framed(away)).toEqual({ ...away, distance: Framed, panX: 0, panY: 0, panZ: 0 });
  });

  it('snaps to an axis and frames the model there', () => {
    expect(snapped(away, 'front')).toEqual({ ...framed(away), yaw: 0, pitch: 0 });
    expect(snapped(away, 'right').yaw).toBeCloseTo(Math.PI / 2);
    expect(snapped(away, 'top').pitch).toBeGreaterThan(1.56);
    expect(snapped(away, 'top').pitch).toBeLessThan(Math.PI / 2);
  });

  it('keeps a top view when a drag starts from it, rather than jumping to the drag limit', () => {
    const top = snapped(away, 'top');
    expect(orbit(top, 10, 0).pitch).toBe(top.pitch);
  });

  it('reads the number pad as Blender does', () => {
    expect(padView('Numpad1', false)).toBe('front');
    expect(padView('Numpad1', true)).toBe('back');
    expect(padView('Numpad3', false)).toBe('right');
    expect(padView('Numpad7', true)).toBe('bottom');
    expect(padView('Digit1', false)).toBeUndefined();
  });
});
