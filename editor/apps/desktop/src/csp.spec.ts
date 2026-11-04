import { describe, expect, it } from 'vitest';
import { contentSecurityPolicy } from './csp';

describe('contentSecurityPolicy', () => {
  const directive = (name: string) =>
    contentSecurityPolicy()
      .split('; ')
      .find(d => d.startsWith(`${name} `));

  it('runs only the app’s own scripts', () => {
    expect(directive('script-src')).toBe("script-src 'self'");
  });

  it('reaches the local Server and nowhere else', () => {
    expect(directive('connect-src')).toBe("connect-src 'self' http://127.0.0.1:* ws://127.0.0.1:*");
  });

  it('refuses plugins and framing', () => {
    expect(directive('object-src')).toBe("object-src 'none'");
    expect(directive('frame-ancestors')).toBe("frame-ancestors 'none'");
  });
});
