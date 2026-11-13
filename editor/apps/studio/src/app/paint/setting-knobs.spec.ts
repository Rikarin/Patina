import { TestBed } from '@angular/core/testing';
import type { GraphSettingDefinition } from '@patina/api-client';
import { chooseIn } from '../core/select-testing';
import { type KnobChange, SettingKnobs } from './setting-knobs';

const amount: GraphSettingDefinition = {
  name: 'amount',
  label: 'Amount',
  default: '0.5',
  summary: 'How much.',
  kind: 'Float',
  minimum: 0,
  maximum: 1,
  group: '',
  accepted: []
};

const style: GraphSettingDefinition = {
  name: 'style',
  default: 'soft',
  summary: 'Which look.',
  kind: 'Text',
  group: '',
  accepted: ['soft', 'hard'],
  labels: ['Soft', 'Hard']
};

function render(values: Record<string, string> = {}) {
  const fixture = TestBed.createComponent(SettingKnobs);
  fixture.componentRef.setInput('parameters', [amount, style]);
  fixture.componentRef.setInput('values', values);
  fixture.componentRef.setInput('testId', 'knob-');
  const emitted: KnobChange[] = [];
  fixture.componentInstance.knobChange.subscribe(next => emitted.push(next));
  fixture.detectChanges();
  const element = fixture.nativeElement as HTMLElement;
  return { fixture, element, emitted };
}

describe('the knobs of a generator a layer or a mask runs (#120)', () => {
  it('shows each setting by its label, at its default until it is set', () => {
    const { element } = render();
    expect(element.textContent).toContain('Amount');
    expect(element.textContent).toContain('Style');
    expect(element.textContent).toContain('50%');
  });

  it('shows what a setting is set to', () => {
    const { element } = render({ amount: '0.25' });
    expect(element.textContent).toContain('25%');
  });

  it('says which setting changed and to what', async () => {
    const { fixture, element, emitted } = render();
    await chooseIn(fixture, element, 'knob-style', 'Hard');
    expect(emitted.at(-1)).toEqual({ name: 'style', value: 'hard' });
  });
});
