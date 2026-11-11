import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { App } from './app';
import { API_CLIENT_FACTORY, SERVER_CONNECTION, type ServerState, SystemStatus } from './core/system-status';

/** jsdom lays nothing out, so it has no ResizeObserver; the pane only needs one to exist. */
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

async function render(state: ServerState): Promise<HTMLElement> {
  TestBed.configureTestingModule({
    providers: [
      {
        provide: SystemStatus,
        useValue: {
          state: signal(state),
          textureSides: signal([256, 512, 1024, 2048]),
          defaultTextureSide: signal(2048)
        }
      },
      { provide: SERVER_CONNECTION, useValue: () => Promise.reject(new Error('no server in this test')) },
      { provide: API_CLIENT_FACTORY, useValue: () => null }
    ]
  });
  const fixture = TestBed.createComponent(App);
  await fixture.whenStable();
  return fixture.nativeElement as HTMLElement;
}

const system = {
  version: '1.2.3',
  apiVersion: 1,
  library: '/games/Ember.ptlib' as string | null,
  host: { os: 'macOS', architecture: 'Arm64', runtime: '.NET 11' },
  worker: { connected: false },
  formats: { pictures: ['.png'], environments: ['.hdr'], meshes: ['.obj'] },
  textureSides: [256, 512, 1024, 2048],
  defaultTextureSide: 2048
};

describe('App', () => {
  it('starts with the Library and nothing that would be empty', async () => {
    const element = await render({ kind: 'connecting' });

    for (const pane of ['Library', 'Start', 'Inspector']) {
      expect(element.textContent).toContain(pane);
    }

    for (const pane of ['Viewport', 'Texture', 'Graph']) {
      expect(element.querySelector(`[data-pane="${pane}"]`)).toBeNull();
      expect(element.textContent).not.toContain(pane);
    }
  });

  it('shows the Server, its library and its Worker in the status bar', async () => {
    const element = await render({ kind: 'online', system });

    const status = element.querySelector('footer[role=status]')?.textContent;
    expect(status).toContain('Patina 1.2.3');
    // The library by its name, with the whole path on hover (#200).
    expect(status).toContain('Ember');
    expect(status).not.toContain('/games/');
    expect(element.querySelector('[data-testid=status-library]')?.getAttribute('title')).toBe('/games/Ember.ptlib');
    expect(status).toContain('GPU starting');
  });

  it('has one Undo, Redo and Save, for whichever document is in front, from the command table (#200)', async () => {
    const element = await render({ kind: 'online', system });

    for (const [id, title] of [
      ['app-undo', /^Undo \(.+Z\)$/u],
      ['app-redo', /^Redo \(.+Z\)$/u],
      ['app-save', /^Save \(.+S\)$/u]
    ] as const) {
      const button = element.querySelector<HTMLButtonElement>(`[data-testid=${id}]`);
      expect(button?.disabled, id).toBe(true); // nothing is open
      expect(button?.title, id).toMatch(title);
    }
  });

  it('says so when no library is open', async () => {
    const element = await render({ kind: 'online', system: { ...system, library: null } });

    expect(element.querySelector('footer[role=status]')?.textContent).toContain('No library open');
  });

  it('says why when the Server cannot be reached', async () => {
    const element = await render({ kind: 'offline', reason: 'The engine stopped.', detail: 'connection refused' });

    expect(element.querySelector('footer[role=status]')?.textContent).toContain('The engine stopped.');
    expect(element.querySelector('footer[role=status]')?.textContent).not.toContain('connection refused');
    expect(element.querySelector('[data-testid=status-offline]')?.getAttribute('title')).toBe('connection refused');
  });
});
