import { recentProjectsOf } from './start-pane';

const document = (project: string, path: string) => ({ library: 'l', project, path, name: path });

describe('recentProjectsOf', () => {
  it('lists each project of the recent documents once, as recent as its newest document (#162)', () => {
    const recent = recentProjectsOf([
      document('props/crate', 'projects/props/crate/a.yaml'),
      document('barrel', 'projects/barrel/b.yaml'),
      document('props/crate', 'projects/props/crate/c.yaml')
    ]);

    expect(recent).toEqual([
      { path: 'props/crate', name: 'crate' },
      { path: 'barrel', name: 'barrel' }
    ]);
  });

  it('offers no more than it shows', () => {
    const many = Array.from({ length: 10 }, (_, at) =>
      document(`p${at.toString()}`, `projects/p${at.toString()}/d.yaml`)
    );

    expect(recentProjectsOf(many, 3).map(project => project.path)).toEqual(['p0', 'p1', 'p2']);
  });
});
