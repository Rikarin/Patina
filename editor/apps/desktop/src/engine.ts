import { app } from 'electron';
import { existsSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { dotnetHost } from './dotnet';

export interface Command {
  command: string;
  args: string[];
  /** What the process needs in its environment beyond the app's own, such as where its runtime is. */
  env?: NodeJS.ProcessEnv;
}

/**
 * Where patina-worker and patina-server are. PATINA_ENGINE_DIR overrides; a packaged app carries them in
 * resources/engine; a development checkout uses the Release builds under engine/ (run through `dotnet`). Outside a
 * package each is told where the engine's runtime is (#249), which a self-contained package does not need.
 */
function locate(name: 'patina-worker' | 'patina-server'): Command {
  const native = process.platform === 'win32' ? `${name}.exe` : name;
  // Never in a package: an environment variable must not choose what a signed app runs.
  const override = app.isPackaged ? undefined : process.env['PATINA_ENGINE_DIR'];
  const packaged = app.isPackaged ? join(process.resourcesPath, 'engine') : null;
  const host = app.isPackaged ? { command: 'dotnet', env: {} } : dotnetHost();
  const throughDotnet = (dll: string): Command => ({ command: host.command, args: [dll], env: host.env });
  for (const dir of [override, packaged].filter((d): d is string => !!d)) {
    if (existsSync(join(dir, native))) {
      return { command: join(dir, native), args: [], env: host.env };
    }

    if (existsSync(join(dir, `${name}.dll`))) {
      return throughDotnet(join(dir, `${name}.dll`));
    }
  }

  const project = name === 'patina-worker' ? 'Patina.Worker' : 'Patina.Server';
  const checkout = resolve(__dirname, '../../../../engine/src', project, 'bin/Release/net11.0', `${name}.dll`);
  if (existsSync(checkout)) {
    return throughDotnet(checkout);
  }

  throw new Error(
    `${name} not found. Build the engine (cd engine && dotnet build -c Release) or set PATINA_ENGINE_DIR.`
  );
}

export const engine = {
  worker: () => locate('patina-worker'),
  server: () => locate('patina-server')
};
