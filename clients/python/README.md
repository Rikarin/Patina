# Python client

`patina_client` is generated from `engine/src/Patina.Server/openapi.json` by `generate.py`, with the generator pinned by hash in
`requirements.txt`. Do not edit it. `generate.py --check` is what CI runs, and it fails on drift.

`tests/test_blender_session.py` is the proof of plan §8: a session shaped like a Blender add-on's, with no Blender and nothing of
the editor. It attaches through the discovery file, sends a mesh, applies a material, bakes, changes a parameter and receives the
updated texture, and a build returns paths. It starts a real Server and Worker, so it needs an engine build. A step that needs a GPU
skips where the Worker has no Vulkan.

```sh
python3 -m venv .venv && .venv/bin/pip install --require-hashes -r requirements.txt
.venv/bin/python generate.py
(cd ../../engine && dotnet build Patina.slnx -c Release)
.venv/bin/python -m pytest
```

`PATINA_CONFIGURATION=Debug` runs it against a Debug build. A client finds a running Server through the per-user discovery file.
Clients that talk to the API over HTTP are separate programs and need not be GPL. To change a pin, edit `requirements.in` and run
the `pip-compile` line at its top.

## Packages and parameters

`test_a_graph_publishes_as_a_package_that_every_shelf_offers` is ADR 0027 from a client: describe a graph with a `setInfo`
command, save it, ask `get_package_plan` what publishing would bring, then `publish_package`. The package is a folder in the
library's `packages/`, and `get_materials` lists it with the origin `package`, its parameters and its description.

A build script needs no Server for the same thing:

```sh
patina package projects/kit/Rust.vxtexgraph packages/          # gathers its compounds and pictures
patina graph info packages/Rust.ptpkg                          # inputs, outputs, parameters and presets, as JSON
patina bake packages/Rust.ptpkg out/ --preset Heavy --param tint=0.3 --seed 7 --size 2048
```
