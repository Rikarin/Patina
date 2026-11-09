import { TestBed } from '@angular/core/testing';
import { createPatinaClient } from '@patina/api-client';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';
import { GRAPH_SOCKET, type GraphSocket } from '../graph/graph-store';
import { CompressionView } from './compression-view';
import { ExportStore } from './export-store';
import { PresetEditor, laneOf, textOf } from './preset-editor';

const unreal = {
  id: 'unreal',
  name: 'Unreal Engine',
  engine: 'Unreal Engine',
  normal: 'y-down',
  filter: 'box',
  dither: false,
  padding: { mode: 'infinite', distance: 16, colour: [] },
  materials: [],
  textures: [
    {
      file: 'T_{project}_ORM',
      format: 'png8',
      space: 'linear',
      compression: 'none',
      mips: false,
      scale: 1,
      channels: ['occlusion.r', 'roughness.r', 'metalness.r']
    }
  ]
};

const list = {
  presets: [
    { id: 'unreal', name: 'Unreal Engine', origin: 'factory', files: [] },
    { id: 'mine', name: 'Mine', origin: 'project', files: [] }
  ],
  problems: [],
  formats: [
    { id: 'png8', label: 'PNG 8-bit', extension: '.png', lanes: [1, 3, 4], linear: false, container: false },
    { id: 'ktx2', label: 'KTX2', extension: '.ktx2', lanes: [1, 2, 3, 4], linear: false, container: true }
  ],
  paddings: [{ value: 'infinite', label: 'Dilation, infinite' }],
  filters: [],
  compressions: [
    { value: 'none', label: 'None' },
    { value: 'bc7', label: 'BC7' }
  ],
  maps: [
    { value: 'occlusion', label: 'Occlusion' },
    { value: 'roughness', label: 'Roughness' },
    { value: 'metalness', label: 'Metalness' }
  ],
  materials: [{ value: 'gltf', label: 'glTF material' }],
  tokens: ['project', 'set', 'udim']
};

function setup(routes: Record<string, (request: Request) => Response | Promise<Response>>) {
  const answer = async (input: RequestInfo | URL) => {
    const request = input as Request;
    const key = `${request.method} ${new URL(request.url).pathname.replace('/api/v1/', '')}`;
    const route = routes[key];
    return route ? route(request) : Response.json({ detail: `no route for ${key}` }, { status: 500 });
  };
  TestBed.configureTestingModule({
    providers: [
      { provide: SERVER_CONNECTION, useValue: () => Promise.resolve({ baseUrl: 'http://server', token: 'secret' }) },
      {
        provide: API_CLIENT_FACTORY,
        useValue: (connection: { baseUrl: string; token: string | null }) => createPatinaClient(connection, answer)
      },
      {
        provide: GRAPH_SOCKET,
        useValue: () => ({ onopen: null, onmessage: null, onclose: null, close: vi.fn() }) as unknown as GraphSocket
      }
    ]
  });
  return TestBed.inject(ExportStore);
}

async function settled(fixture: { whenStable(): Promise<unknown> }, wait = 0): Promise<void> {
  await new Promise(resolve => setTimeout(resolve, wait));
  await fixture.whenStable();
}

describe('laneOf', () => {
  it.each(['roughness.r', '1-roughness.r', 'baseColor.b', '0.5', '1'])('reads and writes %s as it was', text => {
    expect(textOf(laneOf(text))).toBe(text);
  });
});

