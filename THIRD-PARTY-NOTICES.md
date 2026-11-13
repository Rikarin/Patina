# Third-party notices

Patina is licensed under GPL-3.0-or-later (see [LICENSE](LICENSE)). This file lists third-party code that ships with it
and is not simply a package-manager dependency, with the licence each is used under, and it is checked for GPLv3
compatibility as each one arrives (plan §11). NuGet and npm packages are listed by their package managers' lock files.

| Component | Used in | Version | Licence used | Source |
|---|---|---|---|---|
| xUI | `editor/` (npm packages `@xui/*`) | see `editor/pnpm-lock.yaml` | Apache-2.0 | https://github.com/rikarin/xui |
| SQLite (native `e_sqlite3`) | `Patina.Library`, through `Microsoft.Data.Sqlite` and SQLitePCLRaw | the SQLitePCLRaw that `Microsoft.Data.Sqlite` in `engine/Directory.Packages.props` brings | Public domain | https://sqlite.org |
| HarfBuzz (native `libHarfBuzzSharp`) | `patina-worker` and `patina`, through `Vixen.TextureGraph.Drawing` → `Vixen.Ui.Text` and the NuGet package `HarfBuzzSharp`, to shape a Text node's string | see `external/Vixen/Directory.Packages.props` | MIT ("Old MIT") | https://github.com/harfbuzz/harfbuzz |
| Open Sans (`OpenSans-Regular.ttf`, embedded in `Vixen.TextureGraph.Drawing.dll`) | The face a Text node draws with when it names none | as in `external/Vixen/Editor/Vixen.Editor.App/Fonts` | OFL-1.1, whose text is `Fonts/OFL.txt` there and ships in Vixen's `NOTICE` | https://github.com/googlefonts/opensans |
| MoltenVK (native `libMoltenVK.dylib`, macOS package only) | `patina-worker`, which loads it as its Vulkan implementation (ADR 0010) | 1.4.1, pinned by SHA-256 in `editor/apps/desktop/scripts/publish-engine.mjs` | Apache-2.0, whose text ships beside it as `MoltenVK-LICENSE.txt` | https://github.com/KhronosGroup/MoltenVK |
| Assimp (native `libassimp`, `Assimp64.dll`) | `patina-worker`, through `Vixen.Interchange` and the NuGet package `Ultz.Native.Assimp` 6.0.2 | 5.4.1 on Linux and macOS, 6.0.2 on Windows | BSD-3-Clause, below | https://github.com/assimp/assimp |

Assimp links zlib in statically (zlib licence), with the other libraries its own source tree credits. BSD-3-Clause is compatible
with GPLv3 and requires the text below in a binary distribution, so this file ships in the app's resources.

HarfBuzz's MIT licence is compatible with GPLv3. The OFL lets a font be bundled with software under any licence as long as
the font is not sold on its own and keeps its licence, which is what embedding the face in a library is.

MoltenVK's Apache-2.0 is compatible with GPLv3, which may include Apache-2.0 code but not the other way round. Apache-2.0 asks a
binary distribution for the licence's text, which ships beside the dylib, and for any `NOTICE` file, which MoltenVK's release
archive does not have. Patina ships the release's dylib unchanged, so there are no changes to state. MoltenVK builds in parts of
its `External` dependencies: SPIRV-Cross (Apache-2.0) and Vulkan-Headers (Apache-2.0 or MIT) are under the same terms, and cereal
(BSD-3-Clause) asks for its notice in a binary distribution. That list comes from MoltenVK's documentation and was not checked
against the 1.4.1 binary on a Mac: before a release, compare it with `ExternalRevisions` at the `v1.4.1` tag, and add cereal's
notice here if it is in the dylib.

Vixen (Apache-2.0, through `external/Vixen`) ships in the Worker. These are expected and must be listed here, with their licences
confirmed, when they ship: an EXR library.

Vixen's and xUI's licence and `NOTICE` texts ship with the app.

## Assimp

Open Asset Import Library (assimp). Copyright (c) the assimp team. All rights reserved.

Redistribution and use of this software in source and binary forms, with or without modification, are permitted provided that
the following conditions are met:

- Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
- Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer
  in the documentation and/or other materials provided with the distribution.
- Neither the name of the assimp team, nor the names of its contributors may be used to endorse or promote products derived from
  this software without specific prior written permission of the assimp team.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT
NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL
THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE
OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
