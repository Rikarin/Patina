import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import type { GraphNodeType } from '@patina/api-client';
import { CurveEditor, type Curves, GradientRamp, type RampStop, blackToWhite, straightCurves } from '@patina/studio-ui';
import { XuiColorPicker } from '@xui/color-picker';
import { XuiNumericInput } from '@xui/numeric-input';
import { XuiSelect } from '@xui/select';
import { SystemStatus } from '../core/system-status';
import { ProjectStore } from '../library/project-store';
import { GraphInspector, Inspector, keptLanes } from './graph-inspector';
import { GraphSelection, GraphSettings, freeParameterName } from './graph-settings';
import { GraphStore } from './graph-store';
import { node } from './graph-testing';

const tintType: GraphNodeType = {
  path: 'Colour/Tint',
  title: 'Tint',
  category: 'Colour',
  summary: '',
  hasPreview: true,
  ports: [{ name: 'Colour', direction: 'Input', kind: 'Float4', default: [1, 0, 0, 1], summary: '' }],
  settings: []
};

function fakeStore() {
  return {
    types: signal(new Map([[tintType.path, tintType]])),
    compounds: signal(new Map()),
    document: signal(null),
    selection: signal(new Set<number>()),
    selectedNode: signal(null),
    selectedNote: signal<number | null>(null),
    graphInspected: signal(false),
    editable: signal(true),
    valueOf: vi.fn((_node: number, _port: string, fallback: readonly number[]) => fallback),
    textOf: vi.fn((_node: number, _port: string, fallback: string) => fallback),
    setValue: vi.fn(),
    setText: vi.fn(),
    endGesture: vi.fn(),
    shortcut: vi.fn(() => true),
    hasNumberSource: signal(false),
    shownPorts: signal(new Set<string>()),
    wireInto: vi.fn((): { fromPort: string } | undefined => undefined)
  };
}

describe('the colour control', () => {
  function render() {
    const store = fakeStore();
    TestBed.configureTestingModule({ providers: [{ provide: GraphStore, useValue: store }] });
    const fixture = TestBed.createComponent(GraphInspector);
    fixture.componentRef.setInput('node', { ...node(1), type: tintType.path });
    fixture.detectChanges();
    const picker = fixture.debugElement.query(By.directive(XuiColorPicker));
    return { store, fixture, picker: picker.componentInstance as XuiColorPicker };
  }

  it("shows the lanes as the picker's hex", () => {
    expect(render().picker.value()).toBe('#FF0000FF');
  });

  it('writes a new colour as lanes, and writes nothing for the colour it already holds', () => {
    const { store, picker } = render();
    picker.value.set('#FF0000FF');
    expect(store.setValue).not.toHaveBeenCalled();
    picker.value.set('#00FF0080');
    expect(store.setValue).toHaveBeenCalledWith(1, 'Colour', [0, 1, 0, 128 / 255]);
  });

  it('keeps a lane the picker did not move, past what eight bits hold', () => {
    expect(keptLanes([2, 0.18, 0.5, 1], [0.25, 46 / 255, 1, 1])).toEqual([0.25, 0.18, 1, 1]);
    expect(keptLanes([1, 0, 0], [0, 0, 0, 1])).toEqual([0, 0, 0, 1]);
  });

  it('has a number for each lane, which writes past 1 with the other lanes as they were', () => {
    const { store, fixture } = render();
    const lanes = fixture.debugElement
      .queryAll(By.directive(XuiNumericInput))
      .map(input => input.componentInstance as XuiNumericInput)
      .slice(-4);
    expect(lanes.map(input => input.value())).toEqual([1, 0, 0, 1]);
    lanes[2]?.valueChange.emit(2);
    expect(store.setValue).toHaveBeenCalledWith(1, 'Colour', [1, 0, 2, 1]);
  });

  it('ignores what the picker sends while it is still being typed', () => {
    const { store, picker } = render();
    picker.value.set('#00F');
    expect(store.setValue).not.toHaveBeenCalled();
  });
});

