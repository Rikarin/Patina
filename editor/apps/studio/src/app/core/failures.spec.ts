import { ApiError } from '@patina/api-client';
import { messageOf } from './messages';
import { sources } from './source-testing';

/**
 * Files that may read a failure's own text: messages.ts, which falls back to it for a refusal with no words, and the
 * engine status, which keeps the network's sentence for the tooltip under its own words.
 */
const Readers = new Set(['apps/studio/src/app/core/messages.ts', 'apps/studio/src/app/core/system-status.ts']);

describe('failures in the app’s words (#198)', () => {
  it('shows a caught failure through messageOf, never its own message', async () => {
    const raw = /\b(?:error|failure|err|e|reason|cause|thrown|problem)\.message\b|as Error\)\.message/u;
    const found = (await sources())
      .map(({ path, text }) => ({ path: path.replaceAll('\\', '/'), text }))
      .filter(({ path }) => !Readers.has(path))
      .flatMap(({ path, text }) =>
        text
          .split('\n')
          .map((line, index) => ({ line: index + 1, code: line }))
          .filter(({ code }) => raw.test(code))
          .map(({ line }) => `${path}:${String(line)}`)
      );

    expect(found).toEqual([]);
  });

  it('says a refusal with a code in the app’s words, with the file named and not where it is', () => {
    const refusal = new ApiError(422, "'warm.png' is not a .cube file.", [], 'file-kind-wrong', {
      file: 'warm.png',
      wanted: '.cube'
    });

    expect(messageOf(refusal)).toBe('warm.png can’t be used here. Choose a .cube file.');
  });

  it('does not call a missing mesh in the viewport a failure of the engine', () => {
    const refusal = new ApiError(409, 'Open a mesh first: there is nothing to pick.', [], 'viewport-no-mesh');

    expect(messageOf(refusal)).not.toMatch(/engine/iu);
  });
});
