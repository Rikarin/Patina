# ADR 0051: The undo history of a texturing document is kept in the cache at a save, and taken up again at an open

**Status:** proposed, 2026-11-16, for issue #209. Nothing of it is built yet.

ADR 0035 gave a document named snapshots and left one thing out: the undo history still ends when the document
closes. This is the storage design that issue asked for before any of it is built. It covers texturing documents
(`.vxlayers`), whose history is the expensive one; a graph's history is commands over a text document and follows the
same shape with no canvases.

## What a step is today, and why it cannot be written down as it is

`StackEditor` keeps each undo step as Vixen `IAuthoringCommand` objects, which hold references into the live document,
and the `StackJournalEntry` records of the strokes the step made. The commands cannot be written to a file and read
back: they are closures over objects that do not exist in the next session. The journal entries can: they are a
stroke's first command and its stamps, which is all a Worker needs to paint it again (ADR 0009). The canvases are
the Worker's, and the Server only ever holds the files the session opened (the checkpoint `base`) and the newer
checkpoints the Worker wrote.

## Decisions

- **A kept step is the document's text after it, its label and its strokes.** At a save the Server writes each step
  that stands and each that is undone: its label, the document as text after the step, and its journal entries, stamps
  and all. Going back over a kept step replaces the whole document with the text before it, as one command; the
  history list shows the same labels as before the close. A stack's document is kilobytes of YAML, so a text per step
  costs less than a diff format would cost to write and test.
- **The canvases are the base and the saved files.** A kept history holds the paint files as they were when it began,
  which is the session's checkpoint `base` (ADR 0009) moved out of the session's folder. The saved files themselves are
  the canvases after every step that stands, so an open needs no replay: the Worker loads the files as it does now, and
  only an undo past them starts from the kept base and stamps the strokes that still stand, as a lost Worker does. No
  new message on the Worker's wire is needed; `stack.load` already takes a checkpoint folder and the files strokes
  created.
- **It lives in the cache**, under `.ptcache/history/`, one folder per document named by its path below the library. A
  history is a convenience over files that are already whole, so deleting `.ptcache` loses the undo past the open and
  nothing a file holds, which is the rule and ADR 0009's precedent for the undo past a save after a lost Worker. The
  cache budget (#189) may take a history as it takes bakes: a document opens with no history then.
- **It is kept only while the files are the ones it was kept with.** The kept history records the SHA-256 of the
  document and of each paint file it names as they were saved. An open that finds any of them different (an edit in
  another program, a Git checkout, a snapshot restored, a sync from another machine) starts with an empty history and
  deletes the kept one, since its strokes would be painted over pictures they were not made on.
- **What is not saved is not kept.** Steps after the last save are not in the files, so they are not in the history; a
  close that discards changes keeps the history of the last save. Undone steps at the save are kept and can be redone
  after an open.
- **It is bounded.** At most 256 steps or 65,536 stamps are kept, whichever comes first. Beyond that the oldest steps
  are folded into the base: the Worker writes a checkpoint of the canvases as they stand after the steps that go, which
  becomes the new base, as `StackEvaluator`'s checkpoints already do every 2,048 stamps.
- **A person can let it go.** Clear History, in the History panel, deletes the kept history of the document in front
  and empties its undo list; the files do not change.

## What stays open

- Whether a kept history should travel with the library instead (beside the document, like snapshots, so a Git clone
  carries it). It would make every save write stamp lists and canvases into the repository, which is why this proposes
  the cache; the owner may decide otherwise.
- Whether the bound is per document or per library.
- Layered materials (`.ptmat`) and graphs, whose histories have no canvases, can take the same files without a base; they
  are not part of the first step.

## How it would be tested

- A stroke, a save, a close and an open: undo takes the stroke away, the pixels' digest equals a document that never
  had it, and redo puts it back to the saved digest.
- A file changed between the close and the open starts with no history, and the kept one is gone.
- A deleted `.ptcache` opens the document whole with no history.
- More steps than the bound: the oldest go, an undo to the new oldest gives the digest the document had then.
