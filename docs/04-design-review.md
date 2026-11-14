# 04. Design review of the densest panes

The review #13 asked for: the app driven in the bezel skin, dark, at 1600 × 1000, through what an artist meets first. A
texturing project on a mesh with a fill layer chosen, then the Bake dialog, Export, Preferences and a graph project. Each
finding says what was wrong, and whether it is fixed or still open, so the next review starts from here.

## Fixed in wave 16

| Where | What was wrong | Now |
|---|---|---|
| Properties, Document | The channel depth was a segmented control of three two-word labels, so in the pane's width each wrapped onto two lines: "8-" over "bit". | A list, which is one line at any width and is changed seldom. |
| Texture Sets | The pane started about 100 px tall: under the set's row and Isolate parts, the parts list was cut off by the splitter. | It starts half again as tall, so the first part shows. A layout a person saved keeps its own size. |
| Viewport | Every setting a painter changes stroke by stroke was in Properties, below the document, so painting meant looking away from the strokes (#201, #23). | A bar over the viewport with size, flow, opacity and color, or the fill's and the picker's own choices. As the viewport narrows, the tool's name goes first, then flow and opacity; size and color stay. |
| Export, Preferences | The skin draws a default button in the accent, as it draws a primary one. Export's Choose… and Preview sat beside Export looking like three primary actions, and Keyboard Shortcuts… looked like Preferences' main action. | Ghost buttons, as the Bake dialog's Choose… already were: one accent button per dialog, the one that runs it. |
| Bake | Height read "Select…" while Follow each set's shape had it switched off, which asks for a choice that cannot be made. | It reads "Each set's own", and "Same as width" when it can be chosen. |

## Still open

- **Properties is still tall.** With the Document section open, a fill layer's settings start below the fold at 1000 px, and
  its mask lower still. The sections fold (#201), but a new person does not know that. Direction: fold Document by default
  once the set has been set up, or give the document its own tab beside Properties.
- **Default buttons in the accent.** 63 buttons across the app use xUI's default variant, which bezel draws like a primary
  one, so most dialogs still have several accent buttons. The fix belongs in the skin, not in each button: see the xUI
  problem in wave 16's report.
- **The color picker's trigger** shows the hex text, about 90 px of a bar where a swatch would do. That is xUI's trigger.
- **Layer rows** repeat a blend list and an opacity slider on every row; at five layers the list is mostly controls. A
  painting tool shows them for the chosen layer only. Worth trying once layers can be renamed in place.
- **The graph's empty state** is a dotted canvas with no hint that Space adds a node.
