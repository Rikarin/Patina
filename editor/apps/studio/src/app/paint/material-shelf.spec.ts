import type { MaterialInfo } from '@patina/api-client';
import { shelfMatches } from './material-shelf';

const rust: MaterialInfo = {
  name: 'Rust',
  kind: 'graph',
  source: 'projects/props/wear/rust.vxtexgraph',
  role: 'mask',
  origin: 'library',
  channels: ['baseColor'],
  project: 'Props'
};

describe('shelfMatches', () => {
  it('finds a material by its name, its type, its kind, where it is from and its path', () => {
    for (const search of ['rust', 'mask', 'masks', 'graph', 'other projects', 'props', 'wear/rust', '']) {
      expect(shelfMatches(rust, search)).toBe(true);
    }
  });

  it('needs every word', () => {
    expect(shelfMatches(rust, 'rust mask')).toBe(true);
    expect(shelfMatches(rust, 'rust brush')).toBe(false);
  });
});
