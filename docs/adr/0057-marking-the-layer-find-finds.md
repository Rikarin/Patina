# ADR 0057: The layer Find finds is marked on the model as a picture the Server names and the Worker draws

**Status:** proposed, 2026-11-16, for issue #136. Nothing of it is built.

Find Layer Under Cursor (#136) answers with a layer's id, and the client selects it. What is left is to show on the model
where that layer is, as pointing at a texture set or a part already marks it (wave 12: an orange wash with an outline
where the marked faces end, `PUT /viewport/highlight {parts, materials}`). A part or a material is a set of faces, which
the Server can name and the Worker can draw from the mesh alone. A layer is not: where it lies is a picture, its weight at
each texel of the set's map, and only the Worker can make it. So the shape is not the highlight's, and this proposes one.

## Proposed

- **The Server owns the mark, as a name.** `PUT /viewport/highlight` takes a third, optional field, `layer`:
  `{stack, layer}`, the stack session and the layer's id, beside `parts` and `materials`. The viewport's state reports it,
  and it is part of what the Server tells every Worker with the display (`ViewportDisplaySet.Highlight`), so a new Worker
  draws it again. A client that finds a layer sets it; choosing another layer, or none, replaces it. It clears when the
  stack session closes, when the layer is deleted (the Server knows both), and when the viewport stops wearing the stack.
- **The Worker makes the picture, never the Server.** When the display names a layer, the Worker weighs that layer as
  Find does (`StackHost.LayerAt`'s candidate: alone, or in the picture when its mask reads what is beneath), at the
  set's size, and keeps the largest weight over the channels it writes as one grey map. No pixels cross the socket; the
  map lives on the device as a live map does.
- **It is drawn as a wash, weighted.** The viewport's forward shader gains one map, the mark, sampled on the stack's UVs
  and mixed in the same orange the part highlight uses, by its weight, with an outline where the weight crosses one
  half. Clay and the other views show it too; a capture and an export never do, as the quick mask's red does not.
- **It follows edits lazily.** The mark is weighed again when the stack's revision moves and the mark is still set, after
  the evaluation, on the device's turn, at no more than one weighing in flight; a stroke does not wait for it. A paint
  layer is weighed by its strokes (wave 6), so painting it grows the mark.
- **Wire.** `ViewportHighlight` and its IPC twin gain the layer; no new message is needed, since the display set already
  carries the highlight and the Worker already holds the stack session. The Worker's version moves once.

## Considered

- **Marking the faces the layer touches**, so the existing part highlight draws it. Cheaper, but a layer rarely covers
  whole faces: a mask over half a crate would mark all of it, which is the wrong answer.
- **Sending the weight picture to the client** and drawing it in the 2D view only. It crosses JSON or the socket as
  pixels (against plan §3), and the viewport, where Find is used, would show nothing.
- **A one-shot flash** with no state. The Server would own nothing to tell a new Worker, a frame drawn after a crash
  would lose it, and nothing could clear it on purpose.

## Open

- Whether the mark should stay set while painting the found layer, or clear on the first stroke, is a product choice.
- A folder found as one layer (flattened) is weighed as one; whether its children are marked apart is left for later.
