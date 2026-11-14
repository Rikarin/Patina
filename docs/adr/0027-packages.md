# ADR 0027: A package is a folder of text that evaluates where it is put

**Status:** accepted, 2026-11-10. Decides the package format #41 and #185 asked for, and what a graph says about itself
(#181, #185). Builds on [ADR 0007](0007-graph-authoring-references.md), whose lookups a package is laid out for, and
[ADR 0012](0012-documents.md), whose shelf offers it.

## Decision

A package is a folder named `<name>.ptpkg`:

```
Rust.ptpkg/
  package.yaml                              what it carries, and which Patina wrote it
  Rust.vxtexgraph                           the graph, parameters, presets and description included
  Compounds/Packages/Rust/Wear.vxtexgraph   each compound of its project it reads, under the package's name
  files/a372ce12f9f6dc50.png                each picture it reads, named by the first 16 hex of its SHA-256
```

`package.yaml` is keyed as the API names a `PackageManifest`:

```yaml
format: 1
name: Rust
graph: Rust.vxtexgraph
role: material
writer: Patina 0.1.0-dev
compounds:
  - type: Packages/Rust/Wear
    from: Wear
shipped:
  - Grunges/Speckle
files:
  - path: files/a372ce12f9f6dc50.png
    from: mask.png
```

- **`format`** is the version stamp. A reader refuses a newer one with a sentence (`LibraryFormatException`, newer
  schema), and `writer` says which Patina published it. Nothing else is versioned: the graph file carries Vixen's own
  `version`.
- **The manifest repeats nothing the graph holds.** Parameters, their descriptions, presets, outputs, the description,
  author, tags and physical size are in the graph's file, where they were authored. Two lists that must agree are how
  one of them goes stale.
- **A package evaluates wherever the folder is put.** ADR 0007 reads compounds from `Compounds` beside a graph and
  pictures beside it, then from the library, so a package is laid out for exactly those lookups and needs no resolver of
  its own: `patina bake Rust.ptpkg out/` works on a machine with no library, and so does `vixen texture bake` on the
  graph inside. Every picture a package's graph or its compounds name is rewritten to `files/…`, which is beside the
  graph, because a compound's pictures are found from the graph holding it.
- **A package's compounds live under `Packages/<name>/`**, and every node that named one is rewritten to that type. Two
  packages each carrying a `Wear`, or a package and the project using it, then never share one by accident.
- **A shipped compound is named, not carried.** `shipped` lists the compounds of Vixen's and Patina's own that it reads;
  a Patina without one says `TG0025` on the node, as for any graph.
- **Publishing is refused** when a picture cannot be found, a node type is published by nothing, a Text or Vector node
  reads a font or an SVG file (a package cannot carry those yet), or the graph does not compile. `GET
/library/packages/plan` lists each of these before anything is written, so a dialog can show them; `POST
/library/packages` answers `package-refused` with the reason.
- **The same graph publishes to the same bytes.** Files are content-named and lists are sorted, so republishing an
  unchanged material is no diff, and a package is written into a folder beside it first and moved into place, so a
  reader never sees half of one.

A library keeps its packages in `packages/` beside `projects/`. Every project's shelf lists them as their own origin,
`package`, called Packages. Using one copies its graph into the project as a compound, as any material is (ADR 0012), and
brings its compounds into the project's `Compounds/Packages/<name>/` with their pictures named from the library; a newer
package replaces them. Nothing indexes `packages/`: the folders are the truth, and the scanner never walks them.

## What a graph says about itself

Vixen's `GraphInfo` (on Vixen `patina/fix-graph-params`) is a description, an author, tags and the physical size one tile
stands for, in centimetres, written as four flat fields of the graph's file. No compiler reads it, so `setInfo` is one
undo step that a Worker never hears of. `GraphState.info` and a delta's `info` carry it.

**Tags come from two places, on purpose.** A project's tags for a document (ADR 0018) are the library's words for it, kept
in `project.yaml`, which is what a search of other projects can afford to read. A graph's own tags travel with the file.
Publishing a package merges the project's tags into the package's graph, so they travel too. The shelf matches both for
what it reads anyway (the starters, the packages and this project's documents); for another project's graphs it matches
the project's tags only, because reading every graph of ten thousand projects on each keystroke is not affordable. That is
the one place a word can find a material in one project and not in another, and it is written here so that it is a choice.

## Why not a compiled format

#185 asked for "a compiled format … holding parameters only". That was weighed and refused:

- **A plan is a compiled artefact of one Vixen build.** Doc 48 says so: its promise is about the graph. A package of
  plans would stop evaluating at the next kernel change, could not be diffed or merged, and could not be checked by a
  person before it is used.
- **Hiding the graph is not possible with an open evaluator.** Whatever evaluates a package has to read what it computes;
  a format that pretends otherwise sells a promise nobody can keep.
- **What "parameters only" buys a user is the interface, and that holds.** The shelf offers a package by its parameters,
  presets, outputs and description, the Inspector shows only those, and nothing in the app opens a package's graph for
  editing. Someone who wants the graph can read the folder, as they could read any other format with an open evaluator.

So the format is the graph and what it reads, made self-contained, and stamped.

## `patina` with no Server

- `patina package <graph> <folder> [--library <path>] [--role …] [--zip]` publishes a package anywhere, as a folder or
  as one zip.
- `patina package import <zip | folder> (--library <path> | --into <folder>)` brings one in, as Import Package… does.
- `patina graph info <graph | package>` prints a `GraphFacts` as JSON: inputs, outputs, parameters, presets, settings,
  what the graph says about itself and the manifest.
- `patina bake <graph | package> <folder> [--seed <n>] [--preset <name>] [--param <name>=<value>]…` sets a parameter's
  value as its default before compiling, which is what a graph evaluated on its own reads. A name the graph does not
  declare, or a value its parameter cannot hold, is refused with exit 2 before a GPU is opened.
- Every command takes `--help`, and `patina help` lists them.

## Costs and open questions

- **Fonts and SVG files are not carried.** A Text or Vector node with a file of its own refuses publishing.
- **A package is a folder, not one file.** That is what diffs in Git; sending one means zipping it, which
  `patina package --zip` does: the folder under `<name>.ptpkg/`, its entries sorted and stamped alike, so the same package
  zips to the same bytes. Import Package… and `POST /library/packages/import` (#185) take such a zip, or a manifest at the
  zip's top, or a `.ptpkg` folder. Nothing is written until the whole package has been unpacked beside `packages/` and its
  manifest read (a newer `format` is refused as above) and every file it names found; an entry that is rooted or climbs
  out of the package, or a zip that unpacks to more than 4 GiB, brings nothing in. The package then replaces one of its
  name whole.
- **Relink reaches the graph's own Bitmap nodes.** The plan's `in` says which compound names a picture, or none for the
  graph itself; `POST /graphs/{id}/relink` repoints every Bitmap of an open graph that reads one picture, as one undo
  step, naming the new file as ADR 0007 finds it. A picture only a compound reads is relinked by opening that compound.
- **Stale compounds of a package that was removed stay in a project** that used it, as any project compound does.
- **Graph-file tags of another project's graph are not searched** from this project, as above.
