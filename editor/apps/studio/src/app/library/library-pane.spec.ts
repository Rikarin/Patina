import { TestBed } from '@angular/core/testing';
import { type LibraryInfo, createPatinaClient } from '@patina/api-client';
import { API_CLIENT_FACTORY, SERVER_CONNECTION } from '../core/system-status';
import { LibraryInspector } from './library-inspector';
import { LibraryPane } from './library-pane';
import { LibrarySelection } from './library-selection';

/** jsdom lays nothing out, so rows never render here: Playwright covers them, and this covers what surrounds them. */
vi.stubGlobal(
  'ResizeObserver',
  class {
    observe(): void {
      // Nothing resizes here.
    }

    disconnect(): void {
      // Nothing to stop.
    }
  }
);

const info: LibraryInfo = {
  id: '00000000-0000-0000-0000-000000000001',
  name: 'Ember',
  path: '/games/Ember.ptlib',
  schemaVersion: 1,
  texelDensity: 1024,
  channels: [],
  defaultShader: 'pbr',
  exportTargets: [],
  projectCount: 0,
  assetCount: 0,
  indexing: false
};

const nothing = { items: [], total: 0, offset: 0, limit: 100 };

function provide(library: () => Response, projects: () => Response = () => Response.json(nothing)): void {
  const answer = (input: RequestInfo | URL): Promise<Response> => {
    const path = new URL((input as Request).url).pathname;
    if (path.endsWith('/library')) {
      return Promise.resolve(library());
    }

    if (path.endsWith('/system')) {
      return Promise.resolve(Response.json({ textureSides: [1024, 2048], defaultTextureSide: 2048 }));
    }

    return Promise.resolve(path.endsWith('/library/projects') ? projects() : Response.json(nothing));
  };
  TestBed.configureTestingModule({
    providers: [
      { provide: SERVER_CONNECTION, useValue: () => Promise.resolve({ baseUrl: 'http://server', token: null }) },
      {
        provide: API_CLIENT_FACTORY,
        useValue: (connection: { baseUrl: string; token: string | null }) => createPatinaClient(connection, answer)
      }
    ]
  });
}

async function pane(text: string): Promise<HTMLElement> {
  const fixture = TestBed.createComponent(LibraryPane);
  const element = fixture.nativeElement as HTMLElement;
  await vi.waitFor(async () => {
    await fixture.whenStable();
    expect(element.textContent).toContain(text);
  });
  return element;
}

describe('LibraryPane', () => {
  it('says so when the Server has no library', async () => {
    provide(() => Response.json({ detail: 'none' }, { status: 404 }));

    const element = await pane('No library open');

    expect(element.querySelector('input')).toBeNull();
  });

  it('offers a retry when the library cannot be reached', async () => {
    provide(() => Response.json({ detail: 'on fire' }, { status: 500 }));

    const element = await pane('on fire');

    expect(element.querySelector('button')?.textContent).toContain('Try again');
  });

  it('invites a first project into an empty library', async () => {
    provide(() => Response.json(info));

    const element = await pane('No projects yet');

    const count = element.querySelector('[data-testid=library-count]');
    expect(count?.textContent).toContain('0 projects');
    // Narrow, only the number shows; the words are the part a container query hides, and the tooltip says it all (#234).
    expect(count?.firstChild?.textContent).toBe('0');
    expect(count?.querySelector('span')?.className).toContain('hidden');
    expect(count?.getAttribute('title')).toBe('0 projects');
  });

  it('says a scan is running rather than that the library is empty', async () => {
    provide(() => Response.json({ ...info, indexing: true }));

    const element = await pane('Indexing the library');

    expect(element.querySelector('[data-testid=library-indexing]')).not.toBeNull();
  });

  it('says why a list did not load', async () => {
    provide(
      () => Response.json(info),
      () => Response.json({ detail: 'index is locked' }, { status: 500 })
    );

    const element = await pane('The list did not load');

    expect(element.textContent).toContain('index is locked');
  });
});

describe('LibraryInspector', () => {
  const inspect = async (): Promise<{ element: HTMLElement; settle: () => Promise<void> }> => {
    const fixture = TestBed.createComponent(LibraryInspector);
    await fixture.whenStable();
    return { element: fixture.nativeElement as HTMLElement, settle: () => fixture.whenStable() };
  };

  it('starts empty', async () => {
    provide(() => Response.json(info));

    const { element } = await inspect();

    expect(element.textContent).toContain('Nothing chosen');
  });

  it('shows a project', async () => {
    provide(() => Response.json(info));
    const { element, settle } = await inspect();

    TestBed.inject(LibrarySelection).select({
      kind: 'project',
      project: { id: 'p1', name: 'Crate', path: 'props/crate', tags: ['hero'], referenceCount: 3 }
    });
    await settle();

    expect(element.querySelector('[data-testid=inspector-title]')?.textContent).toContain('Crate');
    expect(element.querySelector('[data-testid=inspector-references]')?.textContent).toContain('3');
    expect(element.textContent).toContain('props/crate');
    expect(element.textContent).toContain('hero');
  });

  it('shows what an asset would touch, and says when the counts are a lower bound', async () => {
    const asset = { id: 'a1', kind: 'graph', name: 'Rust', path: 'graphs/rust', tags: [], usageCount: 2 };
    TestBed.configureTestingModule({
      providers: [
        { provide: SERVER_CONNECTION, useValue: () => Promise.resolve({ baseUrl: 'http://server', token: null }) },
        {
          provide: API_CLIENT_FACTORY,
          useValue: (connection: { baseUrl: string; token: string | null }) =>
            createPatinaClient(connection, () =>
              Promise.resolve(
                Response.json({
                  ...info,
                  asset,
                  projects: 1412,
                  pinned: 3,
                  unreadableDocuments: 2,
                  items: [],
                  offset: 0,
                  limit: 100
                })
              )
            )
        }
      ]
    });
    const { element, settle } = await inspect();

    TestBed.inject(LibrarySelection).select({ kind: 'asset', asset });
    await vi.waitFor(async () => {
      await settle();
      expect(element.querySelector('[data-testid=impact-summary]')?.textContent).toContain('Affects 1,412 projects');
    });

    expect(element.querySelector('[data-testid=impact-summary]')?.textContent).toContain('3 pinned');
    expect(element.textContent).toContain('2 documents could not be read');
  });
});
