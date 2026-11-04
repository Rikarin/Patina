# Third-party notices

Patina is licensed under GPL-3.0-or-later (see [LICENSE](LICENSE)). This file lists third-party code that ships with it
and is not simply a package-manager dependency, with the licence each is used under, and it is checked for GPLv3
compatibility as each one arrives (plan §11). NuGet and npm packages are listed by their package managers' lock files.

| Component | Used in | Version | Licence used | Source |
|---|---|---|---|---|
| xUI | `editor/` (npm packages `@xui/*`) | see `editor/pnpm-lock.yaml` | Apache-2.0 | https://github.com/rikarin/xui |

No native code ships yet. These are expected and must be listed here, with their licences confirmed, when they do: Vixen
(Apache-2.0, through `external/Vixen`), Assimp, MoltenVK and an EXR library.

Vixen's and xUI's licence and `NOTICE` texts ship with the app.
