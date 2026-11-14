"""The scripts in examples/ run as they are documented, against a real Server and Worker: a package sent as a zip comes
in, and a project bakes with the settings it keeps (#185, #186)."""
import importlib
import pathlib
import subprocess
import sys

from conftest import dll

from patina_client import AuthenticatedClient
from patina_client.api.library import create_project, get_project_contents, set_project_bake
from patina_client.models import ProjectBakeRequest, ProjectCreateRequest

EXAMPLES = pathlib.Path(__file__).resolve().parents[1] / "examples"
sys.path.insert(0, str(EXAMPLES))


def client_of(session) -> AuthenticatedClient:
    url, token = session.attach()
    return AuthenticatedClient(base_url=url, token=token, timeout=None)


def test_every_example_reads_only_what_the_generated_client_has() -> None:
    for script in EXAMPLES.glob("*.py"):
        importlib.import_module(script.stem)


def test_a_zipped_package_comes_in_through_the_example(session, monkeypatch, capsys) -> None:
    monkeypatch.setenv("PATINA_DISCOVERY", str(session.discovery))
    sent = session.folder / "sent"
    graph = session.library / "projects" / "graphs" / "Rust.vxtexgraph"
    patina = dll("src/Patina.Cli", "patina")
    subprocess.run(["dotnet", patina, "package", str(graph), str(sent), "--zip"], check=True, stdout=subprocess.DEVNULL)

    assert importlib.import_module("import_package").main(str(session.library), str(sent / "Rust.zip")) == 0
    assert (session.library / "packages" / "Rust.ptpkg" / "package.yaml").exists()
    assert "Rust" in capsys.readouterr().out


def test_a_project_bakes_with_its_own_settings_through_the_example(session, monkeypatch, capsys) -> None:
    monkeypatch.setenv("PATINA_DISCOVERY", str(session.discovery))
    client = client_of(session)
    create_project.sync(
        client=client,
        body=ProjectCreateRequest(
            name="Housing",
            folder=None,
            tags=None,
            mesh=str(session.meshes / "housing_low.glb"),
            resolution=256,
            kind="texturing",
            image=None,
            high=str(session.meshes / "housing_high.glb"),
        ),
    )
    bake = get_project_contents.sync(client=client, path="housing").bake
    bake.options.resolution = 32
    bake.options.maps = ["normal"]
    set_project_bake.sync(client=client, body=ProjectBakeRequest(project="housing", bake=bake))

    assert importlib.import_module("bake_project").main(str(session.library), "housing") == 0
    baked = [pathlib.Path(line) for line in capsys.readouterr().out.splitlines()]
    assert baked and all(path.exists() for path in baked)
    assert all((session.library / "projects" / "housing" / "bakes" / "maps") in path.parents for path in baked)
