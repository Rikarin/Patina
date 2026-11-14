# ADR 0038: What a graph measures about a picture travels as numbers, counted where the picture is

**Status:** accepted, 2026-11-14, for issues #172 and #182.

A client showing a node's picture wants two measurements of it: its histogram, and what the node cost to compute. Both
come from the Worker, which holds the picture and the GPU.

- **A histogram is counted on the GPU at full size.** The Worker appends Vixen's own counting ops
  (`TextureHistograms.Append`, the ones `Analysis/Histogram` is) to the plan the last evaluation ran, so a result cache
  that holds the picture runs only the count. It reads back each channel's table, 1 KB, not the picture. The editor
  counted the 256-pixel reduction before, which hid any detail the reduction averaged away.
- **The answer is numbers in JSON**: `POST /api/v1/graphs/{id}/histogram` gives 256 shares per channel, red, green, blue,
  alpha and Rec. 709 luma. The plan's rule (§3) is that pixels never cross JSON or the socket (ADR 0005); a histogram
  is 1,280 numbers about a picture whatever its size, so it is not a picture and does not go through the cache. A
  client that wants a picture of it can draw one, or add `Analysis/Histogram Render` to the graph.
- **A grey is counted as the picture a person sees.** A one-channel image is shown with its value on red, green and
  blue and an alpha of one, so those are what its histogram says.
- **A node's cost is the GPU time of the operations it was compiled to.** Vixen times each op it runs with a timestamp
  after it (`TexturePlanEvaluator.TimeOps`), and its compiler names the node each op was emitted for, a compound's inner
  ops being the compound's (`TextureGraphCompiler.OpNodes`). The Worker sums them per node into the evaluation's
  `times`; the Server keeps each node's newest time in the state.
- **A result the cache answers keeps the time it was made in.** The result cache is the device's, shared by every graph
  and every shelf thumbnail, so a graph opened after its thumbnail was drawn runs nothing at all. The Worker keeps each
  op's time by the key of the result it made (`TextureBake.KeyOf`) and reports a reused op with that time; a node shows
  its time from the first evaluation, wherever its results were made. Thumbnails are timed for that.
- **A time ranks, it does not add up.** A timestamp records when everything before it finished, so operations that
  overlap on a GPU share their time unevenly. The card's title says so, and the times are not summed anywhere.

## Not yet

- A histogram of a stack's channel, or of a 2D View's mesh map.
- Statistics beyond the histogram: a mean, a minimum and a maximum, which the same tables could give.
