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
  const edit = vi.fn((..._commands: unknown[]) => Promise.resolve(true));
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
      channels: channels.map(usage => ({ usage, format: usage === 'height' ? 'Rgba16Float' : 'Rgba8' }))
    }),
    channel: signal('baseColor'),
    vocabulary: signal({
      usages: [
        { value: 'baseColor', label: 'Base Color' },
        { value: 'roughness', label: 'Roughness' },
        { value: 'occlusion', label: 'Ambient Occlusion' }
      ],
      formats: [
        { value: 'Rgba8', label: '8-bit' },
        { value: 'Rgba16Float', label: '16-bit float' },
        { value: 'Rgba32Float', label: '32-bit float' }
      ]
    }),
    // A fill is chosen, which no brush paints: the document's settings are there all the same (#201).
    canPaint: signal(false),
    showChannel: vi.fn(),
    applyChannelsToAllSets: vi.fn(() =>
      Promise.resolve({
        sets: [
          { set: 'Strap', path: 'strap.vxlayers', added: ['occlusion'], open: false },
          { set: 'Lid', path: 'lid.vxlayers', added: [], open: true }
        ]
      })
    ),
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
          textureSets: signal([{ material: 'Body' }, { material: 'Strap' }, { material: 'Lid' }]),
          settingsOpen: signal(false)
        }
      }
    ]
  });
  const fixture = TestBed.createComponent(DocumentSection);
  await fixture.whenStable();
  await new Promise(resolve => setTimeout(resolve));
  await fixture.whenStable();
  return { element: fixture.nativeElement as HTMLElement, edit, asked, fixture, paint };
}

describe('DocumentSection', () => {
  it('shows the document, its size and its channels, and adds a channel whatever is chosen', async () => {
    const { element, edit, fixture } = await render();
    const text = (id: string) => element.querySelector(`[data-testid=${id}]`)?.textContent.trim();

    expect(text('paint-project')).toBe('Crate');
    expect(text('paint-mesh-name')).toBe('crate_low.glb');
    expect(text('paint-document-set')).toBe('Body');
    expect(text('texture-set-width')).toContain('2048');
    expect(text('texture-set-height')).toContain('1024');
    expect(text('paint-channel-roughness')).toBe('Roughness');

    element.querySelector<HTMLElement>('[data-testid=paint-add-channel]')?.click();
    fixture.detectChanges();
    await fixture.whenStable();
    const offered = [...document.querySelectorAll<HTMLElement>('[role=menu] button[data-testid^=paint-add-channel-]')];
    expect(offered.map(item => item.textContent.trim())).toEqual(['Ambient Occlusion']);
    offered[0]?.click();

    expect(edit).toHaveBeenCalledWith({ op: 'addChannel', usage: 'occlusion' });
  });

  it('removes any channel of a set with more than one (#141), and offers none to remove from a set of one', async () => {
    const { element, edit, fixture } = await render();

    element.querySelector<HTMLElement>('[data-testid=paint-remove-channel]')?.click();
    fixture.detectChanges();
    await fixture.whenStable();
    const offered = [
      ...document.querySelectorAll<HTMLElement>('[role=menu] button[data-testid^=paint-remove-channel-]')
    ];
    expect(offered.map(item => item.textContent.trim())).toEqual(['Base Color', 'Roughness']);
    offered[1]?.click();
    expect(edit).toHaveBeenCalledWith({ op: 'removeChannel', usage: 'roughness' });

    TestBed.resetTestingModule();
    const single = await render(['baseColor']);
    expect(single.element.querySelector('[data-testid=paint-remove-channel]')).toBeNull();
  });

  it('gives the model’s other texture sets this one’s channels and says what each gained (#141)', async () => {
    const { element, fixture, paint } = await render();

    element.querySelector<HTMLElement>('[data-testid=paint-apply-channels]')?.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(paint.applyChannelsToAllSets).toHaveBeenCalled();
    expect(element.querySelector('[data-testid=paint-applied-channels]')?.textContent.trim()).toBe(
      'Strap gained Ambient Occlusion. Lid had them all.'
    );
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

  it('carries the paint into a new UV set as one step, unless asked to leave it on the map (#210)', async () => {
    const { element, edit, fixture } = await render();
    const section = fixture.componentInstance as unknown as {
      setUvSet(uvSet: number): void;
      carryPaint: { set(on: boolean): void };
    };

    expect(element.querySelector('[data-testid=texture-set-carry-paint]')).not.toBeNull();
    section.setUvSet(1);
    expect(edit).toHaveBeenLastCalledWith({ op: 'setSettings', uvSet: 1 }, { op: 'carryPaint', fromUvSet: 0 });

    section.carryPaint.set(false);
    section.setUvSet(1);
    expect(edit).toHaveBeenLastCalledWith({ op: 'setSettings', uvSet: 1 });
  });

  it('sets height to normal only for a set with a height channel', async () => {
    const { element } = await render(['baseColor', 'height']);

    expect(element.querySelector('[data-testid=texture-set-height-normal]')).not.toBeNull();
  });

  it('paints the channel being painted at another depth, one step (#19)', async () => {
    const { element, edit, fixture, paint } = await render(['baseColor', 'height']);
    const depth = () => element.querySelector('[data-testid=paint-channel-depth]');
    const pressed = () => depth()?.querySelector('[aria-pressed=true], [aria-checked=true]')?.textContent.trim();
    expect(pressed()).toBe('8-bit');

    const wide = [...(depth()?.querySelectorAll('button') ?? [])].find(
      button => button.textContent.trim() === '16-bit float'
    );
    wide?.click();
    await fixture.whenStable();
    expect(edit).toHaveBeenCalledWith({ op: 'setChannelFormat', usage: 'baseColor', format: 'Rgba16Float' });

    paint.channel.set('height');
    await fixture.whenStable();
    expect(pressed()).toBe('16-bit float');
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