describe('a number port from the Inspector', () => {
  const scale: GraphNodeType = {
    ...tintType,
    ports: [{ name: 'Scale', direction: 'Input', kind: 'Float', default: [1], summary: '' }]
  };

  function render(wire: { fromPort: string } | undefined, hasNumberSource = true) {
    const store = {
      ...fakeStore(),
      types: signal(new Map([[scale.path, scale]])),
      hasNumberSource: signal(hasNumberSource),
      wireInto: vi.fn(() => wire),
      togglePort: vi.fn(),
      disconnect: vi.fn()
    };
    TestBed.configureTestingModule({ providers: [{ provide: GraphStore, useValue: store }] });
    const fixture = TestBed.createComponent(GraphInspector);
    fixture.componentRef.setInput('node', { ...node(1), type: scale.path });
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    return { store, element, found: (id: string) => element.querySelector<HTMLElement>(`[data-testid="${id}"]`) };
  }

  it('offers a port where there is a number to wire in, and asks the store for it', () => {
    const { store, found } = render(undefined);

    found('port-Scale')?.click();

    expect(store.togglePort).toHaveBeenCalledWith(1, 'Scale');
  });

  it('offers none in a graph with no number to wire in', () => {
    expect(render(undefined, false).found('port-Scale')).toBeNull();
  });

  it('shows what drives a wired number instead of a control that would do nothing', () => {
    const { store, element, found } = render({ fromPort: 'Columns' });

    expect(found('wired-Scale')?.textContent).toContain('Columns');
    expect(element.querySelector('xui-numeric-input')).toBeNull();

    found('wired-Scale')?.querySelector('button')?.click();

    expect(store.disconnect).toHaveBeenCalledWith(1, 'Scale');
  });
});

describe('the graph settings', () => {
  function render(settings: Record<string, string>) {
    const store = {
      ...fakeStore(),
      document: signal({
        name: 'Rust',
        nodes: new Map(),
        settings,
        parameters: [],
        interface: [],
        groups: [],
        comments: []
      }),
      setSetting: vi.fn()
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
    const select = (key: string) =>
      fixture.debugElement.query(By.css(`[data-testid=setting-${key}]`)).query(By.directive(XuiSelect))
        .componentInstance as XuiSelect<string>;
    return { store, select };
  }

  it('offers the sizes the Server lists, keeping one that is not among them', () => {
    const { select } = render({ baseWidth: '1024', baseHeight: '1024', size: '300' });

    expect(select('size').items()).toEqual(['512', '1024', '2048', '300']);
  });

  it('shows the longer side of the base for a graph that names no size', () => {
    const { select } = render({ baseWidth: '512', baseHeight: '1024' });

    expect(select('size').value()).toBe('1024');
  });

  it('changes the size without touching the base', () => {
    const { store, select } = render({ baseWidth: '1024', baseHeight: '1024', size: '1024' });

    select('size').value.set('2048');

    expect(store.setSetting.mock.calls).toEqual([['size', '2048']]);
  });
});

/** The Inspector's own logic around what its panes hold, which have tests of their own. */
const selectionOrBox = `
  @if (store.selection().size > 1) {
    <ptn-graph-selection />
  }
  <input data-testid="box" />
`;

describe('the Inspector', () => {
  function render(arrange: (store: ReturnType<typeof fakeStore>) => void = () => undefined) {
    const store = fakeStore();
    arrange(store);
    TestBed.configureTestingModule({ providers: [{ provide: GraphStore, useValue: store }] });
    TestBed.overrideComponent(Inspector, {
      set: { imports: [GraphSelection, GraphSettings], template: selectionOrBox }
    });
    const fixture = TestBed.createComponent(Inspector);
    fixture.detectChanges();
    return { store, fixture };
  }

  const key = (target: Element) => {
    target.dispatchEvent(new KeyboardEvent('keydown', { key: 'z', ctrlKey: true, bubbles: true }));
  };

  it('says how many nodes are selected rather than showing one of them', () => {
    const { fixture } = render(store => {
      store.selection.set(new Set([1, 2]));
      Object.assign(store, {
        document: signal({
          nodes: new Map([
            [1, { ...node(1), type: tintType.path }],
            [2, node(2)]
          ])
        })
      });
    });
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('[data-testid=inspector-title]')?.textContent).toBe('2 nodes');
    expect(element.querySelector('[data-testid=selection-list]')?.textContent).toContain('Tint');
  });

  it('passes Undo to the graph from a control', () => {
    const { store, fixture } = render(held => {
      Object.assign(held, { document: signal({ nodes: new Map() }) });
    });
    const box = (fixture.nativeElement as HTMLElement).querySelector('[data-testid=box]');
    if (box) {
      key(box);
    }

    expect(store.shortcut).toHaveBeenCalledOnce();
  });

  it('leaves Undo to a box with text still being typed, until it is committed', () => {
    const { store, fixture } = render(held => {
      Object.assign(held, { document: signal({ nodes: new Map() }) });
    });
    const box = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>('[data-testid=box]');
    if (!box) {
      throw new Error('No box.');
    }

    box.dispatchEvent(new Event('input', { bubbles: true }));
    key(box);
    expect(store.shortcut).not.toHaveBeenCalled();
    box.dispatchEvent(new Event('change', { bubbles: true }));
    key(box);
    expect(store.shortcut).toHaveBeenCalledOnce();
  });
});

