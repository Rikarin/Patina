Turns a bake into files: reads the meshes, runs Vixen's `MapBaker` and writes each map at the depth that was asked for. The Worker
runs it as a job, and `patina bake-maps` runs it in-process for a build farm. There is no baking in this assembly (plan §4); only what
is specific to Patina's outputs. Export presets, channel packing and naming templates arrive here in P6.
