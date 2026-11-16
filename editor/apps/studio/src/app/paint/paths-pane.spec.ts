import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { StackLayer, StackSurfacePath } from '@patina/api-client';
import { PaintStore } from './paint-store';
import { PathsPane, pathLength, pathRows } from './paths-pane';

function path(points: number[][], more: Partial<StackSurfacePath> = {}): StackSurfacePath {
  return {
    points: points.map(position => ({ position, normal: [0, 0, 1], pressure: 1 })),
    radius: 0.1,
    hardness: 0.5,
    ribbon: false,
    repeat: 0,
    angle: 1.3,
    spacing: 0,
    jitter: 0,
    filled: false,
    ...more
  };
}

function layer(id: string, more: Partial<StackLayer> = {}): StackLayer {
  return { id, name: id, kind: 'fill', enabled: true, projection: 'Uv', ...more } as StackLayer;
}

const square = [
  [0, 0, 0],
  [1, 0, 0],
  [1, 1, 0],
  [0, 1, 0]
];

describe('paths (#130)', () => {
  it('lists the fills laid along a path, with what each does, its points and how long it is', () => {
    const rows = pathRows([
      { layer: layer('Trim', { projection: 'Path', path: path(square) }) },
      { layer: layer('Plain') },
      { layer: layer('Patch', { projection: 'Path', path: path(square, { filled: true }) }) },
      { layer: layer('Band', { projection: 'Path', path: path(square.slice(0, 2), { ribbon: true }) }) },
      { layer: layer('Brush', { kind: 'paint', projection: 'Path' }) },
      { layer: layer('Empty', { projection: 'Path' }) }
    ]);

    expect(rows.map(row => [row.layer.id, row.kind, row.points, row.length])).toEqual([
      ['Trim', 'Stroke', 4, 3],
      ['Patch', 'Loop', 4, 4],
      ['Band', 'Ribbon', 2, 1],
      ['Empty', 'Stroke', 0, 0]
    ]);
  });

  it('closes a loop of fewer than three points no more than a stroke does', () => {
    expect(pathLength(path(square.slice(0, 2), { filled: true }))).toBe(1);
  });

  describe('the pane', () => {
    function setup(model = 'crate.fbx') {
      const layers = signal<{ layer: StackLayer }[]>([
        { layer: layer('a', { projection: 'Path', path: path(square) }) }
      ]);
      const edits: unknown[] = [];
      const paint = {
        layers,
        layer: signal<string | null>(null),
        marked: signal(new Set<string>()),
        masking: signal(true),
        placing: signal<'decal' | 'path' | null>(null),
        failure: signal<string | null>(null),
        state: signal({ model }),
        addLayer: vi.fn(() => {
          layers.update(rows => [{ layer: layer('b', { name: 'Path' }) }, ...rows]);
          paint.layer.set('b');
          return Promise.resolve();
        }),
        edit: vi.fn((...commands: unknown[]) => {
          edits.push(...commands);
          return Promise.resolve();
        })
      };
      TestBed.configureTestingModule({ providers: [{ provide: PaintStore, useValue: paint }] });
      const fixture = TestBed.createComponent(PathsPane);
      fixture.detectChanges();
      return { fixture, paint, edits, element: fixture.nativeElement as HTMLElement };
    }

    it('chooses a path from its row, out of the mask', () => {
      const { fixture, paint, element } = setup();

      element.querySelector<HTMLButtonElement>('[data-testid="path-a"]')?.click();
      fixture.detectChanges();

      expect(paint.layer()).toBe('a');
      expect(paint.masking()).toBe(false);
      expect(element.querySelector('[data-testid="path-a"]')?.getAttribute('aria-selected')).toBe('true');
      expect(element.querySelector('[data-testid="paths-add-points"]')).not.toBeNull();
    });

    it('starts a new path as a fill along a path, taking points from the next clicks', async () => {
      const { paint, edits, element } = setup();

      element.querySelector<HTMLButtonElement>('[data-testid="paths-new"]')?.click();
      await vi.waitFor(() => expect(paint.placing()).toBe('path'));

      expect(paint.addLayer).toHaveBeenCalledWith('fill', 'Path');
      expect(edits).toEqual([{ op: 'setLayer', layer: 'b', projection: 'Path' }]);
      expect(paint.layer()).toBe('b');
    });

    it('offers no new path on a document with no model to click on', () => {
      const { element } = setup(' ');

      expect(element.querySelector<HTMLButtonElement>('[data-testid="paths-new"]')?.disabled).toBe(true);
    });
  });
});
