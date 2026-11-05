# ADR 0010: Native libraries in a package (spike S8)

**Status:** S8 **failed**, 2026-11-05, measured on the macOS package: Vulkan is not in it. Assimp has one mechanism on every
platform, but no Linux or Windows machine has run it yet: that proof is CI's first run. **Pass criteria (plan §6):** the
Vulkan loader, MoltenVK and ICD inside a signed `.dmg`, the Worker finding them with no Homebrew or SDK, launching on a clean Mac.
If not, change the bundling layout in Vixen's `VulkanLoader` upstream.

## Assimp: the application carries it, and one search finds it

GitHub's `ubuntu-latest` moved to 26.04, which has no `libassimp5`. `Silk.NET.Assimp` brings its native through
`Ultz.Native.Assimp`, but Silk's own loader does not read `runtimes/linux-x64/native`, so a build needed the system's. The report
was worse than the fact: `dotnet publish -r` flattens natives beside the executable, so a publish already held the file, and the
gap was builds without a runtime identifier: `dotnet test`, the dev Worker and the `vixen` tool.

`AssimpLoader` in `Vixen.Interchange` now replaces `Assimp.GetApi()`, the one call that opened it. It searches
`runtimes/<rid>/native`, then beside the executable, through the same `Vixen.Platform.Native` search as `VulkanLoader`, and
last the binding's soname on the machine. So a build, a publish and a package are one code path, with no copy step, which is why
this is not a repeat of Raven's `Directory.Build.targets`: a target in Vixen does not run for a Patina project that only has a
`ProjectReference`, and a run-time search needs none. Every Vixen consumer gets it.

A system Assimp 6 cannot answer. The name is `libassimp.so.5`, never the `libassimp.so` symlink, and a test holds it to the
binding's own name. `publish-engine.mjs` also leaves the package's `libassimp.so.6` and `libassimp.6.dylib` behind.

`patina-worker --check-natives` reports the Vulkan device and reads a triangle, and fails unless the application's own Assimp
answered: a library found on the machine would hide a package without one. It runs after `publish-engine.mjs`, and on the finished
package at the end of `package.mjs` (for Linux, the AppImage, unpacked), so `pnpm dist` cannot produce a package that fails it.

| Place | What shows it | State |
|---|---|---|
| macOS build | `AssimpLoaderTests`, `NativeCheckTests`, sabotaged both ways | Run here |
| macOS package, ad hoc | The packaged Worker, under `sandbox-exec` with `/opt/homebrew` and `/usr/local` denied | Run here |
| Linux build | The same tests with no system Assimp (`no-system-assimp` action) | **First run is CI's** |
| Linux publish | `--check-natives` on the runner, then in an `ubuntu:26.04` container | **First run is CI's** |
| Linux AppImage | `--check-natives` from the unpacked AppImage, in `package.mjs` | **First run is CI's** |
| Windows | `Assimp64.dll` beside `patina-worker.exe` in a cross-publish; run by CI | **First run is CI's** |

No Linux code ran for this ADR: Docker Desktop was not running and was not started. A cross-publish for `linux-x64` puts the
package's `libassimp.so.5`, byte for byte, beside `patina-worker`, and `bin/.../runtimes/linux-x64/native` holds it for a build.
Those are the two paths the search tries. To run the container by hand, publish as `engine.yml` does and:

```sh
docker run --rm -v "$PWD/out:/engine:ro" ubuntu:26.04 /engine/patina-worker --check-natives
```

### What a Linux machine needs

Read from the ELF, since nothing could run:

| Needed | From | Floor |
|---|---|---|
| `libc`, `libm` | `libassimp.so.5` needs `GLIBC_2.33`; the runtime alone would need 2.28 | glibc 2.33 |
| `libstdc++.so.6` | `GLIBCXX_3.4.29`, GCC 11 | Ubuntu 21.04, Debian 12, RHEL 9, Fedora 34 |
| `libgcc_s.so.1` | Both | Any of those |
| zlib | Inside the library, statically (`inflate` and `deflate` are its own exports) | None |

