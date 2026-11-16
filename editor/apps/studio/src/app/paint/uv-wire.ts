import type { ServerState } from '../core/system-status';

/** Which Worker answers now, as the Engine status reports it: its process, or none. */
export function workerOf(state: ServerState): string {
  return state.kind === 'online' && state.system.worker.connected
    ? `worker ${state.system.worker.processId ?? ''}`
    : 'none';
}

/**
 * Whether the UV layout is to be asked for: for another stack or mesh, or again once another Worker answers after
 * the last ask failed (#309). A layout that failed is not asked for again while the same Worker answers, so a refusal
 * stays one, but a Worker that died while it drew it no longer keeps the layout away until it is toggled.
 * @param key The stack and mesh the layout is wanted for, or null when it is not wanted.
 * @param asked What it was last asked for.
 * @param failed Whether that ask failed.
 * @param worker The Worker now, from {@link workerOf}.
 * @param askedWith The Worker when it was last asked.
 */
export function wireAskedAgain(
  key: string | null,
  asked: string | null,
  failed: boolean,
  worker: string,
  askedWith: string
): boolean {
  if (key !== asked) {
    return true;
  }

  return key !== null && failed && worker !== askedWith;
}
