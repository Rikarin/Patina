# ADR 0052: A project template is a project file with nothing in it but settings

**Status:** accepted, 2026-11-16, for issue #144.

New Project could not start from anything but the defaults, so a studio that exports to one engine, bakes at one
size and holds its sets to one budget set the same blocks by hand on every project. Painter calls its answer a
template; this is Patina's.

## Decisions

- **A template holds a project's settings and nothing that is the project's work.** It keeps Auto Unwrap, Preserve
  Paint and the unwrap options, and the export, bake, viewport, checks and paint blocks of `project.yaml`. It never
  keeps a mesh, a document, a reference, an output or the project's tags: those are what a project is made of, and a
  template that carried documents would be a copy of a project, which Duplicate already is.
- **It is a `project.yaml` with only those blocks, in the library's `project-templates` folder.** One file per
  template, named after it as a project's folder is named (`Hero Prop` is `project-templates/hero-prop.yaml`), read and
  written by the same codec as a project's own file, so a template can never hold a block a project cannot. Files are
  the truth: no index lists templates, and a library copied to another machine brings them.
- **Made from a project, by name.** Save as Template, in Project Settings, keeps the project's settings as they were
  last applied; a template of the same name is replaced. There is no editor of its own: a template is changed by
  changing a project and saving it again.
- **A block the template does not hold leaves the new project's own.** A project made at 1024 bakes at 1024 until
  someone says otherwise (#144); a template that was never told how to bake leaves that, and one that was replaces it,
  and the person's default bake with it. Auto Unwrap and Preserve Paint are always the template's, since a project
  always has them.
- **A name nobody kept refuses the project before anything is made**, with the 404 any missing file gives, so a typo
  leaves no half-made project behind.

## Left open

Channels a texturing document starts with and its shader are the library's (`library.yaml`), not a project's, so a
template does not carry them; that would need the document kinds to take a channel list at creation. Cameras imported
from the mesh are the other part of #144 and need the Vixen model reader to read a file's cameras first.
