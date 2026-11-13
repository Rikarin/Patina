import { mkdirSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { dotnetHost, holdsRuntime } from './dotnet';

describe('dotnetHost', () => {
  const home = join('/home', 'someone');
  const own = join(home, '.dotnet');

  it('runs the SDK in ~/.dotnet when PATH has another, and tells the children where it is', () => {
    const host = dotnetHost({ env: {}, home, platform: 'linux', has: root => root === own });

    expect(host).toEqual({ command: join(own, 'dotnet'), env: { DOTNET_ROOT: own } });
  });

  it('takes DOTNET_ROOT before ~/.dotnet', () => {
    const set = join('/opt', 'dotnet');
    const host = dotnetHost({ env: { DOTNET_ROOT: set }, home, platform: 'win32', has: () => true });

    expect(host).toEqual({ command: join(set, 'dotnet.exe'), env: { DOTNET_ROOT: set } });
  });

  it('passes over a root without .NET 11 and falls back to PATH', () => {
    const host = dotnetHost({ env: { DOTNET_ROOT: join('/opt', 'old') }, home, platform: 'linux', has: () => false });

    expect(host).toEqual({ command: 'dotnet', env: {} });
  });
});

describe('holdsRuntime', () => {
  let root: string;

  beforeEach(() => {
    root = mkdtempSync(join(tmpdir(), 'patina-dotnet-'));
  });

  afterEach(() => rmSync(root, { recursive: true, force: true }));

  const install = (framework: string, version: string) =>
    mkdirSync(join(root, 'shared', framework, version), { recursive: true });

  it('wants both runtimes at .NET 11', () => {
    install('Microsoft.NETCore.App', '11.0.0');
    expect(holdsRuntime(root)).toBe(false);

    install('Microsoft.AspNetCore.App', '10.0.3');
    expect(holdsRuntime(root)).toBe(false);

    install('Microsoft.AspNetCore.App', '11.0.0');
    expect(holdsRuntime(root)).toBe(true);
  });

  it('says no for a folder that is no install', () => {
    expect(holdsRuntime(join(root, 'nothing'))).toBe(false);
  });
});
