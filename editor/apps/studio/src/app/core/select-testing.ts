import type { ComponentFixture } from '@angular/core/testing';

/**
 * Chooses in an xUI select as a person does: opens it by its button, then clicks the choice it lists by name. The
 * list is an overlay, so the choice is found in the document rather than under the select.
 */
export async function chooseIn(
  fixture: ComponentFixture<unknown>,
  root: HTMLElement,
  testId: string,
  name: string
): Promise<void> {
  const trigger = root.querySelector<HTMLElement>(`[data-testid="${testId}"] button`);
  if (!trigger) {
    throw new Error(`There is no select ${testId}.`);
  }

  trigger.click();
  fixture.detectChanges();
  await fixture.whenStable();
  const options = [...document.querySelectorAll<HTMLElement>('[role="option"]')];
  const option = options.find(candidate => candidate.textContent.trim() === name);
  if (!option) {
    throw new Error(`${testId} lists no ${name}: ${options.map(candidate => candidate.textContent.trim()).join(', ')}`);
  }

  option.click();
  fixture.detectChanges();
  await fixture.whenStable();
}
