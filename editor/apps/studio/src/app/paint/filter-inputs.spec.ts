import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { StackFilterKind, StackInput } from '@patina/api-client';
import { chooseIn } from '../core/select-testing';
import { ProjectStore } from '../library/project-store';
import { FilterInputs, type FilterSources } from './filter-inputs';
import { PaintStore } from './paint-store';

const warp: Pick<StackFilterKind, 'inputs'> = {
  inputs: [
    { port: 'Input', summary: '', subject: true, optional: false, channel: '' },
    { port: 'Warp', summary: 'The field.', subject: false, optional: false, channel: '' }
  ]
};

const clouds: StackFilterKind = {
  node: 'Noise/Clouds',
  name: 'Clouds',
  category: 'Noise',
  summary: '',
  ports: [{ name: 'Scale', direction: 'Input', kind: 'Float', default: [4], summary: '' }],
  settings: [],
  inputs: []
};

function render(mask = false, inputs: FilterSources = {}, kind: Pick<StackFilterKind, 'inputs'> = warp) {
  const store = {
    vocabulary: signal({
      inputs: ['Beneath', 'Anchor', 'Generator', 'Texture', 'Bake'].map(value => ({
        value,
        label: value === 'Beneath' ? 'Layers Beneath' : value
      })),
      meshMaps: [{ value: 'curvature', label: 'Curvature' }],
      sources: [clouds]
    }),
    state: signal({ channels: [{ usage: 'baseColor' }, { usage: 'height' }] })
  };
  TestBed.configureTestingModule({
    providers: [
      { provide: PaintStore, useValue: store },
      { provide: ProjectStore, useValue: { pickPicture: vi.fn(() => Promise.resolve('images/field.png')) } }
    ]
  });
  const fixture = TestBed.createComponent(FilterInputs);
  fixture.componentRef.setInput('kind', kind);
  fixture.componentRef.setInput('mask', mask);
  fixture.componentRef.setInput('inputs', inputs);
  fixture.componentRef.setInput('testId', 'in-');
  const emitted: FilterSources[] = [];
  fixture.componentInstance.inputsChange.subscribe(next => emitted.push(next));
  fixture.detectChanges();
  const element = fixture.nativeElement as HTMLElement;
  const source = (port: string) => element.querySelector<HTMLElement>(`[data-testid="in-${port}-source"]`);
  const pick = (port: string, name: string) => chooseIn(fixture, element, `in-${port}-source`, name);
  return { fixture, element, emitted, source, pick };
}

describe('a filter input slot', () => {
  it('offers a slot for each image, the subject reading the layers beneath until told otherwise', () => {
    const { source } = render();
    expect(source('Input')?.textContent).toContain('Layers Beneath · this channel');
    expect(source('Warp')?.textContent.trim()).toBe('Choose what it reads');
  });

  it('says a channel the set lacks is read untouched, and one it has is read from the layers beneath', () => {
    const { source } = render(
      false,
      {},
      {
        inputs: [
          { port: 'Holes', summary: '', subject: false, optional: false, channel: 'opacity' },
          { port: 'Tone', summary: '', subject: false, optional: false, channel: 'baseColor' }
        ]
      }
    );
    expect(source('Holes')?.textContent).toContain('Untouched · Opacity');
    expect(source('Tone')?.textContent).toContain('Layers Beneath · Base Color');
  });

  it('has no slot for a mask step’s subject, which is the mask', () => {
    const { source } = render(true);
    expect(source('Input')).toBeNull();
    expect(source('Warp')).not.toBeNull();
  });

  it('starts a generator from a soft noise, and a mask reading beneath from a named channel', async () => {
    const generator = render();
    await generator.pick('Warp', 'Generator');
    expect(generator.emitted.at(-1)?.['Warp']).toMatchObject({ source: 'Generator', generator: 'Noise/Clouds' });

    TestBed.resetTestingModule();
    const beneath = render(true);
    await beneath.pick('Warp', 'Layers Beneath');
    expect(beneath.emitted.at(-1)?.['Warp']).toMatchObject({ source: 'Beneath', channel: 'baseColor' });
  });

  it('sends the generator’s own fields with the rest of what it reads, and clears a slot back to its default', async () => {
    const held: StackInput = { source: 'Generator', channel: '', generator: 'Noise/Clouds', asset: '', map: '' };
    const { element, emitted, pick } = render(false, { Warp: held, Input: { ...held, source: 'Beneath' } });
    const scale = element.querySelector('[data-testid="in-Warp-Scale"]');
    expect(scale).not.toBeNull();

    await pick('Warp', 'Choose what it reads');
    expect(Object.keys(emitted.at(-1) ?? {})).toEqual(['Input']);
  });
});
