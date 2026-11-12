import { Component, inject } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { GraphStore } from './graph-store';
import { FrontGraph, GraphViews } from './graph-views';

/** A pane that follows a graph, as the Inspector does. */
@Component({ selector: 'ptn-probe', template: '' })
class Probe {
  readonly store = inject(GraphStore);
}

@Component({
  selector: 'ptn-host',
  imports: [FrontGraph, Probe],
  template: `
    @for (front of [views.front()]; track front) {
      <ng-container ptnFrontGraph><ptn-probe /></ng-container>
    }
  `
})
class Host {
  readonly views = inject(GraphViews);
}

describe('GraphViews', () => {
  it('gives a pane the store of the graph touched last, and the main one again when the other goes (#40)', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const views = TestBed.inject(GraphViews);
    const probe = () => fixture.debugElement.children[0]?.injector.get(Probe).store;
    expect(probe()).toBe(views.main);

    const beside = TestBed.runInInjectionContext(() => new GraphStore());
    views.touch(beside);
    fixture.detectChanges();
    expect(views.front()).toBe(beside);
    expect(probe()).toBe(beside);

    views.release(views.main);
    expect(views.front()).toBe(beside);
    views.release(beside);
    fixture.detectChanges();
    expect(probe()).toBe(views.main);
  });
});
