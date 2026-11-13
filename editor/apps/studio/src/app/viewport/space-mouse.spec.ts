import { describe, expect, it } from 'vitest';
import { DefaultCamera } from './orbit';
import { Still, moving, readButtons, readReport, spaceStep } from './space-mouse';

/** A report's bytes: little-endian 16-bit axes, as a 3D mouse sends them. */
function report(...values: number[]): DataView {
  const view = new DataView(new ArrayBuffer(values.length * 2));
  values.forEach((value, at) => view.setInt16(at * 2, value, true));
  return view;
}

describe('a 3D mouse (#163)', () => {
  it('reads a push and a turn from one report on newer devices, and from two on older ones', () => {
    const newer = readReport(1, report(350, -175, 0, 0, 0, 350), Still);
    expect(newer).toEqual({ move: [1, -0.5, 0], turn: [0, 0, 1] });

    const pushed = readReport(1, report(0, 0, 350), Still);
    const older = readReport(2, report(-350, 0, 0), pushed);
    expect(older).toEqual({ move: [0, 0, 1], turn: [-1, 0, 0] });
    expect(readReport(1, report(0, 0, 0), older).turn).toEqual([-1, 0, 0]);
  });

  it('takes a cap barely off its rest as still, and holds a reading past full travel at full', () => {
    const resting = readReport(1, report(8, -10, 5, 3, -2, 1), Still);
    expect(moving(resting)).toBe(false);
    expect(readReport(1, report(700, 0, 0), Still).move[0]).toBe(1);
  });

  it('reads the buttons held from their own report, and nothing else from it', () => {
    const view = new DataView(new Uint8Array([0b10, 0]).buffer);
    expect(readButtons(3, view)).toBe(2);
    expect(readButtons(1, report(1, 2, 3))).toBeNull();
    expect(readReport(3, view, Still)).toBe(Still);
  });

  it('turns, slides and zooms the camera by how long the cap is held, and no more than a short frame at once', () => {
    const twisted = spaceStep(DefaultCamera, { move: [0, 0, 0], turn: [0, 0, 1] }, 0.02);
    expect(twisted.yaw).not.toBe(DefaultCamera.yaw);
    expect(twisted.pitch).toBe(DefaultCamera.pitch);
    const longer = spaceStep(DefaultCamera, { move: [0, 0, 0], turn: [0, 0, 1] }, 0.04);
    expect(DefaultCamera.yaw - longer.yaw).toBeCloseTo(2 * (DefaultCamera.yaw - twisted.yaw));
    expect(spaceStep(DefaultCamera, { move: [0, 0, 0], turn: [0, 0, 1] }, 5)).toEqual(
      spaceStep(DefaultCamera, { move: [0, 0, 0], turn: [0, 0, 1] }, 0.05)
    );

    const slid = spaceStep(DefaultCamera, { move: [1, 0, 0], turn: [0, 0, 0] }, 0.02);
    expect([slid.yaw, slid.pitch, slid.distance]).toEqual([
      DefaultCamera.yaw,
      DefaultCamera.pitch,
      DefaultCamera.distance
    ]);
    expect(slid.panX).not.toBe(0);

    const away = spaceStep(DefaultCamera, { move: [0, 1, 0], turn: [0, 0, 0] }, 0.02);
    expect(away.distance).toBeGreaterThan(DefaultCamera.distance);
    expect(spaceStep(DefaultCamera, Still, 0.02)).toEqual(DefaultCamera);
  });
});
