# ADR 0016: One command table, which the menus, the keys and the tooltips read

**Status:** accepted, 2026-11-08, for issues #188, #191 and #193.

## What was wrong

A command was written down wherever it was used. The native menu held its own labels and keys in the main process, the
Keyboard Shortcuts dialog listed eight paint keys, panes listened for keys of their own (Delete, Cmd+G, F, the arrows,
Space), and tooltips were constants. So a rebound key left the menu and the tooltip wrong, two commands could share a key
in silence, a modifier was dropped (Cmd+X bound X), paint keys fired behind dialogs, and the menus were the same with or
without anything to act on.

## The model

- **One table** (`editor/apps/studio/src/app/core/command-table.ts`) lists every command: its id, its label, its section,
  its keys and where they work (`app`, `stack`, `graph`, `viewport`). The native menu bar is laid out there too, by id. Keys
  a control answers as its kind of control does everywhere (a list's arrows, the graph canvas's Delete) are listed as fixed.
- **Handlers belong to what owns the work.** A store or a pane calls `commands.handle(id, { enabled, run, label, checked })`
  for as long as it lives. `Commands` runs the first handler that would act, for a key, a menu item and a button alike, so
  a menu item is on exactly when its key would do something. A text field's own edits are handlers too, and go first in a
  field.
- **Keys respect where focus is.** A key a text field or a dialog would type or use stays there unless its command says it
  works there (Save does). A key a control already answered is left alone. Two commands share a key only in areas that are
  never live together, a stack's and a graph's; rebinding into a clash is refused with an offer to swap, and a settings
  file that clashes keeps the person's own key and drops the default. Where a control would take a command's key before
  the window sees it (xUI's graph canvas deletes on Delete with any modifier, which Delete and Relink's keys are), the
  pane catches the key ahead of the control and hands it to the table (`Commands.isKeyOf`, then `press`), so the table
  still says what the key is and a rebound one works.
- **The studio says what the menus are.** It sends the menu bar to the main process whenever anything in it changes; the
  main process builds it, adding only what is its own (the app menu, Open Recent, the window's items). The menu takes a
  key itself only when it has the command key, so a plain letter in a menu is shown and still reaches a text field.
  Before the studio has spoken, the main process shows its own items only.
- **A person's keys are a file.** `keys.json` in the app's per-user settings folder holds the preset and the keys that
  differ from it, written whole by the main process. Keys are a person's, so they are not in a library.
- **Undo history is the Server's, as a list.** `GET /history` lists a stack's or a graph's steps and `POST /history` jumps
  to one as a single event, so any client, Blender included, can show and walk it.

## What it costs

A command needs an entry in the table as well as a handler, and a pane that wants a key has to name a command rather
than listen for it. A menu that follows focus is rebuilt as focus moves; the studio sends it only when it changed.
