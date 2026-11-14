import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { CommandTable } from '../core/command-table';
import { Commands } from '../core/commands';
import { PaintStore, type PaintTool } from './paint-store';
import { PaintTools } from './paint-tools';
import { ToolStrip } from './tool-strip';

describe('ToolStrip (#127)', () => {
  function strip(state: object | null = { id: 's' }) {
    const tool = signal<PaintTool>('brush');
    const quickMask = signal<'off' | 'paint' | 'keep'>('off');
    const toggleQuickMask = vi.fn(() => quickMask.set(quickMask() === 'paint' ? 'keep' : 'paint'));
    TestBed.configureTestingModule({
      providers: [
        { provide: PaintStore, useValue: { tool, state: signal(state), quickMask, toggleQuickMask } },
        {
          provide: Commands,
          useValue: {
            spec: (id: string) => CommandTable.find(command => command.id === id),
            tooltip: (id: string) => CommandTable.find(command => command.id === id)?.label ?? ''
          }
        }
      ]
    });
    const fixture = TestBed.createComponent(ToolStrip);
    fixture.detectChanges();
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const button = (id: string) => element.querySelector<HTMLButtonElement>(`[data-testid=tool-strip-${id}]`);
    return { fixture, element, tool, button, quickMask, toggleQuickMask };
  }

  it('has a button for every tool, each a command of the table, and presses the one in hand', () => {
    const { element, button } = strip();
    expect(element.querySelectorAll('button')).toHaveLength(PaintTools.length + 1);
    for (const { command } of PaintTools) {
      expect(CommandTable.some(spec => spec.id === command)).toBe(true);
    }

    expect(button('brush')?.getAttribute('aria-pressed')).toBe('true');
    expect(button('fill')?.getAttribute('aria-label')).toBe('Polygon Fill');
  });

  it('follows the tool chosen elsewhere, and chooses the one clicked', () => {
    const { fixture, element, tool, button } = strip();
    tool.set('smudge');
    fixture.detectChanges();
    expect(button('smudge')?.getAttribute('aria-pressed')).toBe('true');
    expect(button('brush')?.getAttribute('aria-pressed')).not.toBe('true');
    expect(element.getAttribute('data-tool')).toBe('smudge');

    button('clone')?.click();
    expect(tool()).toBe('clone');
  });

  it('chooses nothing with no stack open', () => {
    const { button } = strip(null);
    expect(button('brush')?.disabled).toBe(true);
  });

  it('turns the quick mask on, then applies it, and presses its button while it is on', () => {
    const { fixture, button, toggleQuickMask } = strip();
    expect(button('quick-mask')?.getAttribute('aria-label')).toBe('Quick Mask');
    expect(button('quick-mask')?.getAttribute('aria-pressed')).not.toBe('true');

    button('quick-mask')?.click();
    fixture.detectChanges();
    expect(toggleQuickMask).toHaveBeenCalledTimes(1);
    expect(button('quick-mask')?.getAttribute('data-quick-mask')).toBe('paint');
    expect(button('quick-mask')?.getAttribute('aria-pressed')).toBe('true');

    button('quick-mask')?.click();
    fixture.detectChanges();
    expect(button('quick-mask')?.getAttribute('data-quick-mask')).toBe('keep');
    expect(button('quick-mask')?.getAttribute('aria-pressed')).toBe('true');
  });
});
