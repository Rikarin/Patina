import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type {
  ProjectBakeSettings,
  ProjectContents,
  ProjectDocumentInfo,
  ProjectMeshInfo,
  ViewportBakePreview
} from '@patina/api-client';
import { SystemStatus } from '../core/system-status';
import { ProjectStore } from '../library/project-store';
import { BakeSettings } from './bake-settings';
import { type BakePlanQuery, BakeStore } from './bake-store';

const defaults: ProjectBakeSettings = {
  options: {
    resolution: 2048,
    format: 'png16',
    supersampling: 1,
    gutter: 4,
    diffusion: false,
    maps: ['normal', 'height', 'ao'],
    space: 'tangent',
    rays: {
      front: 0.05,
      rear: 0.05,
      units: 'relative',
      ignoreBackfaces: false,
      automaticCage: false,
      skew: 0,
      skewMap: '',
      distanceMap: '',
      matchByName: false,
      lowSuffix: '_low',
      highSuffix: '_high'
    },
    height: { range: 'measured', scale: 0.05 },
    occlusion: {
      rays: 64,
      distance: 0.5,
      spread: 90,
      falloff: 'none',
      distribution: 'cosine',
      minimum: 0,
      ignoreBackfaces: false,
      withinPart: false,
      groundPlane: false,
      groundOffset: 0
    },
    thickness: { normalised: true },
    curvature: { source: 'mesh', range: 'auto', radius: 0.01, sampling: 0.01, rays: 16 },
    edges: { edges: 'hard', width: 0.01, angle: 30, profile: 'even', mesh: 'high' },
    borders: { width: 0.02 },
    position: { range: 'box' },
    id: { source: 'auto', palette: 'random' },
    rounding: { enabled: false, width: 0.01, edges: 'angle', angle: 30, profile: 'even' },
    colour: { source: 'auto', texture: '' }
  },
  sets: [],
  high: [],
  cage: ''
};

const mesh: ProjectMeshInfo = { id: 'm1', path: 'projects/a/crate.glb', high: 'projects/a/crate_high.glb' };

function document(name: string, kind: string, material: string | null, onMesh: boolean): ProjectDocumentInfo {
  return {
    id: name,
    kind,
    name,
    path: `projects/a/${name}`,
    mesh: onMesh ? 'm1' : null,
    material,
    udim: 1001,
    tags: []
  };
}

function contents(documents: ProjectDocumentInfo[], bake = defaults): ProjectContents {
  return {
    id: 'p1',
    name: 'A',
    path: 'a',
    meshes: [mesh],
    documents,
    bakeFolder: 'projects/a/bakes',
    bakes: [],
    pictures: [],
    autoUnwrap: true,
    preservePaint: true,
    normalFormat: 'y-up',
    unwrap: { resolution: 2048, margin: 4, quality: 'outlines', rotations: 4, texelDensity: 0, overflow: 'shrink' },
    exportSets: [],
    tags: [],
    bake
  };
}

function setup(documents: ProjectDocumentInfo[], front: ProjectDocumentInfo | null | undefined) {
  const current = signal<ProjectContents | null>(contents(documents));
  const written: { project: string; bake: ProjectBakeSettings }[] = [];
  const started: unknown[][] = [];
  const planned: { query: BakePlanQuery; written: number }[] = [];
  const planAnswer = [{ material: 'Strap' }];
  const shown: ViewportBakePreview[] = [];
  const answers: { gate: Promise<void>; refuse?: Error; refusePlan?: Error } = { gate: Promise.resolve() };
  const project = {
    current,
    document: signal(front ?? null),
    mesh: signal(front?.mesh ? mesh : null),
    unwrapping: signal(false),
    textureSets: () => (current()?.documents ?? []).filter(each => each.material),
    setBake: (path: string, bake: ProjectBakeSettings) => {
      written.push({ project: path, bake });
      current.set(contents(documents, bake));
      return Promise.resolve();
    }
  };
  TestBed.configureTestingModule({
    providers: [
      { provide: ProjectStore, useValue: project },
      {
        provide: SystemStatus,
        useValue: { state: signal({ kind: 'online', system: { library: '/lib' } }) }
      },
      {
        provide: BakeStore,
        useValue: {
          busy: signal(false),
          plan: (query: BakePlanQuery) => {
            planned.push({ query, written: written.length });
            return answers.refusePlan ? Promise.reject(answers.refusePlan) : Promise.resolve(planAnswer);
          },
          startAll: (requests: unknown[]) => {
            started.push(requests);
            return answers.refuse ? Promise.reject(answers.refuse) : Promise.resolve([]);
          },
          showSetup: async (preview: ViewportBakePreview) => {
            shown.push(preview);
            await answers.gate;
            return { preview: preview.project || preview.low ? preview : null, parts: [], warnings: [] };
          }
        }
      }
    ]
  });
  return { settings: TestBed.inject(BakeSettings), written, started, planned, planAnswer, project, shown, answers };
}

