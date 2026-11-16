import { nudged, positionOf, shareOf } from './position';

describe('position', () => {
  const unit = { minimum: 0, maximum: 1 };

  it('reads a point on the pad as a position, its origin at the top left', () => {
    expect(positionOf(0.25, 0.75, unit)).toEqual({ x: 0.25, y: 0.75 });
    expect(positionOf(0.5, 0, { minimum: -2, maximum: 2 })).toEqual({ x: 0, y: -2 });
  });

  it('holds a point dragged off the pad at its edge', () => {
    expect(positionOf(-1, 3, unit)).toEqual({ x: 0, y: 1 });
  });

  it('places a position back on the pad, and a range of nothing at its corner', () => {
    expect(shareOf({ x: 1, y: -1 }, { minimum: -2, maximum: 2 })).toEqual({ x: 0.75, y: 0.25 });
    expect(shareOf({ x: 5, y: 5 }, { minimum: 1, maximum: 1 })).toEqual({ x: 0, y: 0 });
  });

  it('nudges one lane and keeps it in range', () => {
    expect(nudged({ x: 0.5, y: 0.98 }, 'y', 0.1, unit)).toEqual({ x: 0.5, y: 1 });
    expect(nudged({ x: 0.5, y: 0.5 }, 'x', -0.1, unit)).toEqual({ x: 0.4, y: 0.5 });
  });
});
