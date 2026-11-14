# ADR 0043: A texture set keeps its mesh's name as its key, and a person's name for it beside that

**Status:** proposed, 2026-11-14, for issue #298. Nothing is built yet: the open questions below are the owner's.

A texture set is named after the material slot it paints on the mesh, and that name is its key. `TextureSetAsset.Name`
(Vixen) is what an edit names a set by, what `SetSetName` writes when a project's mesh is chosen, what Reload Mesh
matches the mesh's material slots against, what a bake names a set by, and what `{set}` writes in an export's file
names. A mesh exported from a DCC tool often names its slots `M_Body_01` or `lambert3`, and a person wants to call the
set "Body" without the next Reload Mesh losing its layers or a bake missing it. Renaming the key cannot do that: the mesh
still says `lambert3`, so Reload Mesh would read a renamed set as gone and the slot as new.

## Proposal

- **The key stays the slot's name.** `TextureSetAsset.Name` keeps meaning the mesh's material slot, and nothing that
  matches a set against the mesh, a bake or an edit changes.
- **A display name beside it.** `TextureSetAsset.Label` (Vixen, Apache-2.0, empty by default and left out of the file
  when empty) is what a person calls the set. Empty reads as the key. It is set by a `setTextureSetLabel` stack command,
  one undo step, refused by code for a set the document does not have.
- **Shown wherever a set is named in the app.** The Texture Sets pane, Properties' document section, the export's set
  list, the bake's set list and every message that names a set use the label, as a user channel's name is used
  (#141). The API sends both, `name` and `label`, as it sends a channel's usage and its name.
- **The key in anything that matches.** The Python client and the CLI keep naming a set by its key, so a script written
  against the mesh's names keeps working after a rename.

## Open questions (the owner's)

- **`{set}` in an export's file names: the key or the label?** The label is what a person reads, and file names are
  read by people; the key is what a game's import scripts were written against. One option is `{set}` for the label and
  `{setKey}` for the slot's name, which changes what existing presets write for any set given a label.
- **A label unique within a document?** Two sets named alike in the app would be told apart only by the mesh's names.
- **What a renamed slot in the mesh does.** If a later mesh renames `lambert3` to `M_Body`, Reload Mesh reads a set gone
  and a set new, as today. A label could carry the layers over by asking, but that is Reload Mesh's matching, not this.

## Cost

A Vixen field and command, a wire field in `StackState`'s set list and the export's, and the app's places that name a
set reading `label || name`. No migration: a file without a label reads as today.
