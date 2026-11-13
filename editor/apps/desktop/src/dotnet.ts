import { existsSync, readdirSync } from 'node:fs';
import { homedir } from 'node:os';
import { join } from 'node:path';

/** The runtime the engine's development builds run on: `net11.0`, and the Server is an ASP.NET Core app. */
const Major = 11;
const Frameworks = ['Microsoft.NETCore.App', 'Microsoft.AspNetCore.App'];

/** The `dotnet` that runs a development build of the engine, and what its children need to find it too. */
export interface DotnetHost {
  command: string;
  env: NodeJS.ProcessEnv;
}

/** Where to look and what is there, so a test can say both. */
export interface DotnetPlaces {
  env: NodeJS.ProcessEnv;
  home: string;
  platform: NodeJS.Platform;
  /** Whether a root holds a `dotnet` with the engine's runtimes. */
  has: (root: string) => boolean;
}

/**
 * Whether a .NET install holds the runtimes the engine needs, read from its `shared` folder as `dotnet` itself does.
 */
export function holdsRuntime(root: string): boolean {
  return Frameworks.every(framework => {
    try {
      return readdirSync(join(root, 'shared', framework)).some(version => version.startsWith(`${Major}.`));
    } catch {
      return false;
    }
  });
}

/**
 * The `dotnet` to run a development build with (#249). `DOTNET_ROOT` first, then `~/.dotnet`, where
 * `dotnet-install` and CI's setup put the SDK without putting it first on PATH, and only then whatever `dotnet` is on
 * PATH, which may be an older one with no .NET 11. A root found here is passed on as `DOTNET_ROOT`, so the Server
 * and the Worker it starts find the runtime as well. A packaged app is self-contained and never asks.
 */
export function dotnetHost(places: Partial<DotnetPlaces> = {}): DotnetHost {
  const { env = process.env, home = homedir(), platform = process.platform } = places;
  const executable = platform === 'win32' ? 'dotnet.exe' : 'dotnet';
  const has = places.has ?? ((root: string) => existsSync(join(root, executable)) && holdsRuntime(root));
  for (const root of [env['DOTNET_ROOT'], join(home, '.dotnet')]) {
    if (root && has(root)) {
      return { command: join(root, executable), env: { DOTNET_ROOT: root } };
    }
  }

  return { command: 'dotnet', env: {} };
}
