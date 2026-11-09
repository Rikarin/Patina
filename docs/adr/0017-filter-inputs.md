# ADR 0017: A filter's first image is its subject, and every image says where it reads

**Status:** accepted, 2026-11-09, for issues #220, #113 and #114, and the filters of #115, #116 and #117.

## What was wrong

A filter was "one image in and one image out", and that rule did two wrong things at once. It refused every node with a
second image (Warp, Slope Blur, Non-Uniform Blur, Image Ramp) as a composite whose subject a stack file could not choose,
and it took in every node with that shape, including three that measure their input rather than change it: Histogram,
Histogram Render and Summed Area Table. A filter also read only the channel it wrote, so Height to Normal could not read
height and write normal, and ambient occlusion from height could not write to occlusion. A second image could be fed from
an anchor point through the API, and from nothing else.

## What a filter is

- **A node says when it is not an adjustment.** `[Node(Adjustment = false)]` in Vixen, or a compound's `adjustment: false`
  setting, is carried on `NodeTypeDefinition.Adjustment`. The three statistics nodes say it, because their output describes
  their input; Blend, Mix, Dissolve, Select Input, Tile Sampler and Splatter say it, because their other inputs are what
  they are about, and a layer already composites. Normal Blend says it as a composite of two normal maps (#176). The flag
  is the node's, so the next statistics node is left out by whoever writes it, not by a list in Patina.
- **The image a node changes comes first.** Distance Colour said it was no adjustment while its mask was its first image,
  since a layer would have taken the picture beneath as that shape (#174). Wave 6 put its colour first, so it is a filter
  that pads the picture beneath out from a mask an input names, which is the padding filter. A node that wants to be a
  filter orders its ports so; one whose first image is not a picture of the surface says it is no adjustment.
- **A node whose one image is a mesh map is none either.** Volume Noise, Volume Mask and Volume Gradient read the
  position bake through their one image port, and a stack would hand them the picture beneath instead, so Vixen refuses
  them as filters by name (`ReadsNoPicture`, #179); a fill's graph wires them to a mesh map.
- **Everything else with an image in and a picture out is a filter.** Its first image input is its *subject*, the picture
  it changes. Its picture out is one image, or one per channel named after the channel (`roughness`, `height`): a
  *material filter*, which writes the channels it has an output for and leaves the rest. A material filter is no mask
  step, since a mask is one picture.
- `LayerStackGraph.IsAdjustment` and `IsMaskStep` say so, and the vocabulary's filter and mask-step lists are read from
  them, as they were from the old rule (#112).

## Where each image reads

Each image input of a filter layer, a mask step or a graph fill names its source in `inputs`, by port
(`LayerInputAsset` in a `.vxlayers`, `StackInput` on the wire):

| Source | Reads |
|---|---|
| `Beneath` | the layers beneath this one, in a channel it names, or the channel being written |
| `Anchor` | a named anchor point (#111), in a channel |
| `Generator` | a node or compound that reads no image — a noise, a pattern, a grunge — with its own numbers and settings |
| `Texture` | an imported picture |
| `Bake` | a mesh map |

An input the layer does not name reads, in order: the layers beneath in the channel it is named after, when its name is
one of the set's channels (which is how a material filter reads roughness and height at once); the layers beneath in the
channel being written, when it is the subject; nothing, when the node does without it. Anything else is a slot nobody has
filled: a draft leaves the filter out and says so, and a bake refuses. A mask step's subject is always its mask, and a
mask, being one picture, reads beneath only in a channel it names.

**Reading another channel is a wire, not a copy.** Every layer's place in every channel's walk is recorded, and an input
reading a channel walked later is wired at the end, as an anchor's edge is, so the graph model is what refuses a loop.
That is #114: Height to AO on the occlusion channel with its height read beneath, or a filter that writes roughness from
height.

**A port that measures takes a grey.** Which ports refuse a colour, and which a node can do without, are asked of the
compiler per port (`TextureImageInputs`), so a compound inherits the answer from whatever reads its interface. A picture
is read for its luminance; a generator, a mesh map and a mask for their red, because `Grayscale` reads a one-channel image
as `(r, 0, 0)` and its luminance weights would leave a fifth of it.

## In the API and the editor

`StackFilterKind` lists each filter's images (`inputs`: port, summary, subject, optional, the channel it is named after),
and the vocabulary carries `inputs`, the five sources with labels, and `sources`, the generators an image can read with
their own fields. `StackLayer.inputs`, `StackMaskEffect.inputs` and `StackSetLayer.inputs` are `StackInput` maps; another
filter or graph starts from no inputs, as it starts from its own numbers. Properties shows a slot for each image under a
filter's fields, and on a mask step for each image but the mask. Nothing here knows about Electron.

## Instances

An instance layer (#108) is its source, resolved when the stack compiles, so an instance of a filter reads what the
source's `inputs` name. Its copy owns those inputs and every other dictionary of the source's, so an edit made to one in
place reaches the other only through the file. An input that reads an anchor point reads the source's point in the
source's own set; a source in another texture set that reads one is refused with a warning naming the point, and the
instance shows nothing, since that set's anchor points are not this set's.

## What it does not do

- A generator inside an input is not itself fed: one that reads an image is refused there. A layer's mask generator still
  reads anchor points only.
- An input's numbers are not exposed to a graph's expressions, and its source has no transform of its own.
- `Pixelate`, posterize steps and directional distance are not filters yet; see the issues.
