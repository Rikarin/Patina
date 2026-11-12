import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { StackFilterKind } from '@patina/api-client';
import { chooseIn } from '../core/select-testing';
import { AnchorInputs, type AnchorSources } from './anchor-inputs';
import { tagSwatch } from './layer-tags';
import { PaintStore } from './paint-store';

const blur: Pick<StackFilterKind, 'inputs'> = {
  inputs: [{ port: 'Input', summary: 'The picture.', subject: true, optional: false, channel: '' }]
};

function render(inputs: AnchorSources = {}, channels = ['baseColor', 'height']) {
  TestBed.configureTestingModule({
    providers: [{ provide: PaintStore, useValue: { state: signal({ channels: channels.map(usage => ({ usage })) }) } }]
  });
  const fixture = TestBed.createComponent(AnchorInputs);
  fixture.componentRef.setInput('kind', blur);
  fixture.componentRef.setInput('inputs', inputs);
  fixture.componentRef.setInput('anchors', ['Ground', 'Wear']);
  fixture.componentRef.setInput('testId', 'in-');
  const emitted: AnchorSources[] = [];
  fixture.componentInstance.inputsChange.subscribe(next => emitted.push(next));
  fixture.detectChanges();
  const element = fixture.nativeElement as HTMLElement;
  const choose = (testId: string, name: string) => chooseIn(fixture, element, testId, name);
  return { element, emitted, choose };
}

describe('a mask generator’s anchor slots (#111)', () => {
  it('feeds an image from an anchor point in height, which wear is read from, until a channel is chosen', async () => {
    const { emitted, choose } = render();
    await choose('in-Input-anchor', 'Wear');
    expect(emitted.at(-1)).toEqual({ Input: { anchor: 'Wear', channel: 'height' } });
  });

  it('reads the first channel in a stack with no height', async () => {
    const { emitted, choose } = render({}, ['baseColor', 'roughness']);
    await choose('in-Input-anchor', 'Ground');
    expect(emitted.at(-1)).toEqual({ Input: { anchor: 'Ground', channel: 'baseColor' } });
  });

  it('changes the channel of a fed slot, and lets the slot go back to nothing', async () => {
    const { emitted, choose, element } = render({ Input: { anchor: 'Ground', channel: 'height' } });
    expect(element.querySelector('[data-testid="in-Input-channel"]')).not.toBeNull();

    await choose('in-Input-channel', 'Base Color');
    expect(emitted.at(-1)).toEqual({ Input: { anchor: 'Ground', channel: 'baseColor' } });

    await choose('in-Input-anchor', 'Choose an anchor point');
    expect(emitted.at(-1)).toEqual({});
  });
});

describe('a layer’s colour tag (#106)', () => {
  it('has a swatch for every colour and none for no tag', () => {
    expect(tagSwatch('Violet')).toMatch(/^#[0-9a-f]{6}$/u);
    expect(tagSwatch('None')).toBeNull();
    expect(tagSwatch(undefined)).toBeNull();
  });
});