describe('Save from a box still being typed in', () => {
  it('commits the text by leaving the box, then saves', () => {
    const store = fakeStore();
    Object.assign(store, { document: signal({ nodes: new Map() }) });
    TestBed.configureTestingModule({ providers: [{ provide: GraphStore, useValue: store }] });
    TestBed.overrideComponent(Inspector, { set: { imports: [], template: '<input data-testid="box" />' } });
    const fixture = TestBed.createComponent(Inspector);
    fixture.detectChanges();
    const box = (fixture.nativeElement as HTMLElement).querySelector('input');
    if (!box) {
      throw new Error('No box.');
    }

    (fixture.nativeElement as HTMLElement).ownerDocument.body.append(fixture.nativeElement as HTMLElement);
    box.focus();
    box.dispatchEvent(new Event('input', { bubbles: true }));
    box.dispatchEvent(new KeyboardEvent('keydown', { key: 's', ctrlKey: true, bubbles: true }));
    expect(store.shortcut).toHaveBeenCalledOnce();
    expect(document.activeElement).not.toBe(box);
  });
});

describe('a control dragged faster than change detection', () => {
  it('keeps every key when a move arrives before the input has caught up', () => {
    const fixture = TestBed.createComponent(CurveEditor);
    const written: Curves[] = [];
    fixture.componentRef.setInput('curves', straightCurves);
    fixture.componentInstance.curvesChange.subscribe(curves => written.push(curves));
    fixture.detectChanges();
    const svg = (fixture.nativeElement as HTMLElement).querySelector('svg');
    if (!svg) {
      throw new Error('No curve.');
    }

    svg.setPointerCapture = () => undefined;
    svg.getBoundingClientRect = () => new DOMRect(0, 0, 100, 100);
    const pointer = (type: string, clientX: number, clientY: number) => {
      svg.dispatchEvent(new MouseEvent(type, { clientX, clientY, bubbles: true }));
    };

    pointer('pointerdown', 50, 50);
    pointer('pointermove', 50, 20); // nothing has run change detection since the press
    expect(written.at(-1)?.r).toEqual([
      { x: 0, y: 0 },
      { x: 0.5, y: 0.8 },
      { x: 1, y: 1 }
    ]);
  });

  it('keeps every stop in the same way', () => {
    const fixture = TestBed.createComponent(GradientRamp);
    const written: (readonly RampStop[])[] = [];
    fixture.componentRef.setInput('stops', blackToWhite);
    fixture.componentInstance.stopsChange.subscribe(stops => written.push(stops));
    fixture.detectChanges();
    const bar = (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>('[role=group]');
    if (!bar) {
      throw new Error('No ramp.');
    }

    bar.setPointerCapture = () => undefined;
    bar.getBoundingClientRect = () => new DOMRect(0, 0, 100, 10);
    bar.dispatchEvent(new MouseEvent('pointerdown', { clientX: 25, bubbles: true }));
    bar.dispatchEvent(new MouseEvent('pointermove', { clientX: 40, bubbles: true }));
    expect(written.at(-1)?.map(stop => stop.position)).toEqual([0, 0.4, 1]);
  });
});

describe('freeParameterName', () => {
  const named = (name: string) => ({ name, default: '', summary: '', kind: 'Float', group: '', accepted: [] });

  it('finds a name the graph does not use', () => {
    expect(freeParameterName([])).toBe('Parameter1');
    expect(freeParameterName([named('Parameter2')])).toBe('Parameter3');
    expect(freeParameterName([named('Wear'), named('Parameter3')])).toBe('Parameter4');
  });
});
