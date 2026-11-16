import type { SystemInfo } from '@patina/api-client';
import { describe, expect, it } from 'vitest';
import type { ServerState } from '../core/system-status';
import { wireAskedAgain, workerOf } from './uv-wire';

function online(connected: boolean, processId: number): ServerState {
  return { kind: 'online', system: { worker: { connected, processId } } as SystemInfo };
}

describe('the UV layout over the 2D view', () => {
  it('is asked for when it is wanted for another stack or mesh, and let go of when it is not', () => {
    expect(wireAskedAgain('a\nmesh', null, false, 'worker 1', 'worker 1')).toBe(true);
    expect(wireAskedAgain('b\nmesh', 'a\nmesh', false, 'worker 1', 'worker 1')).toBe(true);
    expect(wireAskedAgain(null, 'a\nmesh', false, 'worker 1', 'worker 1')).toBe(true);
    expect(wireAskedAgain('a\nmesh', 'a\nmesh', false, 'worker 2', 'worker 1')).toBe(false);
  });

  it('is asked for again once another Worker answers after it failed, not while the same one does (#309)', () => {
    expect(wireAskedAgain('a\nmesh', 'a\nmesh', true, 'worker 1', 'worker 1')).toBe(false);
    expect(wireAskedAgain('a\nmesh', 'a\nmesh', true, 'none', 'worker 1')).toBe(true);
    expect(wireAskedAgain('a\nmesh', 'a\nmesh', true, 'worker 2', 'none')).toBe(true);
    expect(wireAskedAgain(null, null, true, 'worker 2', 'worker 1')).toBe(false);
  });

  it('knows a Worker by its process, and none while none is connected', () => {
    expect(workerOf(online(true, 7))).toBe('worker 7');
    expect(workerOf(online(false, 7))).toBe('none');
    expect(workerOf({ kind: 'connecting' })).toBe('none');
    expect(workerOf(online(true, 8))).not.toBe(workerOf(online(true, 7)));
  });
});
