# Python client

A placeholder. This folder will hold a client generated from `engine/src/Patina.Server/openapi.json`, and a conformance
test that drives a Blender-shaped session with no Blender installed: attach, send a mesh, apply a material, bake, change a
parameter and receive updated textures (plan §8).

A client finds a running Server through the per-user discovery file. Clients that talk to the API over HTTP are separate
programs and need not be GPL.
