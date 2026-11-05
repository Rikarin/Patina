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
