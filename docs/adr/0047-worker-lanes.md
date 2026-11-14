# ADR 0047: What goes ahead of a Worker's queue never wears a stack, and a frame does not wait for a wear

**Status:** accepted, 2026-11-15, for issue #295.

Wave 15 let every `viewport.*` request go ahead of the Worker's queue (`IpcConnection.Urgent`), so that a frame on a
software GPU waited for the request being answered and not for every evaluation and thumbnail sent before it (#274).
`viewport.stack`, which tells the Worker which stacks the viewport wears, went ahead too. A Server sends a stack's
`stack.load` and `stack.sync` before the viewport wears it, and the wear could reach the Worker first: it wore a stack the
Worker did not hold yet, or fed the viewport the stack as it was before its last change.

## Decisions

- **The Worker's lanes.** A request is answered as it is read (`Immediate`: health and cancels), beside the rest
  (`Aside`: work that touches nothing the queue does), ahead of the queue (`Urgent`), or in its turn. One list,
  `IpcTypes.Urgent`, says what goes ahead: opening the mesh, its maps, the camera, the display settings, a frame, a pick,
  a cast, a marquee and a bake's preview. None of them depends on work queued before it: the maps and the display's
  pictures are files the Server had written before it named them.
- **A wear keeps its place.** `viewport.stack` waits behind the stack requests sent before it, so the Worker wears what
  the Server told it about, in the order it was told. `Wearing_a_stack_waits_for_the_stack_and_a_frame_does_not` holds
  the order with the real list.
- **A frame does not wait for a wear.** On a software GPU the stack requests ahead of a wear take minutes. The Server
  sends the wear and goes on: frames are drawn without the stack until it is worn, claim no stack revision meanwhile (the
  frame header's revision is nought while a wear is unanswered), and the viewport is drawn again once the Worker answers.
  A client that waits for a frame showing a revision therefore waits for the wear too.

## Not yet

- A lane per stack. The Worker could let a wear go ahead of everything but its own stack's requests; the gain is a
  frame showing the stack sooner on a slow GPU, where a frame without it is already drawn.
