import { CdkVirtualScrollViewport } from '@angular/cdk/scrolling';
import { type AfterViewInit, DestroyRef, Directive, ElementRef, inject } from '@angular/core';

/** The CDK viewport measures itself on window resizes only, and a dock pane changes size without one. */
@Directive({ selector: 'cdk-virtual-scroll-viewport[ptnViewportFit]' })
export class ViewportFit implements AfterViewInit {
  private readonly viewport = inject(CdkVirtualScrollViewport);
  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly destroyRef = inject(DestroyRef);

  ngAfterViewInit(): void {
    const observer = new ResizeObserver(() => {
      this.viewport.checkViewportSize();
    });
    observer.observe(this.element.nativeElement);
    this.destroyRef.onDestroy(() => {
      observer.disconnect();
    });
  }
}
