// Publishes the engine for this machine's OS and CPU into dist/engine, the folder the packaged app carries as
// resources/engine (apps/desktop/src/engine.ts): patina-server and patina-worker, self-contained and ReadyToRun. Not
// NativeAOT: the Worker compiles kernels at run time (plan §3.1). CI runs this on one runner per OS
// (.github/workflows/desktop.yml), since each package carries its OS's runtime.
import { execFileSync } from 'node:child_process';
import { readdirSync, rmSync } from 'node:fs';
import { join, resolve } from 'node:path';

const workspace = resolve(import.meta.dirname, '../../..');
const engine = resolve(workspace, '../engine');
const out = resolve(workspace, 'dist/engine');

const rids = {
  'darwin-arm64': 'osx-arm64',
  'darwin-x64': 'osx-x64',
  'linux-x64': 'linux-x64',
  'linux-arm64': 'linux-arm64',
  'win32-x64': 'win-x64',
  'win32-arm64': 'win-arm64'
};
const rid = rids[`${process.platform}-${process.arch}`];
if (!rid) {
  throw new Error(`No .NET runtime identifier for ${process.platform}-${process.arch}`);
}

rmSync(out, { recursive: true, force: true });
console.log(`Publishing the engine for ${rid}`);
for (const project of ['src/Patina.Server', 'src/Patina.Worker']) {
  execFileSync(
    'dotnet',
    [
      'publish',
      project,
      '-c',
      'Release',
      '-r',
      rid,
      '--self-contained',
      '-p:PublishReadyToRun=true',
      '-p:DebugType=none',
      '-o',
      out
    ],
    { cwd: engine, stdio: 'inherit' }
  );
}

// Debug symbols and API docs stay out of the app, and so does Assimp 6: Ultz.Native.Assimp carries two majors on Linux
// and macOS, and the binding opens the 5 (ADR 0010).
for (const entry of readdirSync(out)) {
  if (/\.(pdb|dbg|dSYM|xml)$/.test(entry) || /^libassimp\.(so\.6|6\.dylib)$/.test(entry)) {
    rmSync(join(out, entry), { recursive: true, force: true });
  }
}

// What the app will carry reads a mesh with what it carries, on the machine that built it, or there is no package.
execFileSync(join(out, process.platform === 'win32' ? 'patina-worker.exe' : 'patina-worker'), ['--check-natives'], {
  stdio: 'inherit'
});
