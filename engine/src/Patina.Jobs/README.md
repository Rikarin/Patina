The job queue the Server owns (plan §3.1). Each job is one JSON file, replaced atomically, so a killed Server loses at most the
last progress report. On start, a job that was running goes back in the queue: the Worker that had it is gone or will be told
nothing, and a bake holds nothing that cannot be run again. `BakeCache` holds what jobs made, a folder per build key. Batch rebuilds
and impact analysis arrive here in P6. `BuildPlan` works out what a build would do from build keys, writing nothing, so a preview and a resumed build are the same act (ADR 0011).

A bake's key reads each input for what the bake uses of it, where that is less than the bytes (#151). A bake is one job per
texture set, and a set's maps are cast from its own low faces, so the low mesh is keyed by the geometry of that set's faces
(`BakeKeyReads` in Patina.Export): positions, faces, corner normals and coordinates, smoothing and part names, and not its
materials. A change to one set's faces rebakes that set, and the others come back from the cache. The high meshes and the cage
stay keyed by their bytes, because any ray may strike any high face and occlusion reaches across the model, so a change to either
rebakes every set: the full rebake is the answer whenever it is unclear. The one exception is a set whose rays match parts by name
and which bakes nothing that reaches past its parts: its key reads only the high parts it matches, their box and every part's name
(`BakeHighReads`, and ADR 0008 for the table of which options reach past). A set is not split further. Every map of it rests on the
same ray, so its maps are made together; and occlusion rays and the gutter both cross any region of the atlas a change could be
said to have, so no region can be called untouched without baking it again. A bake delivered from the cache names the mesh as it
is now, so it is not reported out of date.
