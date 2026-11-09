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
    TestBed.configureTestingModule({
      providers: [
        { provide: PaintStore, useValue: { tool, state: signal(state) } },
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
    return { fixture, element, tool, button };
  }

  it('has a button for every tool, each a command of the table, and presses the one in hand', () => {
    const { element, button } = strip();
    expect(element.querySelectorAll('button')).toHaveLength(PaintTools.length);
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
});