Debian 11, Ubuntu 20.04 and RHEL 8 fall under that floor, and the loader reports the linker's reason rather than "not found".
musl (Alpine) has no build in the package, and there only a system Assimp 5 can answer. The Worker needs no ICU. To draw anything
it needs `libvulkan.so.1` and a driver, which on Linux are the system's and rightly so. Running the AppImage needs FUSE 2,
which recent Ubuntu releases do not install by default; that is the AppImage's cost, not the engine's.

### Why the image stays at 24.04

Patina's jobs keep `ubuntu-24.04`. A pinned image is what stopped the surprise, and the fix removes the need to move, not the
risk of moving. Reaching forward is the job of the `ubuntu:26.04` container step. Move the pin when something wants a newer runner.

Vixen's workflows keep their `libassimp5` lines for now. They are redundant once this lands, because the application's copy
answers first whether or not one is installed, and `AssimpLoaderTests` shows it on Vixen's Linux leg. They should go once this
has run green on Linux once, since no Linux run of it exists yet.

### What changed for meshes, and one thing that was never true

Linux now reads with the package's 5.4.1, as the disassembled `aiGetVersionMinor` and `aiGetVersionPatch` of both Linux
libraries show, rather than Ubuntu's 5.3. It is the build macOS already used.

**The binding's major 5 does not hold on Windows.** The package ships one `Assimp64.dll` there, and its version resource says
**6.0.2**. Windows has been running the binding over a 6 all along, and neither the binding's soname test nor any comment
caught it, since there is no version in that file name. `Vixen.Interchange.Tests` passes 36 of 36 over the macOS 6 with the file
swapped in, so the reader's surface survives it. That is evidence and not a proof that the ABIs match, and a pinned 5 for
Windows would have to come from outside the package.

## Vulkan: S8 fails, on a Mac without Homebrew

The `.dmg` from `pnpm dist` holds no Vulkan loader, no MoltenVK and no ICD manifest. Nothing in Patina's or Vixen's closure brings
them: `Silk.NET.Vulkan` is bindings only, and `build/native-dependencies.json` pins MoltenVK for iOS alone. The one ICD manifest in
the bundle is Electron's SwiftShader, which nothing loads. So `VulkanLoader` falls through to Homebrew.

Measured on the packaged, ad hoc Worker: on this machine `vulkan: Apple M1 Max`, through Homebrew. With `/opt/homebrew` and
`/usr/local` denied by `sandbox-exec`, it says `Vulkan could not be loaded`, and meshes still read. The Worker starts and answers
health on a clean Mac, and every graph, bake and viewport frame fails.

The plan's way out stands, and is not small enough to do here. It needs:

1. `libvulkan.1.dylib` and `libMoltenVK.dylib` for `osx-arm64`, pinned and hashed in Vixen's `native-dependencies.json`, which
   adds a download and two licences to clear. `Build.Publish` already expects those two names.
2. A decision between the loader with an ICD manifest, which then has to be pointed at the app's own folder, and MoltenVK loaded
   directly, which costs the validation layers. `VulkanLoader`'s last resort is the second, at a Homebrew path; the bundled path
   does not exist.
3. Running `--check-natives` under the `sandbox-exec` line above as the gate, so a Mac runner shows it on every package.

### Still unverified

- **A signed and notarised build.** Everything here was ad hoc. The hardened runtime with `entitlements.mac.plist` has never met
  these dylibs, and `disable-library-validation` is what should let them load. Gatekeeper rejects the ad hoc app, as expected.
- **A real clean Mac.** `sandbox-exec` hides two folders; it is a good stand-in and not a clean install.
- **Windows installers and the Vulkan loader.** `vulkan-1.dll` comes from the GPU driver, which holds on any machine with one.
  An NSIS install has not been run.
