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
        useValue: { state: signal(state), textureSides: signal([256, 512, 1024, 2048]), defaultTextureSide: signal(2048) }
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
  host: { os: 'macOS', architecture: 'Arm64', runtime: '.NET 10' },
  worker: { connected: false },
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
    expect(status).toContain('/games/Ember.ptlib');
    expect(status).toContain('GPU starting');
  });

  it('says so when no library is open', async () => {
    const element = await render({ kind: 'online', system: { ...system, library: null } });

    expect(element.querySelector('footer[role=status]')?.textContent).toContain('No library open');
  });

  it('says why when the Server cannot be reached', async () => {
    const element = await render({ kind: 'offline', reason: 'connection refused' });

    expect(element.querySelector('footer[role=status]')?.textContent).toContain('connection refused');
  });
});
