import { describe, expect, it } from 'vitest';
import { QuitTrace } from './quit-trace';

describe('QuitTrace', () => {
  it('times each step from the first, under one prefix the e2e tests look for', () => {
    const lines: string[] = [];
    let now = 1_000;
    const trace = new QuitTrace(
      line => lines.push(line),
      () => now
    );

    trace.step('asked the studio about unsaved edits');
    now += 250.4;
    trace.step('the studio answered: quit');
    now += 3_000;
    trace.step('the sidecars stopped');

    expect(lines).toEqual([
      'Patina quit: asked the studio about unsaved edits (+0 ms)',
      'Patina quit: the studio answered: quit (+250 ms)',
      'Patina quit: the sidecars stopped (+3250 ms)'
    ]);
  });
});
