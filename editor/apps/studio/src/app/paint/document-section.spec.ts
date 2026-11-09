import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ProjectStore } from '../library/project-store';
import { DocumentSection } from './document-section';
import { PaintStore } from './paint-store';

async function render() {
  const edit = vi.fn((_command: unknown) => Promise.resolve(true));
  const paint = {
    state: signal({
      set: 'Body',
      width: 2048,
      height: 1024,
      channels: [{ usage: 'baseColor' }, { usage: 'roughness' }]
    }),
    channel: signal('baseColor'),
    vocabulary: signal({
      usages: [
        { value: 'baseColor', label: 'Base Color' },
        { value: 'roughness', label: 'Roughness' },
        { value: 'occlusion', label: 'Ambient Occlusion' }
      ]
    }),
    // A fill is chosen, which no brush paints: the document's settings are there all the same (#201).
    canPaint: signal(false),
    showChannel: vi.fn(),
    edit
  };
  TestBed.configureTestingModule({
    providers: [
      { provide: PaintStore, useValue: paint },
      {
        provide: ProjectStore,
        useValue: {
          current: signal({ name: 'Crate' }),
          meshName: signal('crate_low.glb'),
          document: signal({ kind: 'texturing', name: 'Crate' }),
          settingsOpen: signal(false)
        }
      }
    ]
  });
  const fixture = TestBed.createComponent(DocumentSection);
  await fixture.whenStable();
  return { element: fixture.nativeElement as HTMLElement, edit };
}

describe('DocumentSection', () => {
  it('shows the document, its size and its channels, and adds a channel whatever is chosen', async () => {
    const { element, edit } = await render();
    const text = (id: string) => element.querySelector(`[data-testid=${id}]`)?.textContent.trim();

    expect(text('paint-project')).toBe('Crate');
    expect(text('paint-mesh-name')).toBe('crate_low.glb');
    expect(text('paint-document-set')).toBe('Body');
    expect(text('paint-document-size')).toBe('2048 × 1024');
    expect(text('paint-channel-roughness')).toBe('Roughness');

    const add = element.querySelector<HTMLSelectElement>('[data-testid=paint-add-channel]');
    expect([...(add?.options ?? [])].map(option => option.text.trim())).toEqual(['Add Channel…', 'Ambient Occlusion']);
    if (add) {
      add.value = 'occlusion';
      add.dispatchEvent(new Event('change'));
    }

    expect(edit).toHaveBeenCalledWith({ op: 'addChannel', usage: 'occlusion' });
  });
});
