import { angleOf, snapAngle } from './angle';

describe('angle dial', () => {
  it('points right at zero and turns anticlockwise on screen, where y grows downwards', () => {
    expect(angleOf(10, 0)).toBe(0);
    expect(angleOf(0, -10)).toBe(90);
    expect(angleOf(-10, 0)).toBe(180);
    expect(angleOf(0, 10)).toBe(270);
  });

  it('snaps to a step and wraps a full turn back to zero', () => {
    expect(snapAngle(44, 15)).toBe(45);
    expect(snapAngle(359, 15)).toBe(0);
    expect(snapAngle(12.3, 0)).toBe(12.3);
  });
});
