import { type ElementRef, type Signal, afterRenderEffect, effect, signal } from '@angular/core';

/**
 * How a tool bar fits the room it has: whole labels, then short ones, then its switch as a list, then what it does
 * least often in a menu (#184).
 */
export type BarFit = 'full' | 'short' | 'list' | 'menu';

const Fits: readonly BarFit[] = ['full', 'short', 'list', 'menu'];

/**
 * The fit a bar takes next, from how wide what it holds is and how wide it is. One that runs past its end steps down
 * and keeps the width it needed, so a bar that widens steps back up only once it has that room.
 *
 * ⚠ Measured, never guessed from the root font size: the same labels are wider in one font than another, and a bar
 * set to change at a fixed width ran 31 pixels past its end under Linux's fonts at a width where Windows' fitted.
 */
export function nextFit(fit: BarFit, contentWidth: number, width: number, needs: Map<BarFit, number>): BarFit {
  const at = Fits.indexOf(fit);
  if (contentWidth > width) {
    needs.set(fit, contentWidth);
    return Fits[Math.min(at + 1, Fits.length - 1)] ?? fit;
  }

  const wider = Fits[at - 1];
  return wider !== undefined && width >= (needs.get(wider) ?? 0) ? wider : fit;
}

/**
 * Keeps a bar at the widest fit it has room for, as the bar changes size or what it shows changes (`shown`), checked
 * after each render, when the bar's width and what it holds can be measured.
 */
export function fitBar(bar: Signal<ElementRef<HTMLElement> | undefined>, shown: () => unknown): Signal<BarFit> {
  const fit = signal<BarFit>('full');
  const width = signal(0);
  const needs = new Map<BarFit, number>();
  effect(onCleanup => {
    const element = bar()?.nativeElement;
    if (!element) {
      return;
    }

    const observer = new ResizeObserver(() => {
      width.set(element.clientWidth);
    });
    observer.observe(element);
    onCleanup(() => {
      observer.disconnect();
    });
  });
  afterRenderEffect(() => {
    const element = bar()?.nativeElement;
    const now = fit();
    shown();
    if (element && width() > 0) {
      fit.set(nextFit(now, element.scrollWidth, element.clientWidth, needs));
    }
  });
  return fit.asReadonly();
}
