import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { CommandTable } from '../core/command-table';
import { Commands } from '../core/commands';
import { SizeSteps, radiusAt, sizePosition } from './brush-size';
import { type CurveAction, type FillMode, type FillShape, PaintStore, type PaintTool } from './paint-store';
import { ToolSettingsBar } from './tool-settings-bar';

describe('ToolSettingsBar (#201)', () => {
  function bar(tool: PaintTool = 'brush') {
    const paint = {
      tool: signal<PaintTool>(tool),
      radius: signal(24),
      brush: signal({ flow: 0.5, opacity: 1, sizeSpace: 'screen' }),
      colour: signal('#d9401a'),
      recentColours: signal<string[]>([]),
      masking: signal(false),
      maskGrey: signal(0.25),
      fillMode: signal<FillMode>('polygon'),
      fillShape: signal<FillShape>('click'),
      pickEveryChannel: signal(true),
      curveAction: signal<CurveAction>('paint'),
      vocabulary: signal(null)
    };
    TestBed.configureTestingModule({
      providers: [
        { provide: PaintStore, useValue: paint },
        {
          provide: Commands,
          useValue: { spec: (id: string) => CommandTable.find(command => command.id === id) }
        }
      ]
    });
    const fixture = TestBed.createComponent(ToolSettingsBar);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const find = (id: string) => element.querySelector<HTMLElement>(`[data-testid=tool-settings-${id}]`);
    return { fixture, element, paint, find };
  }

  it('is a pane bar named for what it holds, and names the tool in hand', () => {
    const { element, find } = bar();
    const toolbar = element.querySelector('[data-testid=tool-settings-bar]');
    expect(toolbar?.getAttribute('role')).toBe('toolbar');
    expect(toolbar?.getAttribute('aria-label')).toBe('Tool settings');
    expect(find('name')?.textContent.trim()).toBe(CommandTable.find(command => command.id === 'brushTool')?.label);
  });

  it("offers a stroke's size, flow, opacity and colour, the brush's own values", () => {
    const { find } = bar();
    expect(find('size')).not.toBeNull();
    expect(find('size-readout')?.textContent.trim()).toBe('24 px');
    expect(find('flow')).not.toBeNull();
    expect(find('opacity')).not.toBeNull();
    expect(find('colour')).not.toBeNull();
    expect(find('fill-mode')).toBeNull();
  });

  it('follows the size set elsewhere, in its space’s words', () => {
    const { fixture, paint, find } = bar();
    paint.radius.set(80);
    paint.brush.set({ ...paint.brush(), sizeSpace: 'texture' });
    fixture.detectChanges();
    expect(find('size-readout')?.textContent.trim()).toBe('80 tx');
  });

  it('offers no colour for a tool that lays none, and a grey while on a mask', () => {
    const { fixture, paint, find } = bar('eraser');
    expect(find('colour')).toBeNull();
    expect(find('size')).not.toBeNull();

    paint.tool.set('brush');
    paint.masking.set(true);
    fixture.detectChanges();
    expect(find('colour')).toBeNull();
    expect(find('mask-grey')).not.toBeNull();
  });

  it('lets a line or a path erase or smudge along itself, with no colour then (#130)', () => {
    const { fixture, paint, find } = bar('path');
    expect(find('curve-action')).not.toBeNull();
    expect(find('colour')).not.toBeNull();

    paint.curveAction.set('erase');
    fixture.detectChanges();
    expect(find('colour')).toBeNull();
    expect(find('size')).not.toBeNull();

    paint.tool.set('brush');
    fixture.detectChanges();
    expect(find('curve-action')).toBeNull();
    expect(find('colour')).not.toBeNull();
  });

  it("gives the fill its own choices instead of a stroke's", () => {
    const { find } = bar('fill');
    expect(find('size')).toBeNull();
    expect(find('fill-mode')).not.toBeNull();
    expect(find('fill-shape')).not.toBeNull();
    expect(find('colour')).not.toBeNull();
  });

  it('gives the picker its switch alone', () => {
    const { find } = bar('picker');
    expect(find('size')).toBeNull();
    expect(find('colour')).toBeNull();
    expect(find('pick-every-channel')).not.toBeNull();
  });
});

describe('brush size (#201)', () => {
  const paint = (radius: number) =>
    ({ radius: () => radius, vocabulary: () => ({ brush: { maximumRadius: 1000 } }) }) as unknown as PaintStore;

  it('runs from 1 at the start of the slider to the largest at its end', () => {
    expect(radiusAt(paint(1), 0)).toBe(1);
    expect(radiusAt(paint(1), SizeSteps)).toBe(1000);
    expect(sizePosition(paint(1000))).toBe(SizeSteps);
  });

  it('comes back to the radius it was read from, for any whole radius', () => {
    for (const radius of [1, 2, 7, 24, 100, 333, 999]) {
      expect(Math.abs(radiusAt(paint(radius), sizePosition(paint(radius))) - radius)).toBeLessThanOrEqual(
        Math.ceil(radius * 0.01)
      );
    }
  });
});
