import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { App } from './app';
import { type ServerState, SystemStatus } from './core/system-status';

async function render(state: ServerState): Promise<HTMLElement> {
  TestBed.configureTestingModule({
    providers: [{ provide: SystemStatus, useValue: { state: signal(state) } }]
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
  worker: { connected: false }
};

describe('App', () => {
  it('docks the Library, Viewport and Inspector panes', async () => {
    const element = await render({ kind: 'connecting' });

    for (const pane of ['Library', 'Viewport', 'Inspector']) {
      expect(element.textContent).toContain(pane);
    }
  });

  it('shows the Server, its library and its Worker in the status bar', async () => {
    const element = await render({ kind: 'online', system });

    const status = element.querySelector('[role=status]')?.textContent;
    expect(status).toContain('Patina 1.2.3');
    expect(status).toContain('/games/Ember.ptlib');
    expect(status).toContain('Worker not connected');
  });

  it('says so when no library is open', async () => {
    const element = await render({ kind: 'online', system: { ...system, library: null } });

    expect(element.querySelector('[role=status]')?.textContent).toContain('No library open');
  });

  it('says why when the Server cannot be reached', async () => {
    const element = await render({ kind: 'offline', reason: 'connection refused' });

    expect(element.querySelector('[role=status]')?.textContent).toContain('connection refused');
  });
});
