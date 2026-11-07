"""What a Blender add-on does, with no Blender and nothing of the editor: attach, send a mesh, apply a material, bake,
change a parameter, receive updated textures (plan §8). Every call goes through the generated client, and every result is
a file or a hash. A step that needs a GPU skips where the Worker has no Vulkan, which is how Windows CI runs this."""
import pathlib
import time

import pytest

from patina_client import AuthenticatedClient
from patina_client.api.builds import get_build_projects, get_export_presets, preview_build, start_build
from patina_client.api.graphs import (
    apply_graph_commands,
    get_graph,
    get_preview,
    open_graph,
    write_graph_image,
)
from patina_client.api.jobs import get_job, start_bake
from patina_client.api.system import get_system
from patina_client.api.viewport import get_viewport, set_viewport_mesh, show_graph_in_viewport
from patina_client.models import (
    BakeRequest,
    BuildRequest,
    GraphCommandBatch,
    GraphImageRequest,
    GraphOpenRequest,
    GraphCommandGraphSetValue,
    GraphCommandGraphSetValueOp,
    ViewportGraphRequest,
    ViewportMeshRequest,
)

from patina_client.types import Unset

DEADLINE = 180
HSL_NODE = 4


def attach(session) -> AuthenticatedClient:
    url, token = session.attach()
    return AuthenticatedClient(base_url=url, token=token, timeout=None)


def until_over(client, job_id: str):
    deadline = time.monotonic() + DEADLINE
    while time.monotonic() < deadline:
        job = get_job.sync(id=job_id, client=client)
        if job.state in ("done", "failed", "cancelled"):
            return job
        time.sleep(0.05)
    raise TimeoutError(f"Job {job_id} did not end.")


def worker_connected(client) -> None:
    deadline = time.monotonic() + 60
    while time.monotonic() < deadline:
        if get_system.sync(client=client).worker.connected:
            return
        time.sleep(0.05)
    raise TimeoutError("The Worker never connected.")


def needs_gpu(message: str | None) -> None:
    if message and "Vulkan" in message:
        pytest.skip(message)


def evaluated(client, graph: str, revision: int) -> None:
    """Waits for an evaluation of a revision or later, which runs on its own: every edit asks for one."""
    deadline = time.monotonic() + DEADLINE
    while time.monotonic() < deadline:
        latest = get_graph.sync(id=graph, client=client).evaluation
        if latest is not None and not isinstance(latest, Unset):
            if isinstance(latest.error, str):
                needs_gpu(latest.error)
                raise AssertionError(latest.error)
            if latest.revision >= revision:
                return
        time.sleep(0.05)
    raise TimeoutError("The graph never evaluated.")


def picture(client, graph: str, port: str = "Out") -> tuple[str, bytes]:
    image = write_graph_image.sync(id=graph, client=client, body=GraphImageRequest(node=HSL_NODE, port=port))
    response = get_preview.sync_detailed(hash_=image.hash_, client=client)
    return image.hash_, response.content


def test_a_blender_shaped_session(session) -> None:
    client = attach(session)
    worker_connected(client)

    # Send a mesh: by path, as a client gives the Server a file.
    low = session.meshes / "housing_low.glb"
    high = session.meshes / "housing_high.glb"
    set_viewport_mesh.sync(client=client, body=ViewportMeshRequest(path=str(low)))
    assert get_viewport.sync(client=client).mesh == str(low)

    # Apply a material: a graph from the library, laid over the mesh.
    graph = open_graph.sync(client=client, body=GraphOpenRequest(path="projects/graphs/Rust.vxtexgraph"))
    evaluated(client, graph.id, graph.revision)
    applied = show_graph_in_viewport.sync_detailed(client=client, body=ViewportGraphRequest(graph=graph.id))
    if applied.status_code.value >= 400:
        needs_gpu(applied.content.decode())

    # Bake: paths in, paths out.
    bake = start_bake.sync(
        client=client,
        body=BakeRequest(
            high=str(high),
            low=str(low),
            output=str(session.output),
            resolution=64,
            format_="png16",
            supersampling=1,
            occlusion_rays=8,
            search_radius=0.05,
            occlusion_radius=0.5,
            skew=0.0,
            match_by_name=False,
            gutter=4,
            space="tangent",
        ),
    )
    baked = until_over(client, bake.id)
    assert baked.state == "done", baked.error
    assert baked.result.maps
    for map_file in baked.result.maps:
        assert pathlib.Path(map_file.path).is_file()

    # Change a parameter, and receive the texture it changed.
    before, first = picture(client, graph.id)
    edit = apply_graph_commands.sync(
        id=graph.id,
        client=client,
        body=GraphCommandBatch(commands=[GraphCommandGraphSetValue(node=HSL_NODE, port="Hue", value=[0.25], op=GraphCommandGraphSetValueOp.SETVALUE)]),
    )
    evaluated(client, graph.id, edit.revision)
    after, second = picture(client, graph.id)

    assert before != after
    assert first != second
    assert first[:8] == second[:8] == b"\x89PNG\r\n\x1a\n"


def test_results_are_files_and_a_second_build_does_nothing(session) -> None:
    client = attach(session)
    worker_connected(client)

    presets = get_export_presets.sync(client=client)
    assert {"unreal", "unity-urp", "unity-hdrp", "godot", "vixen"} <= {preset.id for preset in presets.presets}

    # The library holds the baked crate and the Rust graph, which exports too, since a project's documents say what it
    # makes. The graph needs a GPU and the crate does not, so the add-on builds the crate: the project it works on.
    everything = preview_build.sync(client=client, body=BuildRequest(presets=["unreal"]))
    assert everything.rebuilds == everything.projects == 2
    crate = next(project.id for project in everything.items if project.name == "Crate")
    request = BuildRequest(presets=["unreal"], projects=[crate])

    preview = preview_build.sync(client=client, body=request)
    assert preview.rebuilds == 1

    job = start_build.sync(client=client, body=request)
    finished = until_over(client, job.id)
    assert finished.state == "done", finished.error
    assert finished.report.built == 1

    report = get_build_projects.sync(id=job.id, client=client)
    written = [pathlib.Path(file) for project in report.items for done in project.exports for file in done.files]
    assert written and all(path.is_file() for path in written)
    assert any(path.name == "T_Crate_N.png" for path in written)

    again = until_over(client, start_build.sync(client=client, body=request).id)
    assert again.report.projects == 0
