// Publishes the engine for this machine's OS and CPU into dist/engine, the folder the packaged app carries as
// resources/engine (apps/desktop/src/engine.ts): patina-server and patina-worker, self-contained and ReadyToRun. Not
// NativeAOT: the Worker compiles kernels at run time (plan §3.1). CI runs this on one runner per OS
// (.github/workflows/desktop.yml), since each package carries its OS's runtime.
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { cpSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';

const workspace = resolve(import.meta.dirname, '../../..');
const engine = resolve(workspace, '../engine');
const out = resolve(workspace, 'dist/engine');

// The version Vixen pins for iOS (build/native-dependencies.json). To move it, change both and take the hash from a
// fetch, not from a release page.
const MoltenVK = {
  version: '1.4.1',
  url: 'https://github.com/KhronosGroup/MoltenVK/releases/download/v1.4.1/MoltenVK-macos.tar',
  sha256: '5ea0c259df7ded9a275444820f09cced54d6e5a7c7a31d262de62a5cdb7e15cf'
};

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

if (process.platform === 'darwin') {
  await carryMoltenVK();
}

// What the app will carry reads a mesh with what it carries, on the machine that built it, or there is no package.
execFileSync(
  join(out, process.platform === 'win32' ? 'patina-worker.exe' : 'patina-worker'),
  ['--check-natives', '--packaged'],
  { stdio: 'inherit' }
);

/** A clean Mac has no Vulkan, so the app carries MoltenVK beside the Worker, which loads it directly (ADR 0010). */
async function carryMoltenVK() {
  const cache = resolve(workspace, 'dist/native-cache');
  const archive = join(cache, `MoltenVK-macos-${MoltenVK.version}.tar`);
  mkdirSync(cache, { recursive: true });
  if (!existsSync(archive) || sha256(readFileSync(archive)) !== MoltenVK.sha256) {
    const bytes = await download(MoltenVK.url);
    if (sha256(bytes) !== MoltenVK.sha256) {
      throw new Error(`${MoltenVK.url} is not the archive pinned here: it hashes to ${sha256(bytes)}`);
    }

    writeFileSync(archive, bytes);
  }

  const extracted = join(cache, 'MoltenVK');
  rmSync(extracted, { recursive: true, force: true });
  execFileSync('tar', ['-xf', archive, '-C', cache, 'MoltenVK/LICENSE', 'MoltenVK/MoltenVK/dynamic/dylib/macOS']);
  cpSync(join(extracted, 'MoltenVK/dynamic/dylib/macOS/libMoltenVK.dylib'), join(out, 'libMoltenVK.dylib'));
  cpSync(join(extracted, 'LICENSE'), join(out, 'MoltenVK-LICENSE.txt'));
}

/**
 * A release file's bytes. GitHub's release download links have answered 503 and 504 for whole afternoons while its API
 * served the same file, so each try takes the link and then, given a token, the API, before waiting longer. The caller
 * checks the hash, so where the bytes came from does not matter.
 */
async function download(url) {
  const failures = [];
  for (const wait of [0, 10, 30, 90]) {
    await new Promise(done => setTimeout(done, wait * 1000));
    for (const attempt of [() => fetch(url), () => fromApi(url)]) {
      try {
        const response = await attempt();
        if (response?.ok) {
          return Buffer.from(await response.arrayBuffer());
        }
        if (response) {
          failures.push(`${response.url} answered ${response.status}`);
        }
      } catch (error) {
        failures.push(`${url}: ${error.message}`);
      }
    }
  }
  throw new Error(`Could not download ${url}:\n  ${failures.join('\n  ')}`);
}

/** The same release file through the GitHub API, or nothing without a token or for a link that is not a release's. */
async function fromApi(url) {
  const token = process.env.RELEASE_DOWNLOAD_TOKEN;
  const release = /^https:\/\/github\.com\/([^/]+)\/([^/]+)\/releases\/download\/([^/]+)\/([^/]+)$/.exec(url);
  if (!token || !release) {
    return undefined;
  }

  const [, owner, repo, tag, name] = release;
  const headers = { authorization: `Bearer ${token}`, 'x-github-api-version': '2022-11-28' };
  const listing = await fetch(`https://api.github.com/repos/${owner}/${repo}/releases/tags/${tag}`, { headers });
  if (!listing.ok) {
    return listing;
  }

  const asset = (await listing.json()).assets.find(each => each.name === name);
  if (!asset) {
    throw new Error(`release ${tag} of ${owner}/${repo} has no ${name}`);
  }
  return fetch(asset.url, { headers: { ...headers, accept: 'application/octet-stream' } });
}

function sha256(bytes) {
  return createHash('sha256').update(bytes).digest('hex');
}