describe('PresetEditor', () => {
  it('shows a built-in preset read-only, and a duplicate kept in the project with a free id', async () => {
    const puts: { url: string; body: unknown }[] = [];
    const store = setup({
      'GET export/presets': () => Response.json(list),
      'GET export/presets/unreal': () => Response.json({ document: unreal, origin: 'factory', file: null, text: '' }),
      'POST export/presets/check': () =>
        Response.json({ problem: null, files: [{ template: 'T', set: '', name: 'T_Crate_Copy_ORM.png' }], text: 'id: x' }),
      'PUT export/presets/unreal-copy': async request => {
        const body = (await request.json()) as { preset: typeof unreal };
        puts.push({ url: request.url, body });
        return Response.json({ document: body.preset, origin: 'project', file: '/l/p.yaml', text: '' });
      }
    });
    await store.load('crate');
    const fixture = TestBed.createComponent(PresetEditor);
    fixture.componentRef.setInput('project', 'crate');
    fixture.componentRef.setInput('projectName', 'Crate');
    fixture.componentRef.setInput('start', 'unreal');
    const page = fixture.nativeElement as HTMLElement;
    await settled(fixture);

    expect(page.querySelector('[data-testid="preset-built-in"]')).not.toBeNull();
    expect(page.querySelector<HTMLInputElement>('[data-testid="preset-name"]')?.disabled).toBe(true);
    page.querySelector<HTMLButtonElement>('[data-testid="preset-duplicate"]')?.click();
    await settled(fixture, 300);

    expect(page.querySelector<HTMLInputElement>('[data-testid="preset-id"]')?.value).toBe('unreal-copy');
    expect(page.querySelector('[data-testid="preset-file-written"]')?.textContent.trim()).toBe('T_Crate_Copy_ORM.png');
    page.querySelector<HTMLButtonElement>('[data-testid="preset-save"]')?.click();
    await settled(fixture, 10);

    expect(puts).toHaveLength(1);
    expect(puts[0]?.body).toMatchObject({ project: 'crate', replaces: null, preset: { id: 'unreal-copy', name: 'Unreal Engine Copy' } });
  });

  it('swaps two lanes dropped on each other, and renames a project preset in place', async () => {
    const mine = { ...unreal, id: 'mine', name: 'Mine' };
    const puts: unknown[] = [];
    const store = setup({
      'GET export/presets': () => Response.json(list),
      'GET export/presets/mine': () => Response.json({ document: mine, origin: 'project', file: '/l/mine.yaml', text: '' }),
      'POST export/presets/check': () => Response.json({ problem: null, files: [], text: '' }),
      'PUT export/presets/ours': async request => {
        const body = (await request.json()) as { preset: typeof unreal };
        puts.push(body);
        return Response.json({ document: body.preset, origin: 'project', file: '/l/ours.yaml', text: '' });
      }
    });
    await store.load('crate');
    const fixture = TestBed.createComponent(PresetEditor);
    fixture.componentRef.setInput('project', 'crate');
    fixture.componentRef.setInput('start', 'mine');
    const page = fixture.nativeElement as HTMLElement;
    await settled(fixture);
    const lanes = page.querySelectorAll<HTMLElement>('[data-testid="preset-lane"]');

    lanes[0]?.dispatchEvent(new Event('dragstart'));
    lanes[2]?.dispatchEvent(new Event('drop'));
    const id = page.querySelector<HTMLInputElement>('[data-testid="preset-id"]');
    if (id) {
      id.value = 'ours';
      id.dispatchEvent(new Event('input'));
    }
    await settled(fixture);
    page.querySelector<HTMLButtonElement>('[data-testid="preset-save"]')?.click();
    await settled(fixture, 10);

    expect(puts[0]).toMatchObject({
      project: 'crate',
      replaces: 'mine',
      preset: { id: 'ours', textures: [{ channels: ['metalness.r', 'roughness.r', 'occlusion.r'] }] }
    });
  });

  it('says what the Server finds wrong, and keeps nothing while it is wrong', async () => {
    const store = setup({
      'GET export/presets': () => Response.json(list),
      'GET export/presets/mine': () =>
        Response.json({ document: { ...unreal, id: 'mine' }, origin: 'library', file: '/l/mine.yaml', text: '' }),
      'POST export/presets/check': () => Response.json({ problem: "texture 1: 'bmp' is not a format.", files: [], text: '' })
    });
    await store.load(null);
    const fixture = TestBed.createComponent(PresetEditor);
    fixture.componentRef.setInput('start', 'mine');
    const page = fixture.nativeElement as HTMLElement;
    await settled(fixture, 300);

    expect(page.querySelector('[data-testid="preset-problem"]')?.textContent).toContain('bmp');
    expect(page.querySelector<HTMLButtonElement>('[data-testid="preset-save"]')?.disabled).toBe(true);
  });
});

describe('CompressionView', () => {
  it('shows the three pictures, the error per lane and the memory, and offers a build when nothing is built', async () => {
    let built = false;
    const store = setup({
      'GET export/presets': () => Response.json(list),
      'POST export/compression': () =>
        built
          ? Response.json({
              id: 'a'.repeat(32),
              format: 'ktx2',
              compression: 'bc7',
              level: 0,
              levels: 12,
              width: 2048,
              height: 2048,
              region: [896, 896, 256, 256],
              lanes: [
                { lane: 'r', psnr: 41.25, maxError: 0.03, meanError: 0.004 },
                { lane: 'g', psnr: null, maxError: 0, meanError: 0 }
              ],
              gain: 16,
              bytes: 5592406,
              uncompressed: 22369622
            })
          : Response.json(
              { detail: 'not built', code: 'export-unbuilt', values: { project: 'crate' } },
              { status: 409, headers: { 'content-type': 'application/problem+json' } }
            ),
      [`GET export/compression/${'a'.repeat(32)}/source`]: () => new Response(new Blob(['png'])),
      [`GET export/compression/${'a'.repeat(32)}/written`]: () => new Response(new Blob(['png'])),
      [`GET export/compression/${'a'.repeat(32)}/difference`]: () => new Response(new Blob(['png'])),
      'GET jobs': () => Response.json({ jobs: [], sequence: 0 })
    });
    await store.load(null);
    const fixture = TestBed.createComponent(CompressionView);
    fixture.componentRef.setInput('project', 'crate');
    fixture.componentRef.setInput('projectId', 'p1');
    fixture.componentRef.setInput('preset', 'vixen');
    fixture.componentRef.setInput('file', '{project}_orm');
    const page = fixture.nativeElement as HTMLElement;
    await settled(fixture, 10);

    expect(page.querySelector('[data-testid="compression-unbuilt"]')).not.toBeNull();
    built = true;
    fixture.componentRef.setInput('file', '{project}_baseColor');
    await settled(fixture, 10);

    expect(page.querySelectorAll('[data-testid="compression-pictures"] img')).toHaveLength(3);
    expect([...page.querySelectorAll('[data-testid="compression-psnr"]')].map(cell => cell.textContent.trim())).toEqual([
      '41.3 dB',
      'Lossless'
    ]);
    expect(page.textContent).toContain('Difference × 16');
    expect(page.querySelector('[data-testid="compression-memory"]')?.textContent).toContain('uncompressed');
  });
});
