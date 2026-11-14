import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { GraphStore } from '../graph/graph-store';
import { ProjectStore } from '../library/project-store';
import { PaintStore } from '../paint/paint-store';
import { Commands } from './commands';
import { KeyHelper } from './key-helper';

async function render() {
  TestBed.configureTestingModule({
    providers: [
      { provide: PaintStore, useValue: { state: signal({ id: 's' }) } },
      { provide: GraphStore, useValue: { document: signal(null) } },
      {
        provide: ProjectStore,
        useValue: { active: signal({ path: 'a', kind: 'texturing' }), graphInFront: signal(false) }
      }
    ]
  });
  const commands = TestBed.inject(Commands);
  const fixture = TestBed.createComponent(KeyHelper);
  fixture.componentRef.setInput('open', true);
  await fixture.whenStable();
  return { fixture, commands, element: fixture.nativeElement as HTMLElement };
}

const line = (element: HTMLElement, id: string) =>
  element.querySelector(`[data-testid="key-helper-line-${id}"]`)?.textContent.replace(/\s+/g, ' ').trim();

describe('KeyHelper', () => {
  it('lists only the keys something would answer now, by section, as the person has them (#190)', async () => {
    const { fixture, commands, element } = await render();
    commands.handle('save', { run: vi.fn() });
    commands.handle('undo', { run: vi.fn(), enabled: () => false });
    commands.handle('brushTool', { run: vi.fn() });
    await fixture.whenStable();

    expect(line(element, 'save')).toMatch(/^Save ?(Ctrl\+S|⌘S)$/);
    expect(element.querySelector('[data-testid="key-helper-File"]')).not.toBeNull();
    expect(line(element, 'undo')).toBeUndefined();
    expect(line(element, 'newProject')).toBeUndefined();
    expect(element.querySelector('[data-testid="key-helper-Painting"]')?.textContent).toContain('Brush');
  });

  it('says how to move the view only while there is a model to frame', async () => {
    const { fixture, commands, element } = await render();
    expect(element.querySelector('[data-testid="key-helper-navigation"]')).toBeNull();

    commands.handle('frameModel', { run: vi.fn() });
    await fixture.whenStable();

    expect(element.querySelector('[data-testid="key-helper-navigation"]')?.textContent).toContain('Orbit');
  });

  it('goes on Escape and on a click outside it, not on one inside', async () => {
    const { fixture, element } = await render();
    element.querySelector<HTMLElement>('[data-testid="key-helper"]')?.click();
    await fixture.whenStable();
    expect(element.querySelector('[data-testid="key-helper"]')).not.toBeNull();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    await fixture.whenStable();
    expect(element.querySelector('[data-testid="key-helper"]')).toBeNull();

    fixture.componentRef.setInput('open', true);
    await fixture.whenStable();
    element.querySelector<HTMLElement>('[data-testid="key-helper-backdrop"]')?.click();
    await fixture.whenStable();
    expect(element.querySelector('[data-testid="key-helper"]')).toBeNull();
  });
});
