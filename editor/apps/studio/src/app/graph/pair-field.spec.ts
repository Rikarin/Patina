import { TestBed } from '@angular/core/testing';
import { PairField } from './pair-field';

function field(value: string, widget: string | null, range: { minimum?: number; maximum?: number } = {}) {
  const fixture = TestBed.createComponent(PairField);
  fixture.componentRef.setInput('value', value);
  fixture.componentRef.setInput('widget', widget);
  fixture.componentRef.setInput('minimum', range.minimum ?? null);
  fixture.componentRef.setInput('maximum', range.maximum ?? null);
  fixture.detectChanges();
  const emitted: string[] = [];
  fixture.componentInstance.valueChange.subscribe(text => emitted.push(text));
  const element = fixture.nativeElement as HTMLElement;
  const component = fixture.componentInstance as unknown as {
    setX(x: number): void;
    setY(y: number): void;
    setBoth(x: number, y: number): void;
  };
  return { fixture, element, emitted, component };
}

describe('PairField (#181)', () => {
  it('draws a position as a point on a square beside its numbers', () => {
    const { element } = field('0.25,0.75', 'Position', { minimum: 0, maximum: 1 });

    expect(element.querySelector('ptn-position-pad')).not.toBeNull();
    expect(element.querySelector('[data-testid="pair-linked"]')).toBeNull();
  });

  it('keeps a position within its range', () => {
    const { component, emitted } = field('0.25,0.75', 'Position', { minimum: 0, maximum: 1 });

    component.setBoth(1.5, -1);

    expect(emitted).toEqual(['1,0']);
  });

  it('moves a square tiling across and down together until it is unlinked', () => {
    const { fixture, element, component, emitted } = field('2,2', 'Tiling');
    expect(element.querySelector('ptn-position-pad')).toBeNull();

    component.setX(4);
    expect(emitted).toEqual(['4,4']);

    fixture.componentRef.setInput('value', '4,4');
    fixture.detectChanges();
    (element.querySelector('[data-testid="pair-linked"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    component.setY(1);
    expect(emitted).toEqual(['4,4', '4,1']);
  });

  it('moves two plain numbers apart', () => {
    const { component, emitted } = field('3,3', null);

    component.setX(5);

    expect(emitted).toEqual(['5,3']);
  });
});
