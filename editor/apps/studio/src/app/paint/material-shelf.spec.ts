import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { type MaterialInfo, createPatinaClient } from '@patina/api-client';
import { Preferences } from '../core/preferences';
import { LibraryStore } from '../library/library-store';
import { ProjectStore } from '../library/project-store';
import { LibraryThumbnails } from '../library/thumbnails';
import { MaterialShelf } from './material-shelf';
import { MaterialUse } from './material-use';

const rust: MaterialInfo = {
  name: 'Rust',
  kind: 'graph',
  source: 'projects/props/wear/rust.vxtexgraph',
  role: 'mask',
  origin: 'library',
  channels: ['baseColor'],
  project: 'Props'
};

const plastic: MaterialInfo = {
  name: 'Plastic',
  kind: 'layered-material',
  source: 'smart:Plastic',
  role: 'material',
  origin: 'shipped',
  channels: []
};

const roles = [
  { value: 'material', label: 'Materials' },
  { value: 'mask', label: 'Masks' }
];
const origins = [
  { value: 'shipped', label: 'Starter' },
  { value: 'library', label: 'Other projects' }
];

function setup() {
  const searches: string[] = [];
  const using = { failure: signal(null), carried: signal(null), use: vi.fn(), paintWith: vi.fn() };
  const wanted: string[] = [];
  const pictures = {
    want: (sources: readonly string[]) => wanted.push(...sources),
    url: (source: string) => (source === plastic.source ? 'blob:plastic' : null)
  };
  const fetcher = ((input: Request) => {
    const url = new URL(input.url);
    if (url.pathname.endsWith('/library/materials')) {
      const search = url.searchParams.get('search') ?? '';
      searches.push(search);
      // The Server decides what a search finds; here a search for another project's words finds Rust alone.
      return Promise.resolve(Response.json({ items: search ? [rust] : [plastic, rust], roles, origins }));
    }

    return Promise.resolve(Response.json({ items: [] }));
  }) as unknown as typeof fetch;
  const client = createPatinaClient({ baseUrl: 'http://server', token: 't' }, fetcher);
  TestBed.configureTestingModule({
    imports: [MaterialShelf],
    providers: [
      { provide: LibraryStore, useValue: { api: () => Promise.resolve(client), changes: signal(0) } },
      { provide: ProjectStore, useValue: { current: signal({ path: 'alpha' }), active: signal(null) } },
      { provide: MaterialUse, useValue: using },
      { provide: LibraryThumbnails, useValue: pictures }
    ]
  });
  const fixture = TestBed.createComponent(MaterialShelf);
  const page = fixture.nativeElement as HTMLElement;
  const names = () => [...page.querySelectorAll('[data-testid="shelf-material"]')].map(row => row.textContent.trim());
  return { fixture, page, names, searches, using, wanted };
}

afterEach(() => {
  globalThis.localStorage.clear(); // a star is this machine's, and each test starts with none
});

async function settled(fixture: ReturnType<typeof setup>['fixture']): Promise<void> {
  await new Promise(resolve => setTimeout(resolve));
  await fixture.whenStable();
}

describe('MaterialShelf', () => {
  it('shows what the Server found for a search, filtering none of it out again (#230)', async () => {
    const { fixture, page, names, searches } = setup();
    await vi.waitFor(async () => {
      await settled(fixture);
      expect(names()).toEqual(['PlasticStarter', 'RustProps']);
    });

    const search = page.querySelector<HTMLInputElement>('[data-testid="shelf-search"]');
    if (search) {
      search.value = 'scratches other';
      search.dispatchEvent(new Event('input'));
    }
    await vi.waitFor(async () => {
      await settled(fixture);
      expect(searches.at(-1)).toBe('scratches other');
      expect(names()).toEqual(['RustProps']);
    });
  });

  it('names types and origins as the Server does, and narrows to one', async () => {
    const { fixture, page, names } = setup();
    await vi.waitFor(async () => {
      await settled(fixture);
      expect(page.querySelector('[data-testid="shelf-material"]')?.getAttribute('title')).toContain(
        'Plastic · Materials, Starter'
      );
    });

    page.querySelector<HTMLElement>('[data-testid="shelf-origin"] button')?.click();
    await settled(fixture);
    const options = [...document.querySelectorAll('[role="option"]')].map(option => option.textContent.trim());
    expect(options).toEqual(['Everywhere', 'Favourites', 'Starter', 'Other projects']);
    document.querySelectorAll<HTMLElement>('[role="option"]')[3]?.click();
    await settled(fixture);

    expect(names()).toEqual(['RustProps']);
  });

  it('shows the pictures the Library draws, adds on a click and paints on an Alt-click (#125, #202)', async () => {
    const { fixture, page, names, using, wanted } = setup();
    await vi.waitFor(async () => {
      await settled(fixture);
      expect(names()).toEqual(['PlasticStarter', 'RustProps']);
    });

    // One service draws for the shelf and the Library alike, and the shelf only asks it.
    expect(wanted).toEqual([plastic.source, rust.source]);
    const [first, second] = page.querySelectorAll<HTMLElement>('[data-testid="shelf-material"]');
    expect(first?.querySelector('img')?.getAttribute('src')).toBe('blob:plastic');
    expect(second?.querySelector('img')).toBeNull();

    first?.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    expect(using.use).toHaveBeenCalledWith(plastic);
    expect(using.paintWith).not.toHaveBeenCalled();

    second?.dispatchEvent(new MouseEvent('click', { bubbles: true, altKey: true }));
    expect(using.paintWith).toHaveBeenCalledWith(rust);
    expect(using.use).toHaveBeenCalledTimes(1);
  });

  it('lists a starred material first, keeps the star on this machine and shows Favourites alone (#203)', async () => {
    const { fixture, page, names } = setup();
    await vi.waitFor(async () => {
      await settled(fixture);
      expect(names()).toEqual(['PlasticStarter', 'RustProps']);
    });

    const star = page.querySelector<HTMLElement>(`[aria-label="Star Rust"]`);
    star?.click();
    await settled(fixture);

    expect(names()).toEqual(['RustProps', 'PlasticStarter']);
    expect(page.querySelector('[aria-label="Unstar Rust"]')?.getAttribute('aria-pressed')).toBe('true');
    expect(TestBed.inject(Preferences).favouriteMaterials()).toEqual([rust.source]);

    page.querySelector<HTMLElement>('[data-testid="shelf-origin"] button')?.click();
    await settled(fixture);
    document.querySelectorAll<HTMLElement>('[role="option"]')[1]?.click();
    await settled(fixture);
    expect(names()).toEqual(['RustProps']);
  });
});
