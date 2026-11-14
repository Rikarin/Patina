# ADR 0043: A platform is the export preset that writes for it, and a project's sets are checked from their files

**Status:** accepted, 2026-11-14, for issue #159.

Checks before export judged one texture set, the one in front, against one set of limits: the project's budget, size
limit and texel density, with the memory counted as the project's own preset writes the files. A studio shipping to a
console and a phone holds the same sets to two budgets, and wants to know every set's problems before a build, not the
one it happens to have open.

## Decisions

- **A platform is named by its export preset.** The library already says which presets a build writes, one for each
  platform it ships to (`exportTargets`, #207), and the memory a set takes depends on the preset's formats and
  compression. So a project's `checks:` block keeps `platforms:`, each a preset id with a budget, a largest side and a
  texel density of its own. A zero keeps the project's limit, so a platform says only how it differs. There is no
  separate list of platform names to keep in step with the presets.
- **The limits follow the preset measured with.** A check names a preset (`?preset=`), or measures with the project's own
  as before. Either way, when that preset has limits in `platforms:`, they hold; the texel density view follows the
  project's own preset the same way. A preset no library or project has is refused with `preset-missing`.
- **A project is checked from its sets as they stand.** `GET /library/project/checks` judges every texturing document
  and layered material. One open in a session is measured by the Worker, as a single check is. One nobody has open is
  read from its file by the Server, which knows its size, channels, files and bake, and is judged on those alone: its
  density, UVs and values need its maps and its mesh, which only the Worker reads (ADR 0039), and opening every set on
  the GPU to check it would evaluate each one. Such a set says `measured: false`, and the editor says Not measured. A set
  whose Worker fails to measure it is judged the same way rather than failing the whole project.

## Costs

- An unopened set's density and value problems are not found until it is opened. A project check that opened each set
  in turn would find them, at the cost of evaluating every set on the GPU; nothing stops a later client doing that
  through the existing routes.
- A platform's limits are keyed by a preset id, so renaming a preset's file leaves its limits behind, as it leaves a
  project's chosen preset; the limits are kept and ignored until a preset of that id comes back.
