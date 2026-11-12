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
    save_graph,
    set_graph_selection,
    write_graph_image,
)
from patina_client.api.jobs import get_job, start_bake
from patina_client.api.library import (
    create_graph,
    create_project,
    get_graph_templates,
    get_materials,
    get_package_plan,
    get_project_contents,
    publish_package,
    set_project_settings,
)
from patina_client.api.stacks import apply_stack_commands, export_stack, get_stack, open_stack
from patina_client.api.system import get_system
from patina_client.api.viewport import (
    follow_graph_in_viewport,
    get_viewport,
    set_viewport_display,
    set_viewport_mesh,
    show_bake_in_viewport,
    show_graph_in_viewport,
)
from patina_client.models import (
    BakeColourOptions,
    BakeHeightOptions,
    BakeIdOptions,
    BakeOcclusionOptions,
    BakeOptions,
    BakeRayOptions,
    BakeRequest,
    BakeRoundingOptions,
    BakeThicknessOptions,
    BuildRequest,
    GraphCommandBatch,
    GraphImageRequest,
    GraphOpenRequest,
    GraphSelection,
    GraphCommandGraphSetInfo,
    GraphCommandGraphSetInfoOp,
    GraphCommandGraphSetValue,
    GraphCommandGraphSetValueOp,
    GraphCreateRequest,
    PackageRequest,
    ProblemDetails,
    ProjectCreateRequest,
    ProjectSettingsRequest,
    StackBrush,
    StackCommandBatch,
    StackCommandStackAddLayer,
    StackCommandStackAddLayerOp,
    StackCommandStackSetLayer,
    StackCommandStackSetLayerOp,
    StackCommandStackStroke,
    StackCommandStackStrokeOp,
    StackExportRequest,
    StackMask,
    StackOpenRequest,
    StackStamp,
    ViewportBakeRequest,
    ViewportFollow,
    ViewportGraphRequest,
    ViewportMeshRequest,
    ViewportNodeShown,
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


def shown_node(client, node: int) -> None:
    """Waits for the viewport to show a node alone, which the Server lays on the mesh by itself."""
    deadline = time.monotonic() + DEADLINE
    while time.monotonic() < deadline:
        state = get_viewport.sync(client=client)
        if isinstance(state.node, ViewportNodeShown) and state.node.node == node:
            return
        time.sleep(0.05)
    raise TimeoutError(f"The viewport never showed node {node}.")


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

    # Follow the graph's selection: the Server keeps the chosen node on the mesh, and nothing of the editor is needed.
    chosen = set_graph_selection.sync(id=graph.id, client=client, body=GraphSelection(nodes=[HSL_NODE], pinned=None))
    assert chosen.nodes == [HSL_NODE]
    following = follow_graph_in_viewport.sync(client=client, body=ViewportFollow(graph=graph.id, node=True))
    assert following.following.graph == graph.id
    shown_node(client, HSL_NODE)
    follow_graph_in_viewport.sync(client=client, body=ViewportFollow(graph=None, node=False))

    # Bake: paths in, paths out.
    bake = start_bake.sync(
        client=client,
        body=BakeRequest(
            high=[str(high)],
            low=str(low),
            output=str(session.output),
            options=BakeOptions(
                resolution=64,
                format_="png16",
                supersampling=1,
                gutter=4,
                maps=["normal", "height", "ao"],
                space="tangent",
                rays=BakeRayOptions(
                    front=0.05,
                    rear=0.05,
                    units="relative",
                    ignore_backfaces=False,
                    skew=0.0,
                    skew_map="",
                    distance_map="",
                    match_by_name=False,
                    low_suffix="_low",
                    high_suffix="_high",
                ),
                height=BakeHeightOptions(range_="measured", scale=0.05),
                occlusion=BakeOcclusionOptions(
                    rays=8,
                    distance=0.5,
                    spread=90.0,
                    falloff="none",
                    distribution="cosine",
                    minimum=0.0,
                    ignore_backfaces=False,
                    within_part=False,
                    ground_plane=False,
                    ground_offset=0.0,
                ),
                thickness=BakeThicknessOptions(normalised=True),
                id=BakeIdOptions(source="auto", palette="random"),
                rounding=BakeRoundingOptions(enabled=False, width=0.01, edges="angle", angle=30.0),
                colour=BakeColourOptions(source="auto", texture=""),
            ),
        ),
    )
    baked = until_over(client, bake.id)
    assert baked.state == "done", baked.error
    assert baked.result.maps
    for map_file in baked.result.maps:
        assert pathlib.Path(map_file.path).is_file()

    # One map of the bake alone on the mesh, through the picture view.
    alone = show_bake_in_viewport.sync(client=client, body=ViewportBakeRequest(job=bake.id, map_="height"))
    assert alone.display.view == "picture"

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


def test_a_graph_publishes_as_a_package_that_every_shelf_offers(session) -> None:
    """Publishing needs no GPU: the Server gathers what the graph reads and compiles it, and the files are the result."""
    client = attach(session)
    source = "projects/graphs/Rust.vxtexgraph"

    # Say what the graph is about, then save it, since a package is made of the graph as it is saved.
    graph = open_graph.sync(client=client, body=GraphOpenRequest(path=source))
    apply_graph_commands.sync(
        id=graph.id,
        client=client,
        body=GraphCommandBatch(
            commands=[
                GraphCommandGraphSetInfo(
                    op=GraphCommandGraphSetInfoOp.SETINFO,
                    description="Rust for a Blender add-on.",
                    author="Conformance",
                    tags=["metal", "rust"],
                    physical_width=100.0,
                    physical_height=100.0,
                )
            ]
        ),
    )
    assert get_graph.sync(id=graph.id, client=client).info.tags == ["metal", "rust"]
    save_graph.sync(id=graph.id, client=client)

    plan = get_package_plan.sync(client=client, graph=source)
    assert plan.path == "packages/Rust.ptpkg"
    assert all(isinstance(dependency.problem, (Unset, type(None))) for dependency in plan.dependencies)
    published = publish_package.sync(client=client, body=PackageRequest(graph=source, role="material"))
    assert published.path == plan.path
    folder = session.library / published.path
    assert (folder / "package.yaml").is_file()
    assert (folder / published.manifest.graph).is_file()

    shelf = get_materials.sync(client=client, search="metal")
    offered = [material for material in shelf.items if material.origin == "package"]
    assert [material.name for material in offered] == ["Rust"]
    assert offered[0].description == "Rust for a Blender add-on."


def test_a_document_on_a_mesh_from_an_add_on(session) -> None:
    """A texturing project made, its mesh reloaded where nothing moved, a selection of two colors, an outline filled,
    and a mask written alone in a format: every field the requests have, through the generated client."""
    client = attach(session)
    worker_connected(client)
    low = session.meshes / "housing_low.glb"

    # A new graph from a template the Server offers.
    templates = get_graph_templates.sync(client=client)
    assert templates
    created = create_graph.sync(
        client=client,
        body=GraphCreateRequest(name="Outputs", project=None, resolution=512, template=templates[-1].value),
    )
    assert created.path.endswith(".vxtexgraph")

    # The viewport's lens.
    display = get_viewport.sync(client=client).display
    display.depth_of_field = 0.4
    display.focus = -0.2
    lens = set_viewport_display.sync(client=client, body=display).display
    assert (lens.depth_of_field, lens.focus) == (0.4, -0.2)

    # A texturing project, and its mesh reloaded on the same layout, which moves nothing and keeps documents open.
    create_project.sync(
        client=client,
        body=ProjectCreateRequest(
            name="Housing",
            folder=None,
            tags=None,
            mesh=str(low),
            resolution=256,
            kind="texturing",
            image=None,
            high=None,
        ),
    )
    contents = get_project_contents.sync(client=client, path="housing")
    document = contents.documents[0]
    stack = open_stack.sync(client=client, body=StackOpenRequest(path=document.path))
    reloaded = set_project_settings.sync(
        client=client,
        body=ProjectSettingsRequest(
            project="housing",
            mesh=str(low),
            high=None,
            auto_unwrap=False,
            preserve_paint=True,
            unwrap=None,
            reassign=None,
            same_layout=True,
        ),
    )
    assert not isinstance(reloaded, ProblemDetails), reloaded
    assert get_stack.sync(id=stack.id, client=client).id == stack.id

    # A fill under a selection of two colors of the ID map, and an outline filled on the paint layer.
    paint = next(layer.id for layer in stack.layers if layer.kind.lower() == "paint")
    apply_stack_commands.sync(
        id=stack.id,
        client=client,
        body=StackCommandBatch(
            commands=[
                StackCommandStackAddLayer(op=StackCommandStackAddLayerOp.ADDLAYER, kind="fill", name="Painted Parts")
            ],
            label=None,
        ),
    )
    fill = get_stack.sync(id=stack.id, client=client).layers[-1].id
    side = 256.0
    corners = [StackStamp(x=x * side, y=y * side, pressure=1.0) for x, y in ((0.1, 0.1), (0.9, 0.1), (0.9, 0.9), (0.1, 0.9))]
    edited = apply_stack_commands.sync(
        id=stack.id,
        client=client,
        body=StackCommandBatch(
            commands=[
                StackCommandStackSetLayer(
                    op=StackCommandStackSetLayerOp.SETLAYER,
                    layer=fill,
                    mask=StackMask(
                        source="Bake",
                        map_="id",
                        colours=[[1.0, 0.0, 0.0], [0.0, 0.0, 1.0]],
                        tolerance=0.05,
                        softness=0.0,
                        inside=1.0,
                        outside=0.0,
                    ),
                ),
                StackCommandStackStroke(
                    op=StackCommandStackStrokeOp.STROKE,
                    stroke="outline",
                    layer=paint,
                    channel="baseColor",
                    brush=StackBrush(radius=1.0),
                    colour=[0.9, 0.1, 0.05, 1.0],
                    stamps=corners,
                    open_=False,
                    mask=False,
                    curve="fill",
                    fill="triangle",
                    outline=True,
                    through=False,
                ),
            ],
            label=None,
        ),
    )
    assert not isinstance(edited, ProblemDetails), edited
    masked = next(layer for layer in get_stack.sync(id=stack.id, client=client).layers if layer.id == fill)
    assert len(masked.mask.colours) == 2

    # The fill's mask alone, as a 16-bit PNG.
    written = export_stack.sync_detailed(
        id=stack.id,
        client=client,
        body=StackExportRequest(
            folder=str(session.output / "mask"),
            layer=fill,
            format_="png16",
            compression="none",
            quality=None,
            mips=False,
            mask=True,
        ),
    )
    if written.status_code.value >= 400:
        needs_gpu(written.content.decode())
        raise AssertionError(written.content.decode())
    assert [pixels.usage for pixels in written.parsed] == ["mask"]
    assert pathlib.Path(written.parsed[0].file).is_file()
