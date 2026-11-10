# Third-party notices

Patina is licensed under GPL-3.0-or-later (see [LICENSE](LICENSE)). This file lists third-party code that ships with it
and is not simply a package-manager dependency, with the licence each is used under, and it is checked for GPLv3
compatibility as each one arrives (plan §11). NuGet and npm packages are listed by their package managers' lock files.

| Component | Used in | Version | Licence used | Source |
|---|---|---|---|---|
| xUI | `editor/` (npm packages `@xui/*`) | see `editor/pnpm-lock.yaml` | Apache-2.0 | https://github.com/rikarin/xui |
| SQLite (native `e_sqlite3`) | `Patina.Library`, through SQLitePCLRaw | see `engine/Directory.Packages.props` | Public domain | https://sqlite.org |
| HarfBuzz (native `libHarfBuzzSharp`) | `patina-worker` and `patina`, through `Vixen.TextureGraph.Drawing` → `Vixen.Ui.Text` and the NuGet package `HarfBuzzSharp`, to shape a Text node's string | see `external/Vixen/Directory.Packages.props` | MIT ("Old MIT") | https://github.com/harfbuzz/harfbuzz |
| Open Sans (`OpenSans-Regular.ttf`, embedded in `Vixen.TextureGraph.Drawing.dll`) | The face a Text node draws with when it names none | as in `external/Vixen/Editor/Vixen.Editor.App/Fonts` | OFL-1.1, whose text is `Fonts/OFL.txt` there and ships in Vixen's `NOTICE` | https://github.com/googlefonts/opensans |
| Assimp (native `libassimp`, `Assimp64.dll`) | `patina-worker`, through `Vixen.Interchange` and the NuGet package `Ultz.Native.Assimp` 6.0.2 | 5.4.1 on Linux and macOS, 6.0.2 on Windows | BSD-3-Clause, below | https://github.com/assimp/assimp |

Assimp links zlib in statically (zlib licence), with the other libraries its own source tree credits. BSD-3-Clause is compatible
with GPLv3 and requires the text below in a binary distribution, so this file ships in the app's resources.

HarfBuzz's MIT licence is compatible with GPLv3. The OFL lets a font be bundled with software under any licence as long as
the font is not sold on its own and keeps its licence, which is what embedding the face in a library is.

Vixen (Apache-2.0, through `external/Vixen`) ships in the Worker. These are expected and must be listed here, with their licences
confirmed, when they ship: MoltenVK and an EXR library.

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
