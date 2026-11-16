import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { MaterialInfo } from '@patina/api-client';
import { SystemStatus } from '../core/system-status';
import { LibraryStore } from '../library/library-store';
import { ProjectStore } from '../library/project-store';
import { ViewportJump } from '../viewport/viewport-jump';
import { MaterialUse } from './material-use';
import { PaintStore } from './paint-store';

function setup(library: string | null = 'C:/Libraries/Game') {
  const brush = signal({ radius: 24, alpha: 'Round' });
  const edit = vi.fn();
  const POST = vi.fn(() => Promise.resolve({ data: { alpha: 'brushes/Splatter.png' }, response: new Response() }));
  TestBed.configureTestingModule({
    providers: [
      { provide: LibraryStore, useValue: { api: () => Promise.resolve({ POST }) } },
      { provide: PaintStore, useValue: { brush, edit } },
      { provide: ProjectStore, useValue: { current: signal({ path: 'crate' }) } },
      {
        provide: SystemStatus,
        useValue: { state: signal({ kind: 'online', system: { library } }) }
      }
    ]
  });
  return { using: TestBed.inject(MaterialUse), jump: TestBed.inject(ViewportJump), brush, edit, POST };
}

const shelved = (role: string, source: string): MaterialInfo => ({
  name: 'Shelved',
  kind: role,
  source,
  role,
  origin: 'shipped',
  channels: []
});

describe('MaterialUse', () => {
  it('makes an alpha from the shelf the brush’s, and adds nothing to the stack (#203)', async () => {
    const { using, brush, edit } = setup();

    await using.use(shelved('alpha', 'alpha:Chisel'));

    expect(brush()).toEqual({ radius: 24, alpha: 'Chisel' });
    expect(edit).not.toHaveBeenCalled();
  });

  it('brings a picture shipped as an alpha into the project and makes it the brush’s (#34)', async () => {
    const { using, brush, edit, POST } = setup();

    await using.use(shelved('alpha', 'alpha-picture:Splatter'));

    expect(POST).toHaveBeenCalledWith('/api/v1/library/materials/brush', {
      body: { project: 'crate', source: 'alpha-picture:Splatter' }
    });
    expect(brush()).toEqual({ radius: 24, alpha: 'brushes/Splatter.png' });
    expect(edit).not.toHaveBeenCalled();
    expect(using.failure()).toBeNull();
  });

  it('lights the viewport with an environment from the shelf, by its file in the library (#203)', async () => {
    const { using, jump, edit } = setup();

    await using.use(shelved('environment', 'environment:projects/skies/dusk.hdr'));

    expect(jump.target()?.to).toEqual({ environment: 'C:/Libraries/Game/projects/skies/dusk.hdr' });
    expect(edit).not.toHaveBeenCalled();
    expect(using.failure()).toBeNull();
  });

  it('says why an environment cannot light the viewport with no library open', async () => {
    const { using, jump } = setup(null);

    await using.use(shelved('environment', 'environment:projects/skies/dusk.hdr'));

    expect(jump.target()).toBeNull();
    expect(using.failure()).toContain('Open a library');
  });
});
