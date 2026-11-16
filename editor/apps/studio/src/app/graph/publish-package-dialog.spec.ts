import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { PackageDependency, PackagePlan } from '@patina/api-client';
import { SystemStatus } from '../core/system-status';
import { GraphStore } from './graph-store';
import { PublishPackageDialog } from './publish-package-dialog';

interface Relinking {
  relinks(dependency: PackageDependency): boolean;
  relink(dependency: PackageDependency): Promise<void>;
}

const plan: PackagePlan = {
  name: 'Signs',
  role: 'material',
  path: 'packages/Signs.ptpkg',
  replaces: false,
  dependencies: [],
  diagnostics: []
};

function dialog(openImage: (formats?: readonly string[], kind?: string) => Promise<string | null>) {
  Object.assign(globalThis, { patina: { openImage } });
  const relinkPicture = vi.fn(() => Promise.resolve());
  TestBed.configureTestingModule({
    providers: [
      {
        provide: GraphStore,
        useValue: {
          document: signal(null),
          relinkPicture,
          packagePlan: () => Promise.resolve(plan),
          save: () => Promise.resolve(true)
        }
      },
      { provide: SystemStatus, useValue: { pictures: signal(['png']) } }
    ]
  });
  const component = TestBed.createComponent(PublishPackageDialog).componentInstance as unknown as Relinking;
  return { component, relinkPicture };
}

afterEach(() => {
  delete (globalThis as { patina?: unknown }).patina;
});

describe('PublishPackageDialog', () => {
  it('relinks a missing font or SVG file as it relinks a picture, asking for a file of its kind (#306)', async () => {
    const openImage = vi.fn((formats?: readonly string[]) =>
      Promise.resolve(formats?.includes('svg') ? 'D:\\Art\\logo.svg' : 'D:\\Art\\plate.ttf')
    );
    const { component, relinkPicture } = dialog(openImage);
    const font: PackageDependency = { kind: 'drawing', name: 'fonts/plate.ttf', problem: 'Not found.' };
    const svg: PackageDependency = { kind: 'drawing', name: 'logo.svg', problem: 'Not found.' };

    expect(component.relinks(font)).toBe(true);
    expect(component.relinks({ kind: 'compound', name: 'Wear' })).toBe(false);
    await component.relink(font);
    await component.relink(svg);
    await component.relink({ kind: 'picture', name: 'moss.png' });

    expect(openImage.mock.calls).toEqual([[['ttf', 'otf'], 'Fonts'], [['svg'], 'SVG files'], [['png']]]);
    expect(relinkPicture.mock.calls).toEqual([
      ['fonts/plate.ttf', 'D:\\Art\\plate.ttf'],
      ['logo.svg', 'D:\\Art\\logo.svg'],
      ['moss.png', 'D:\\Art\\plate.ttf']
    ]);
  });
});
