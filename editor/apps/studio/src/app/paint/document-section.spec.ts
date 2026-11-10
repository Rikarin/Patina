import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { SystemStatus } from '../core/system-status';
import { LibraryStore } from '../library/library-store';
import { ProjectStore } from '../library/project-store';
import { DocumentSection } from './document-section';
import { PaintStore } from './paint-store';

const facts = {
  format: 'glTF',
  triangles: 1200,
  parts: [],
  uvSets: 2,
  vertexColours: false,
  minimum: [0, 0, 0],
  maximum: [1, 1, 1],
  metresPerUnit: 1,
  materials: [
    { name: 'Body', triangles: 1000, parts: 2, uvSets: 2 },
    { name: 'Strap', triangles: 200, parts: 1, uvSets: 1 }
  ]
};

async function render(channels: readonly string[] = ['baseColor', 'roughness'], meshMaps: string | null = null) {
  const edit = vi.fn((_command: unknown) => Promise.resolve(true));
  const asked = vi.fn((_path: string, _init: unknown) => Promise.resolve({ data: facts, response: { ok: true } }));
  const paint = {
    state: signal({
      set: 'Body',
      width: 2048,
      height: 1024,
      uvSet: 0,
      heightNormal: 'Smooth',
      bakedNormal: 'Combine',
      bakedOcclusion: 'Combine',
      meshMaps,
      channels: channels.map(usage => ({ usage }))
    }),
    channel: signal('baseColor'),
    vocabulary: signal({
      usages: [
        { value: 'baseColor', label: 'Base Color' },
        { value: 'roughness', label: 'Roughness' },
        { value: 'occlusion', label: 'Ambient Occlusion' }
      ]
    }),
    // A fill is chosen, which no brush paints: the document's settings are there all the same (#201).
    canPaint: signal(false),
    showChannel: vi.fn(),
    edit
  };
  TestBed.configureTestingModule({
    providers: [
      { provide: PaintStore, useValue: paint },
      { provide: SystemStatus, useValue: { textureSides: signal([512, 1024, 2048, 4096]) } },
      { provide: LibraryStore, useValue: { api: () => Promise.resolve({ GET: asked }) } },
      {
        provide: ProjectStore,
        useValue: {
          current: signal({ name: 'Crate' }),
          meshName: signal('crate_low.glb'),
          mesh: signal({ path: 'projects/crate/crate_low.glb' }),
          document: signal({ kind: 'texturing', name: 'Crate' }),
          settingsOpen: signal(false)
        }
      }
    ]
  });
  const fixture = TestBed.createComponent(DocumentSection);
  await fixture.whenStable();
  await new Promise(resolve => setTimeout(resolve));
  await fixture.whenStable();
  return { element: fixture.nativeElement as HTMLElement, edit, asked, fixture };
}

describe('DocumentSection', () => {
  it('shows the document, its size and its channels, and adds a channel whatever is chosen', async () => {
    const { element, edit } = await render();
    const text = (id: string) => element.querySelector(`[data-testid=${id}]`)?.textContent.trim();

    expect(text('paint-project')).toBe('Crate');
    expect(text('paint-mesh-name')).toBe('crate_low.glb');
    expect(text('paint-document-set')).toBe('Body');
    expect(text('texture-set-width')).toContain('2048');
    expect(text('texture-set-height')).toContain('1024');
    expect(text('paint-channel-roughness')).toBe('Roughness');

    const add = element.querySelector<HTMLSelectElement>('[data-testid=paint-add-channel]');
    expect([...(add?.options ?? [])].map(option => option.text.trim())).toEqual(['Add Channel…', 'Ambient Occlusion']);
    if (add) {
      add.value = 'occlusion';
      add.dispatchEvent(new Event('change'));
    }

    expect(edit).toHaveBeenCalledWith({ op: 'addChannel', usage: 'occlusion' });
  });

  it('says what the mesh under the texture set is, and offers the UV sets all its parts have', async () => {
    const { element, asked } = await render();
    const text = (id: string) => element.querySelector(`[data-testid=${id}]`)?.textContent.trim();

    expect(asked).toHaveBeenCalledWith('/api/v1/library/mesh', {
      params: { query: { path: 'projects/crate/crate_low.glb' } }
    });
    expect(text('paint-document-mesh-facts')).toBe('1,000 triangles, 2 parts, 2 UV sets');
    expect(element.querySelector('[data-testid=texture-set-uv-set]')).not.toBeNull();
    expect(element.querySelector('[data-testid=texture-set-height-normal]')).toBeNull();
  });

  it('sets height to normal only for a set with a height channel', async () => {
    const { element } = await render(['baseColor', 'height']);

    expect(element.querySelector('[data-testid=texture-set-height-normal]')).not.toBeNull();
  });

  it('offers how the bake shades the set only once there is one', async () => {
    const { element } = await render();

    expect(element.querySelector('[data-testid=texture-set-baked-normal]')).toBeNull();
  });

  it('sends how the bake shades the set as a set setting (#86)', async () => {
    const { element, edit, fixture } = await render(['baseColor'], '/library/projects/crate/bakes/maps');
    expect(element.querySelector('[data-testid=texture-set-baked-occlusion]')?.textContent).toContain('Multiply');
    const normal = element.querySelector('[data-testid=texture-set-baked-normal]');
    const replace = [...(normal?.querySelectorAll('button') ?? [])].find(
      button => button.textContent.trim() === 'Replace'
    );
    replace?.click();
    await fixture.whenStable();

    expect(edit).toHaveBeenCalledWith({ op: 'setSettings', bakedNormal: 'Replace', bakedOcclusion: null });
  });
});
