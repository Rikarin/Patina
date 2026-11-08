import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { ProjectBakeSettings, ProjectContents, ProjectDocumentInfo, ProjectMeshInfo } from '@patina/api-client';
import { SystemStatus } from '../core/system-status';
import { ProjectStore } from '../library/project-store';
import { BakeSettings } from './bake-settings';
import { BakeStore } from './bake-store';

const defaults: ProjectBakeSettings = {
  options: {
    resolution: 2048,
    format: 'png16',
    supersampling: 1,
    gutter: 4,
    maps: ['normal', 'height', 'ao'],
    space: 'tangent',
    rays: {
      front: 0.05,
      rear: 0.05,
      units: 'relative',
      ignoreBackfaces: false,
      skew: 0,
      skewMap: '',
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
    id: { source: 'auto', palette: 'random' }
  },
  sets: [],
  high: [],
  cage: ''
};

const mesh: ProjectMeshInfo = { id: 'm1', path: 'projects/a/crate.glb', high: 'projects/a/crate_high.glb' };

function document(name: string, kind: string, material: string | null, onMesh: boolean): ProjectDocumentInfo {
  return { id: name, kind, name, path: `projects/a/${name}`, mesh: onMesh ? 'm1' : null, material };
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
    unwrap: { resolution: 2048, margin: 4, quality: 'outlines', rotations: 4, texelDensity: 0, overflow: 'shrink' },
    exportSets: [],
    bake
  };
}

function setup(documents: ProjectDocumentInfo[], front: ProjectDocumentInfo | null | undefined) {
  const current = signal<ProjectContents | null>(contents(documents));
  const written: { project: string; bake: ProjectBakeSettings }[] = [];
  const started: unknown[][] = [];
  const project = {
    current,
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
          startAll: (requests: unknown[]) => {
            started.push(requests);
            return Promise.resolve([]);
          }
        }
      }
    ]
  });
  return { settings: TestBed.inject(BakeSettings), written, started, project };
}

describe('BakeSettings', () => {
  afterEach(() => {
    vi.useRealTimers();
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

  it('bakes each ticked texture set into a folder of its own, from the high meshes in order', async () => {
    const sets = [document('Body', 'texturing', 'Body', true), document('Strap', 'texturing', 'Strap', true)];
    const { settings, started } = setup(sets, sets[0]);

    settings.update(bake => ({ ...bake, high: ['/elsewhere/extra.glb'], sets: ['Strap'] }));
    await settings.bake();

    expect(started).toHaveLength(1);
    const requests = started[0] as { material: string; output: string; high: string[] }[];
    expect(requests.map(request => request.material)).toEqual(['Strap']);
    expect(requests[0]?.output).toBe('/lib/projects/a/bakes/maps/Strap');
    expect(requests[0]?.high).toEqual(['/lib/projects/a/crate_high.glb', '/elsewhere/extra.glb']);
  });

  it('bakes every set when none is ticked, and says why it cannot when the maps are all off', () => {
    const sets = [document('Body', 'texturing', 'Body', true), document('Strap', 'texturing', 'Strap', true)];
    const { settings } = setup(sets, sets[0]);

    expect(settings.requests().map(request => request.material)).toEqual(['Body', 'Strap']);
    expect(settings.blocked()).toBeNull();

    settings.set(options => ({ ...options, maps: [] }));

    expect(settings.blocked()).toBe('Turn on at least one map.');
  });

  it('bakes the project’s mesh from a layered material, which has none of its own', () => {
    const texturing = document('Crate', 'texturing', null, true);
    const layered = document('Rust', 'layered-material', null, false);
    const { settings } = setup([texturing, layered], layered);

    expect(settings.blocked()).toBeNull();
    expect(settings.low()).toBe('/lib/projects/a/crate.glb');
    expect(settings.requests()).toHaveLength(1);
  });

  it('keeps a chosen output folder while the project’s contents change, and drops it for another project', () => {
    const texturing = document('Crate', 'texturing', null, true);
    const { settings, project } = setup([texturing], texturing);

    settings.chooseOutput('/chosen');
    // Choosing a high poly mesh rereads the project, which is a new object with the same path.
    project.current.set(contents([texturing]));

    expect(settings.requests()[0]?.output).toBe('/chosen');

    project.current.set({ ...contents([texturing]), path: 'b' });

    expect(settings.output()).toBe('');
  });

  it('says plainly that a project with no mesh has nothing to bake', () => {
    const layered = document('Rust', 'layered-material', null, false);
    const { settings, project } = setup([layered], layered);
    project.current.set({ ...contents([layered]), meshes: [] });

    expect(settings.blocked()).toBe('This project has no mesh yet. Choose one in Project Settings.');
    expect(settings.requests()).toEqual([]);
  });
});
