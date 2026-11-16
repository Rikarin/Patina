import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import type { GraphPresetState, GraphSettingDefinition } from '@patina/api-client';
import { XuiSelect } from '@xui/select';
import { SystemStatus } from '../core/system-status';
import { ProjectStore } from '../library/project-store';
import { GraphSettings } from './graph-settings';
import { GraphStore } from './graph-store';

function parameter(name: string, more: Partial<GraphSettingDefinition> = {}): GraphSettingDefinition {
  return { name, default: '0', summary: '', kind: 'Float', group: '', accepted: [], ...more };
}

/** A worn metal: a switch that shows the rust's amount, under a group of its own, and a preset. */
const parameters = [
  parameter('rusty', { kind: 'Bool', default: 'false', label: 'Rusty' }),
  parameter('amount', {
    label: 'Rust amount',
    group: 'Rust',
    visibleIf: { setting: 'rusty', value: 'true', negated: false }
  }),
  parameter('tint', { label: 'Tint' })
];

const presets: GraphPresetState[] = [{ name: 'Old', values: { rusty: 'true', amount: '0.8' } }];

function render() {
  const defaults = signal<Record<string, string>>({ rusty: 'false' });
  const store = {
    document: signal({
      name: 'Metal',
      nodes: new Map(),
      settings: {},
      parameters,
      presets,
      interface: [],
      groups: [],
      comments: []
    }),
    editable: signal(true),
    types: signal(new Map()),
    selection: signal(new Set<number>()),
    parameterDefault: (held: GraphSettingDefinition) => defaults()[held.name] ?? held.default,
    applyPreset: vi.fn(),
    setSetting: vi.fn(),
    endGesture: vi.fn()
  };
  TestBed.configureTestingModule({
    providers: [
      { provide: GraphStore, useValue: store },
      { provide: SystemStatus, useValue: { textureSides: signal([512, 1024, 2048]) } },
      { provide: ProjectStore, useValue: { current: signal(null) } }
    ]
  });
  const fixture = TestBed.createComponent(GraphSettings);
  fixture.detectChanges();
  const shown = () =>
    fixture.debugElement
      .queryAll(By.css('[data-testid^="parameter-"]'))
      .map(element => element.attributes['data-testid'] ?? '')
      .filter(id => ['parameter-rusty', 'parameter-amount', 'parameter-tint'].includes(id));
  const click = (id: string) => {
    (fixture.debugElement.query(By.css(`[data-testid=${id}]`)).nativeElement as HTMLButtonElement).click();
    fixture.detectChanges();
  };
  return { store, fixture, defaults, shown, click };
}

describe('the parameters as used (#181)', () => {
  it('shows every parameter to its author, and as used only the ones showing, by their labels', () => {
    const { fixture, shown, click } = render();
    expect(shown()).toEqual(['parameter-rusty', 'parameter-tint', 'parameter-amount']);
    expect(fixture.debugElement.query(By.css('[data-testid=parameter-add]'))).not.toBeNull();

    click('parameters-as-used');

    expect(shown()).toEqual(['parameter-rusty', 'parameter-tint']);
    expect(
      fixture.debugElement
        .queryAll(By.css('[data-testid=parameter-title]'))
        .map(e => e.nativeElement.textContent.trim())
    ).toEqual(['Rusty', 'Tint']);
    expect(fixture.debugElement.query(By.css('[data-testid=parameter-add]'))).toBeNull();
    expect(fixture.debugElement.query(By.css('[aria-label="Parameter name"]'))).toBeNull();
    expect(fixture.debugElement.queryAll(By.css('[data-testid=parameter-group]'))).toEqual([]);
  });

  it('shows a parameter and its group once the value it waits on holds', () => {
    const { fixture, defaults, shown, click } = render();
    click('parameters-as-used');

    defaults.set({ rusty: 'true' });
    fixture.detectChanges();

    expect(shown()).toEqual(['parameter-rusty', 'parameter-tint', 'parameter-amount']);
    expect(
      fixture.debugElement
        .queryAll(By.css('[data-testid=parameter-group]'))
        .map(e => e.nativeElement.textContent.trim())
    ).toEqual(['Rust']);
  });

  it('offers the presets, as a node holding the graph does, and applies the one chosen', () => {
    const { store, fixture, click } = render();
    expect(fixture.debugElement.query(By.css('[data-testid=parameters-preset]'))).toBeNull();

    click('parameters-as-used');
    const select = fixture.debugElement.query(By.css('[data-testid=parameters-preset]')).query(By.directive(XuiSelect))
      .componentInstance as XuiSelect<GraphPresetState>;
    expect(select.items()).toEqual(presets);
    select.value.set(presets[0] ?? null);

    expect(store.applyPreset).toHaveBeenCalledWith('Old');
  });
});
