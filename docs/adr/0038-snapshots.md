# ADR 0038: A snapshot is a list beside the document and bytes kept by revision, not a Git commit

**Status:** accepted, 2026-11-13, for issue #209.

A snapshot is a named checkpoint of one document: its file and every paint file it names, as they are saved.

- **The list** is `.snapshots/<file>.yaml` beside the document: a schema number, then each snapshot, oldest first, with its
  id, name, time, each file by its name beside the document and the revision of its bytes, and the paint the document
  named that did not exist yet. Text, appended to, so a diff of it reads as "a snapshot was taken".
- **The bytes** are kept under the library's `.revisions/`, by revision, beside the bytes a pinned reference keeps. A
  paint file that did not change between two snapshots is kept once, so a snapshot of a large document after a small edit
  costs the edit.
- **Both are files of the library**, not cache: deleting `.ptcache` loses no snapshot, and both travel with a Git clone.
  The scan passes both by, since their folders are hidden.
- **Restore** needs the document closed (`snapshot-document-open`), since an open session would write over what it puts
  back. It first takes a snapshot of how the document stood, named "Before restoring …", then writes each file back in
  one step, and removes paint the snapshot says the document did not have yet, so strokes on a layer painted since do not
  stay behind. Every file is checked before anything is written: a snapshot whose bytes are gone is refused and changes
  nothing.
- **Taking one** is of the file as saved. A document open with unsaved edits is refused (`snapshot-unsaved`); the editor
  saves first, so the person gets what they see.

## Why not a Git commit

A library is normally a Git repository, and a commit from inside the app was the issue's suggestion. It is not what a
snapshot is:

- a library need not be a repository, and Patina does not ship or require Git;
- a commit is of the whole working tree, and would carry other documents' and other people's changes with it, or need a
  partial stage that a person did not ask for;
- restoring one document from a commit is a checkout of some paths, which fights the person's own branch and stash.

A snapshot is per document and works the same with or without Git. A person who wants commits still has them, and the
snapshot list is plain text in them.

## Not yet

- The undo history still ends when a document closes. Keeping it across a close needs the stroke journal kept beside
  the document and replayed on open, which is a change to the Worker's journal, not to snapshots.
- Nothing forgets old bytes under `.revisions/`: a deleted snapshot keeps them, since a pin or another snapshot may share
  them. A library check that finds bytes nothing names is the place for that.