describe('BakeSettings', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows the setup of the project’s bake, follows an edit once the file has it, and takes it off (#296)', async () => {
    vi.useFakeTimers();
    const texturing = document('Crate', 'texturing', null, true);
    const { settings, shown, written } = setup([texturing], texturing);

    await settings.setPreviewing(true);
    expect(settings.previewing()).toBe(true);
    // The Server reads the meshes from the project, as it plans the bake, so the client sends no paths.
    expect(shown[0]).toMatchObject({ project: 'a', document: 'projects/a/Crate', low: '', high: [] });

    settings.set(options => ({ ...options, rays: { ...options.rays, front: 0.2 } }));
    await vi.advanceTimersByTimeAsync(1_000);
    expect(shown.at(-1)?.options.rays.front).toBe(0.2);
    expect(written.at(-1)?.bake.options.rays.front).toBe(0.2);

    await settings.setPreviewing(false);
    expect(shown.at(-1)).toMatchObject({ project: null, low: '' });
    expect(settings.previewing()).toBe(false);
  });

  it('writes an edit before the preview asks the Server to read the project (#296)', async () => {
    const texturing = document('Crate', 'texturing', null, true);
    const { settings, shown, written, project } = setup([texturing], texturing);
    const order: string[] = [];
    const setBake = project.setBake;
    project.setBake = (path: string, bake: ProjectBakeSettings) => {
      order.push('write');
      return setBake(path, bake);
    };
    const showSetup = shown.push.bind(shown);
    shown.push = (...previews: ViewportBakePreview[]) => {
      order.push('shown');
      return showSetup(...previews);
    };

    settings.set(options => ({ ...options, rays: { ...options.rays, rear: 0.4 } }));
    await settings.setPreviewing(true);

    expect(order).toEqual(['write', 'shown']);
    expect(written.at(-1)?.bake.options.rays.rear).toBe(0.4);
  });

  it('shows an edit made while the Server was still reading the meshes for the first time', async () => {
    const texturing = document('Crate', 'texturing', null, true);
    const { settings, shown, answers } = setup([texturing], texturing);
    let answer: () => void = () => undefined;
    answers.gate = new Promise<void>(resolve => {
      answer = resolve;
    });

    const showing = settings.setPreviewing(true);
    await vi.waitFor(() => expect(shown).toHaveLength(1));
    settings.set(options => ({ ...options, rays: { ...options.rays, front: 0.3 } }));
    answer();
    await showing;

    expect(shown.map(each => each.options.rays.front)).toEqual([0.05, 0.3]);
    expect(settings.previewing()).toBe(true);
  });

  it('keeps an edit in the project a moment later, once, with every setting', async () => {
    vi.useFakeTimers();
    const texturing = document('Crate', 'texturing', null, true);
    const { settings, written } = setup([texturing], texturing);

    settings.set(options => ({ ...options, rays: { ...options.rays, front: 0.1 } }));
    settings.set(options => ({ ...options, occlusion: { ...options.occlusion, spread: 45 } }));

    expect(written).toEqual([]);
    expect(settings.options()?.rays.front).toBe(0.1);

    await vi.advanceTimersByTimeAsync(1_000);

    expect(written).toHaveLength(1);
    expect(written[0]?.project).toBe('a');
    expect(written[0]?.bake.options.rays.front).toBe(0.1);
    expect(written[0]?.bake.options.occlusion.spread).toBe(45);
    expect(written[0]?.bake.options.rays.rear).toBe(0.05);
  });

  it('writes an edit made while the last was being written after it, so the older never lands last', async () => {
    vi.useFakeTimers();
    const texturing = document('Crate', 'texturing', null, true);
    const { settings, project } = setup([texturing], texturing);
    const sent: number[] = [];
    const answers: (() => void)[] = [];
    project.setBake = (_path: string, bake: ProjectBakeSettings) => {
      sent.push(bake.options.occlusion.rays);
      return new Promise<void>(resolve => answers.push(resolve));
    };

    settings.set(options => ({ ...options, occlusion: { ...options.occlusion, rays: 32 } }));
    await vi.advanceTimersByTimeAsync(1_000);
    settings.set(options => ({ ...options, occlusion: { ...options.occlusion, rays: 128 } }));
    await vi.advanceTimersByTimeAsync(1_000);

    // The first is still on its way, so the second waits for it rather than racing it to the Server.
    expect(sent).toEqual([32]);
    answers[0]?.();
    await vi.advanceTimersByTimeAsync(0);
    expect(sent).toEqual([32, 128]);
    answers[1]?.();
    await vi.advanceTimersByTimeAsync(0);
    expect(settings.options()?.occlusion.rays).toBe(128);
  });

  it('bakes what the Server plans from the project’s file, once an edit is written to it (#292)', async () => {
    const sets = [document('Body', 'texturing', 'Body', true), document('Strap', 'texturing', 'Strap', true)];
    const { settings, started, planned, planAnswer, written } = setup(sets, sets[0]);

    settings.update(bake => ({ ...bake, high: ['/elsewhere/extra.glb'], sets: ['Strap'] }));
    await settings.bake();

    // The edit reached the file before the plan was asked for, so the plan bakes it.
    expect(written.at(-1)?.bake.sets).toEqual(['Strap']);
    expect(planned).toEqual([{ query: { project: 'a', document: 'projects/a/Body' }, written: 1 }]);
    expect(started).toEqual([planAnswer]);
  });

  it('bakes nothing when the project’s file did not take the settings, and says why', async () => {
    const texturing = document('Crate', 'texturing', null, true);
    const { settings, started, planned, project } = setup([texturing], texturing);
    project.setBake = () => Promise.reject(new Error('The project file is read-only.'));

    settings.set(options => ({ ...options, resolution: 512 }));

    expect(await settings.bake()).toBe(false);
    expect(settings.refused()).toContain('read-only');
    expect(planned).toEqual([]);
    expect(started).toEqual([]);
  });

  it('writes settings the file did not take again when Bake is pressed, with no edit between (#296)', async () => {
    const texturing = document('Crate', 'texturing', null, true);
    const { settings, started, written, project } = setup([texturing], texturing);
    const setBake = project.setBake;
    project.setBake = () => Promise.reject(new Error('The project file is in use.'));

    settings.set(options => ({ ...options, resolution: 512 }));
    expect(await settings.bake()).toBe(false);
    expect(settings.failure()).toContain('in use');

    project.setBake = setBake;
    expect(await settings.bake()).toBe(true);
    expect(settings.failure()).toBeNull();
    expect(written.at(-1)?.bake.options.resolution).toBe(512);
    expect(started).toHaveLength(1);
  });

  it('Quick Bake starts every chosen set, and a refusal says why and starts nothing (#147)', async () => {
    const sets = [document('Body', 'texturing', 'Body', true), document('Strap', 'texturing', 'Strap', true)];
    const { settings, started, answers } = setup(sets, sets[0]);

    expect(await settings.bake()).toBe(true);
    expect(started).toHaveLength(1);

    answers.refuse = new Error('The low mesh has no UV layout.');
    expect(await settings.bake()).toBe(false);
    expect(settings.refused()).toContain('no UV layout');

    answers.refusePlan = new Error("The mesh has no texture set 'Lid'.");
    expect(await settings.bake()).toBe(false);
    expect(settings.refused()).toContain('no texture set');
    expect(started).toHaveLength(2);
  });

  it('marks each set whose bake no longer fits it (#238)', () => {
    const sets = [
      { ...document('Body', 'texturing', 'Body', true), bakeStale: 'uvSet' },
      document('Strap', 'texturing', 'Strap', true)
    ];
    const { settings } = setup(sets, sets[0]);

    expect(settings.sets().map(set => set.stale)).toEqual(['uvSet', null]);
  });

  it('bakes every set when none is ticked, and says why it cannot when the maps are all off', () => {
    const sets = [document('Body', 'texturing', 'Body', true), document('Strap', 'texturing', 'Strap', true)];
    const { settings } = setup(sets, sets[0]);

    expect(settings.chosen().map(set => set.material)).toEqual(['Body', 'Strap']);
    expect(settings.blocked()).toBeNull();

    settings.set(options => ({ ...options, maps: [] }));

    expect(settings.blocked()).toBe('Turn on at least one map.');
  });

  it('keeps back a minimum occluder distance at the occluder distance, which would bake white, and says why (#228)', async () => {
    vi.useFakeTimers();
    const texturing = document('Crate', 'texturing', null, true);
    const { settings, written } = setup([texturing], texturing);
    const words = 'The minimum occluder distance has to be less than the occluder distance';

    settings.set(options => ({ ...options, occlusion: { ...options.occlusion, minimum: 0.5 } }));
    await vi.advanceTimersByTimeAsync(1_000);

    expect(settings.occlusionProblem()).toContain(words);
    expect(settings.blocked()).toContain(words);
    expect(written).toEqual([]); // the Server would refuse it

    // Nothing that reads the minimum is baked, so it may stay.
    settings.set(options => ({ ...options, maps: ['normal', 'thickness'] }));
    expect(settings.blocked()).toBeNull();

    settings.set(options => ({ ...options, maps: ['bent'], occlusion: { ...options.occlusion, minimum: 0.25 } }));
    await vi.advanceTimersByTimeAsync(1_000);
    expect(settings.blocked()).toBeNull();
    expect(written.at(-1)?.bake.options.occlusion.minimum).toBe(0.25);
  });

  it('bakes the project’s mesh from a layered material, which has none of its own', () => {
    const texturing = document('Crate', 'texturing', null, true);
    const layered = document('Rust', 'layered-material', null, false);
    const { settings } = setup([texturing, layered], layered);

    expect(settings.blocked()).toBeNull();
    expect(settings.low()).toBe('/lib/projects/a/crate.glb');
    // The Server plans onto the project's mesh when the document in front is on none.
    expect(settings.planQuery()).toEqual({ project: 'a' });
  });

  it('keeps a chosen output folder while the project’s contents change, and drops it for another project', () => {
    const texturing = document('Crate', 'texturing', null, true);
    const { settings, project } = setup([texturing], texturing);

    settings.chooseOutput('/chosen');
    // Choosing a high poly mesh rereads the project, which is a new object with the same path.
    project.current.set(contents([texturing]));

    expect(settings.planQuery()).toEqual({ project: 'a', document: 'projects/a/Crate', output: '/chosen' });

    project.current.set({ ...contents([texturing]), path: 'b' });

    expect(settings.output()).toBe('');
  });

  it('says plainly that a project with no mesh has nothing to bake', () => {
    const layered = document('Rust', 'layered-material', null, false);
    const { settings, project } = setup([layered], layered);
    project.current.set({ ...contents([layered]), meshes: [] });

    expect(settings.blocked()).toBe('This project has no mesh yet. Choose one in Project Settings.');
  });
});
