# ADR 0015: The API carries labels and codes, and each client words them

**Status:** accepted, 2026-11-08, for issues #198 and #197.

## What was wrong

The engine's own words reached people. Lists showed `ColourDodge`, `Generators/Dirt`, `ao` and `baseColor`, because the
vocabulary was a list of enum names and the editor kept a table of labels that was always a step behind it. Refusals were
written for whoever read the Server's code: "close it with discard=true", "Bind one first", "No Worker is connected". The
editor showed them as they came, and Blender would have had to as well.

## The model

- **A word goes with its label.** `GET /api/v1/stacks/vocabulary` sends every list as `{ value, label }`: blend modes,
  mask and fill sources, projections, generators, mesh maps, alphas and channels. A setting with choices sends `labels` in
  the order of `accepted`. Files and commands hold the value, and a client shows the label. `Terms` on the Server is the one
  table. A word with no entry splits into capitalised words, so a new one is never shown as code, and a stack says some
  words its own way: `Copy` is Normal there, while a graph's blend node still copies.
- **A refusal has a code.** A ProblemDetails a person might meet carries `code` beside its `detail`, and `values` naming
  what the detail was written from: `stroke-not-paint-layer` with the layer and its kind. `Refusals` lists the codes. The
  detail stays plain and neutral, for any reader of the API. A client says a code it knows in its own words and shows the
  detail for one it doesn't, so new codes need no client release, and new words need no Server release.
- **A compiler diagnostic is read, not decoded.** The editor shows the first sentence of a diagnostic. Its code (`TG0002`)
  and the rest of its reasoning sit behind Details, for whoever reports it.
- **One way to show a number.** A share is a percentage with %, an angle is in degrees, and a material's own value, such as
  roughness, stays 0 to 1 with two places, as the graph and an exported map hold it. Every slider has its number beside
  it. `core/numbers.ts` in the editor is the one place that writes them.

## What it costs

A client keeps its own words for the codes it explains, and these drift from the Server's details unless a test holds a
code to its meaning: the Server's tests check the code and values, and the editor's check the words. A label is English.
When the editor is translated, it maps values and codes itself, and the labels are a fallback.
