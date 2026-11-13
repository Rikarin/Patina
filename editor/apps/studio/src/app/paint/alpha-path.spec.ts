import { alphaPath } from './brush-panel';

describe('alphaPath', () => {
  it('names a picture from the folder of the stack the brush paints (#122)', () => {
    expect(alphaPath('projects/crate/pictures/scratch.png', 'projects/crate/Body.vxlayers')).toBe(
      'pictures/scratch.png'
    );
    expect(alphaPath('projects/crate/scratch.png', 'projects/crate/sets/Body.vxlayers')).toBe('../scratch.png');
    expect(alphaPath('projects\\crate\\a.png', 'projects/crate/Body.vxlayers')).toBe('a.png');
  });
});
