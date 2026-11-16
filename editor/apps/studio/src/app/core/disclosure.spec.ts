import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Disclosure } from '@patina/studio-ui';

@Component({
  imports: [Disclosure],
  template: `<ptn-disclosure [label]="label"><input data-testid="inside" /></ptn-disclosure>`
})
class Host {
  protected readonly label = 'Memory by file';
}

describe('Disclosure', () => {
  beforeAll(() => {
    // jsdom has no Web Animations; xUI's collapse animates its height with them.
    Element.prototype.animate = () => ({ finished: Promise.resolve() }) as unknown as Animation;
  });

  it('opens and closes under its button, and holds nothing that takes the focus while closed (#195)', async () => {
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    const toggle = element.querySelector<HTMLButtonElement>('[data-disclosure-toggle]');

    expect(toggle?.textContent.trim()).toBe('Memory by file');
    expect(toggle?.getAttribute('aria-expanded')).toBe('false');
    expect(element.querySelector('[data-testid=inside]')).toBeNull();

    toggle?.click();
    await fixture.whenStable();
    expect(toggle?.getAttribute('aria-expanded')).toBe('true');
    expect(element.querySelector('[data-testid=inside]')).not.toBeNull();

    toggle?.click();
    await fixture.whenStable();
    expect(toggle?.getAttribute('aria-expanded')).toBe('false');
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(element.querySelector('[data-testid=inside]')).toBeNull();
    });
  });
});
