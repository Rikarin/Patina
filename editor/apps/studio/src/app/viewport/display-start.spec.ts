import { readPreferences } from '../core/preferences';
import { displayStart, startOf } from './display-start';
import { DefaultDisplay } from './viewport-client';

describe('displayStart', () => {
  it('opens a project that kept no display with the one made default, but for what it shows (#189)', () => {
    const own = startOf({
      ...DefaultDisplay,
      backgroundColor: '#336699',
      grid: true,
      exposure: 1.5,
      view: 'normal',
      picture: 'projects/crate/bakes/ao.png'
    });

    expect(own).not.toHaveProperty('view');
    expect(own).not.toHaveProperty('picture');
    expect(displayStart(DefaultDisplay, own)).toEqual({
      ...DefaultDisplay,
      backgroundColor: '#336699',
      grid: true,
      exposure: 1.5
    });
  });

  it('opens with Patina’s display when none was made default, or with what of an edited file it can use', () => {
    const edited = readPreferences({
      viewportStart: { grid: 'yes', exposure: 2, look: 'D:\\Looks\\warm.cube', unknown: 1, view: 'wire', shadows: {} }
    }).viewportStart;

    expect(displayStart(DefaultDisplay, null)).toEqual(DefaultDisplay);
    expect(edited).toEqual({ grid: 'yes', exposure: 2, look: 'D:\\Looks\\warm.cube', unknown: 1, view: 'wire' });
    expect(displayStart(DefaultDisplay, edited)).toEqual({
      ...DefaultDisplay,
      exposure: 2,
      look: 'D:\\Looks\\warm.cube'
    });
    expect(readPreferences({ viewportStart: [1] }).viewportStart).toBeNull();
  });
});
