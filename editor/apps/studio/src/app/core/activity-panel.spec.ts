import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { BakeStore } from '../bake/bake-store';
import { LibraryStore } from '../library/library-store';
import { ProjectStore } from '../library/project-store';
import { Activity } from './activity';
import { ActivityPanel } from './activity-panel';

const summary = (id: string, name: string, dirty = false) => ({
  id,
  path: `projects/a/${name}`,
  name,
  revision: 1,
  dirty
});

async function render() {
  const deleted: string[] = [];
  const graphs = [summary('g1', 'Rust'), summary('g2', 'Moss', true)];
  const client = {
    GET: vi.fn((route: string) =>
      Promise.resolve({
        data: route === '/api/v1/graphs' ? graphs : [summary('s1', 'Crate')],
        response: new Response()
      })
    ),
    DELETE: vi.fn((_route: string, options: { params: { path: { id: string } } }) => {
      deleted.push(options.params.path.id);
      graphs.splice(
        graphs.findIndex(held => held.id === options.params.path.id),
        1
      );
      return Promise.resolve({ data: {}, response: new Response() });
    })
  };
  TestBed.configureTestingModule({
    providers: [
      { provide: BakeStore, useValue: { jobs: signal([]), cancel: vi.fn() } },
      { provide: LibraryStore, useValue: { api: () => Promise.resolve(client) } },
      // The stack is in a tab; the graphs are in none.
      { provide: ProjectStore, useValue: { tabs: signal([{ path: 'projects/a/Crate', session: 's1' }]) } }
    ]
  });
  const activity = TestBed.inject(Activity);
  activity.show();
  const fixture = TestBed.createComponent(ActivityPanel);
  await fixture.whenStable();
  await vi.waitFor(() => {
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('[data-testid=activity-left-open]')).toHaveLength(2);
  });
  return { element: fixture.nativeElement as HTMLElement, fixture, deleted, activity };
}

describe('ActivityPanel', () => {
  it('lists what the engine holds that no tab shows, and closes it there (#192, #232)', async () => {
    const { element, fixture, deleted, activity } = await render();
    const rows = [...element.querySelectorAll('[data-testid=activity-left-open]')];
    expect(rows.map(row => row.textContent)).toEqual([
      expect.stringContaining('Rust'),
      expect.stringContaining('Unsaved changes')
    ]);

    rows[0]?.querySelector<HTMLButtonElement>('[data-testid=activity-close-session]')?.click();
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(element.querySelectorAll('[data-testid=activity-left-open]')).toHaveLength(1);
    });
    expect(deleted).toEqual(['g1']);
    expect(activity.notices()[0]?.text).toBe('Rust closed in the engine.');
  });

  it('says there is nothing yet rather than showing empty lists', async () => {
    const { element } = await render();
    expect(element.textContent).toContain('Bakes and exports show here while they run, and after.');
    expect(element.textContent).toContain('Nothing yet.');
  });
});
