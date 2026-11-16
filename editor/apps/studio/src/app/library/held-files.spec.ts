import { TestBed } from '@angular/core/testing';
import { HeldFiles } from './held-files';

describe('HeldFiles', () => {
  it('counts what each holder holds until it lets go, leaving out empty fields (#301)', () => {
    const held = TestBed.inject(HeldFiles);
    let mesh = '';
    const release = held.hold(() => [mesh, 'C:/Art/Game.ptlib/uploads/ab/high.fbx']);
    held.hold(() => ['{"cage":"C:/Art/Game.ptlib/uploads/cd/cage.glb"}']);

    mesh = 'C:/Art/Game.ptlib/uploads/ef/crate.glb';
    expect(held.all()).toEqual([
      'C:/Art/Game.ptlib/uploads/ef/crate.glb',
      'C:/Art/Game.ptlib/uploads/ab/high.fbx',
      '{"cage":"C:/Art/Game.ptlib/uploads/cd/cage.glb"}'
    ]);

    release();
    expect(held.all()).toEqual(['{"cage":"C:/Art/Game.ptlib/uploads/cd/cage.glb"}']);
  });
});
