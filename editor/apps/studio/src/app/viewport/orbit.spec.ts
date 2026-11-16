import type { MeshCameraFacts } from '@patina/api-client';
import { describe, expect, it } from 'vitest';
import {
  DefaultCamera,
  type FileView,
  Framed,
  framed,
  nudge,
  orbit,
  padView,
  pan,
  snapped,
  throughCamera,
  zoom
} from './orbit';

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
    expect(snapped(away, 'top').pitch).toBe(Math.PI / 2);
    expect(snapped(away, 'bottom').pitch).toBe(-Math.PI / 2);
  });

  it('keeps a top view when a drag starts from it, rather than jumping to the drag limit', () => {
    const top = snapped(away, 'top');
    expect(orbit(top, 10, 0).pitch).toBe(top.pitch);
  });
});

describe('free orbit', () => {
  it('tips on over the pole rather than stopping at it', () => {
    const top = snapped(DefaultCamera, 'top');
    expect(orbit(top, 0, 100).pitch).toBeCloseTo(Math.PI / 2 + 0.8);
  });

  it('spins the other way round the up axis once the view has turned over, so the model follows the pointer', () => {
    const over = { ...DefaultCamera, pitch: 2.4 };
    expect(orbit(over, 10, 0).yaw).toBeGreaterThan(over.yaw);
    expect(orbit(DefaultCamera, 10, 0).yaw).toBeLessThan(DefaultCamera.yaw);
  });

  it('keeps the pitch within half a turn of level however far it goes', () => {
    let camera = DefaultCamera;
    for (let step = 0; step < 40; step++) {
      camera = orbit(camera, 0, 100);
      expect(Math.abs(camera.pitch)).toBeLessThanOrEqual(Math.PI);
    }
  });

  it('reads the number pad as Blender does', () => {
    expect(padView('Numpad1', false)).toBe('front');
    expect(padView('Numpad1', true)).toBe('back');
    expect(padView('Numpad3', false)).toBe('right');
    expect(padView('Numpad7', true)).toBe('bottom');
    expect(padView('Digit1', false)).toBeUndefined();
  });
});

describe('a file camera (#144)', () => {
  const low = [-1, -1, -1];
  const high = [1, 1, 1];
  const radius = Math.sqrt(3);

  function shot(position: number[], forward: number[], change: Partial<MeshCameraFacts> = {}): MeshCameraFacts {
    const length = Math.hypot(...forward);
    return {
      name: 'Shot',
      position,
      forward: forward.map(lane => lane / length),
      up: [0, 1, 0],
      fieldOfView: 0.8,
      aspectRatio: 1.5,
      orthographicHeight: 0,
      ...change
    };
  }

  /** Where the Server's orbit camera puts the eye, as OrbitCamera.Eye works it out, for a pane this wide. */
  function eye({ camera, fieldOfView }: FileView, aspect: number): number[] {
    const direction = [
      -Math.cos(camera.pitch) * Math.sin(camera.yaw),
      -Math.sin(camera.pitch),
      -Math.cos(camera.pitch) * Math.cos(camera.yaw)
    ];
    const half = fieldOfView / 2;
    const reach = (camera.distance * radius) / Math.sin(Math.min(half, Math.atan(aspect * Math.tan(half))));
    const target = [camera.panX * radius, camera.panY * radius, camera.panZ * radius];
    return target.map((lane, axis) => lane - (direction[axis] ?? 0) * reach);
  }

  it('stands where the file camera stands and looks where it looks, in a wide pane and a tall one', () => {
    const file = shot([3, 4, 5], [-3, -3, -5]);
    for (const aspect of [1.6, 0.6]) {
      const view = throughCamera(file, low, high, aspect, 0.7);
      eye(view, aspect).forEach((lane, axis) => expect(lane).toBeCloseTo(file.position[axis] ?? 0, 4));
      expect(view.fieldOfView).toBe(0.8);
      expect(view.orthographic).toBe(false);
    }
  });

  it('circles the point on its line of sight nearest the model, so a camera at the model looks at its centre', () => {
    const view = throughCamera(shot([0, 0, 6], [0, 0, -1]), low, high, 1, 0.7);
    expect(view.camera.yaw).toBeCloseTo(0);
    expect(view.camera.pitch).toBeCloseTo(0);
    expect([view.camera.panX, view.camera.panY, view.camera.panZ].map(lane => lane + 0)).toEqual([0, 0, 0]);
  });

  it('circles a model behind it as far ahead as the model is', () => {
    const view = throughCamera(shot([0, 0, 6], [0, 0, 1]), low, high, 1, 0.7);
    expect(view.camera.panZ * radius).toBeCloseTo(12);
  });

  it('keeps the lens of an orthographic view and sees as tall a slab as the file says', () => {
    const file = shot([0, 0, 6], [0, 0, -1], { fieldOfView: 0, orthographicHeight: 4 });
    const view = throughCamera(file, low, high, 2, 0.7);
    expect(view.orthographic).toBe(true);
    expect(view.fieldOfView).toBe(0.7);
    const perspective = (view.camera.distance * radius) / Math.sin(0.35);
    expect(2 * perspective * Math.tan(0.35)).toBeCloseTo(4);
  });
});
