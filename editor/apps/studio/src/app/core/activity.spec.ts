import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ApiError, type JobState } from '@patina/api-client';
import { BakeStore } from '../bake/bake-store';
import { Activity, jobTitle } from './activity';

const bake = (overrides: Partial<JobState> = {}): JobState => ({
  id: 'j1',
  key: 'k',
  state: 'running',
  progress: 0.42,
  attempts: 1,
  created: new Date().toISOString(),
  cancelRequested: false,
  bake: { high: ['/m/high.glb'], low: '/m/crate_low.glb', output: '/out', material: 'Body', options: {} as never },
  ...overrides
});

function harness() {
  const jobs = signal<JobState[]>([]);
  const cancel = vi.fn((_id: string) => Promise.resolve());
  TestBed.configureTestingModule({ providers: [{ provide: BakeStore, useValue: { jobs, cancel } }] });
  return { activity: TestBed.inject(Activity), jobs, cancel };
}

describe('Activity', () => {
  it('keeps what it is told, newest first, once each, and counts problems until the panel shows', () => {
    const { activity } = harness();

    activity.report({ tone: 'error', source: 'Layers', text: 'The canvas could not be read.' });
    activity.report({ tone: 'error', source: 'Layers', text: 'The canvas could not be read.' });
    activity.report({ tone: 'done', source: 'Export', text: 'Export of the library finished.' });

    expect(activity.notices().map(notice => notice.text)).toEqual([
      'Export of the library finished.',
      'The canvas could not be read.'
    ]);
    expect(activity.unread()).toBe(1);
    activity.show();
    expect(activity.unread()).toBe(0);
    activity.report({ tone: 'warning', source: 'Graph', text: 'Seen as it happens.' });
    expect(activity.unread()).toBe(0);
  });

  it('hears each new value a pane keeps of what went wrong', () => {
    const { activity } = harness();
    const failure = signal<string | null>(null);
    activity.listen('Library', 'error', failure);
    TestBed.tick();

    failure.set('That name is taken.');
    TestBed.tick();
    failure.set(null);
    TestBed.tick();
    failure.set('That name is taken.');
    TestBed.tick();

    // Said twice, with a dismissal between: the history keeps it once, since the newest notice already says it.
    expect(activity.notices().map(notice => [notice.source, notice.text])).toEqual([['Library', 'That name is taken.']]);
  });

  it('lists the Server’s jobs by what they are, and says how far the work going on is', () => {
    const { activity, jobs } = harness();
    jobs.set([bake(), bake({ id: 'j2', state: 'queued', progress: 0, bake: null, build: {} })]);
    TestBed.tick();

    expect(activity.jobs().map(job => job.title)).toEqual(['Bake of Body', 'Export of the library']);
    expect(activity.readout()).toBe('Export of the library, waiting, and 1 more');
    const unnamed = { high: [], low: '/m/crate_low.glb', output: '/out', material: null, name: null, options: {} as never };
    expect(jobTitle(bake({ bake: unnamed }))).toBe('Bake of crate_low');
  });

  it('tells of a job that ends, once, and not of one long over when first heard of', () => {
    const { activity, jobs } = harness();
    jobs.set([bake(), bake({ id: 'old', state: 'done', created: '2020-01-01T00:00:00Z' })]);
    TestBed.tick();
    expect(activity.notices()).toHaveLength(0);

    jobs.set([bake({ state: 'failed', error: 'No Worker came.' })]);
    TestBed.tick();
    jobs.set([bake({ state: 'failed', error: 'No Worker came.', progress: 0.5 })]);
    TestBed.tick();

    expect(activity.notices().map(notice => [notice.tone, notice.text])).toEqual([
      ['error', 'Bake of Body failed: No Worker came.']
    ]);
    expect(activity.readout()).toBeNull();
  });

  it('stops a job through the Server, and says why it would not', async () => {
    const { activity, cancel } = harness();
    await activity.stop('j1');
    expect(cancel).toHaveBeenCalledWith('j1');

    cancel.mockRejectedValueOnce(new ApiError(404, 'There is no such job.'));
    await activity.stop('j9');
    expect(activity.notices()[0]?.tone).toBe('error');
  });
});
