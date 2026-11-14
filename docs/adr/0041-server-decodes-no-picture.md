# ADR 0041: The Server decodes no picture; the Worker decodes and writes what the Server keeps

**Status:** accepted, 2026-11-14, for issue #293.

Plan §3.1 puts image codecs in the Worker beside the mesh parsers, and ADR 0039 moved the mesh reads there. Pictures
still decoded in the Server in three places: a library picture's preview and the check before one is brought in
(`PicturePreviews`, through `stb_image`, TIFF, WebP, EXR and PNG decoders), a bake's ID colours (`IdColours`), and a
compression preview (`ExportPreviews.Make`, which decodes a build's maps and reads back the file it encodes). The first
parses any file a person points at; a crafted or truncated PSD that crashes `stb_image` took the Server, and every open
session, with it.

## Decisions

- **Three requests, each beside the Worker's queue.** `picture.read` decodes a picture whole and, when asked, writes the
  PNG a client is shown where the Server says, in the library's cache. `bake.id-colours` lists an ID map's colours.
  `export.compress` makes a compression preview the Server planned. None uses the device or keeps anything, so each is
  answered beside the queue (`IpcConnection.Aside`), never behind an evaluation. The wire is version 21.
- **The Server plans, the Worker decodes.** A compression preview is planned by the Server (`ExportPreviews.Plan`: the
  build's keys, the preset's text, the chosen set's objects and the id), which reads no picture, and answered from the
  cache when it was made before. The Worker runs it (`ExportPreviews.Run`) into the same folder. `patina` runs both in
  its own process, as it runs a build.
- **The Server keeps what is derived.** The cache of PNGs and the marks of pictures that read stay the Server's, as
  before (#269): the Worker writes the file it was told to, and the Server marks it read.
- **A picture that took the Worker down is not asked about again** while the Server runs and its bytes are the same: it
  is refused as unreadable, saying it stopped the engine. Asked again, it would take the next Worker down too. This is
  kept in memory, so a restarted Server asks once more.
- **No Worker, no picture.** A picture waits 30 seconds for a Worker to come, then is refused with `renderer-missing`.
  A PNG or a JPEG shown at its own size is still served as its file, with no decoding.
- **The guard reads the IL.** `MeshReadGuardTests.Nothing_the_Server_runs_decodes_a_picture` walks what the Server can
  reach (as ADR 0039's guard does) and fails on any call to Vixen's TIFF, WebP and `stb_image` readers, its decoder
  registry, or the decoding half of its EXR and PNG codecs. Encoding stays allowed: the Server writes PNGs of numbers it
  holds, such as a viewport screenshot, which parses no file anybody made.

## Not yet

- Material files the Server reads itself (MaterialX and USD's text form, ADR 0030) are text, not pictures, and stay.
