import { type BarFit, nextFit } from './bar-fit';

describe('a tool bar fitted by measuring it (#184)', () => {
  it('steps down while what it holds runs past its end, whatever the font made that width', () => {
    const needs = new Map<BarFit, number>();
    // 31 pixels over, as Linux's fonts ran the 2D View's bar at a width where Windows' fitted.
    expect(nextFit('full', 431, 400, needs)).toBe('short');
    expect(nextFit('short', 412, 400, needs)).toBe('list');
    expect(nextFit('list', 380, 400, needs)).toBe('list');
    // At the last, what is used least goes into a menu, and that is as far as it goes.
    expect(nextFit('list', 410, 400, needs)).toBe('menu');
    expect(nextFit('menu', 420, 400, needs)).toBe('menu');
  });

  it('steps back up only once it has the room the wider fit needed', () => {
    const needs = new Map<BarFit, number>([
      ['full', 431],
      ['short', 412]
    ]);
    expect(nextFit('list', 380, 411, needs)).toBe('list');
    expect(nextFit('list', 380, 412, needs)).toBe('short');
    expect(nextFit('short', 400, 430, needs)).toBe('short');
    expect(nextFit('short', 400, 431, needs)).toBe('full');
  });

  it('tries a wider fit it has never measured, and keeps what that one needs when it runs over', () => {
    const needs = new Map<BarFit, number>();
    expect(nextFit('short', 300, 400, needs)).toBe('full');
    expect(nextFit('full', 450, 400, needs)).toBe('short');
    expect(nextFit('short', 300, 400, needs)).toBe('short');
  });
});
