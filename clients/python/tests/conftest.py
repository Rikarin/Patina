"""Starts a real Server and Worker as processes, from a Release or Debug engine build, the way any client meets them."""
import json
import os
import pathlib
import subprocess
import sys
import tempfile
import time
import uuid

import pytest

ROOT = pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
ENGINE = ROOT.parents[1] / "engine"
CONFIGURATION = os.environ.get("PATINA_CONFIGURATION", "Release")


def dll(folder: str, assembly: str) -> str:
    path = ENGINE / folder / "bin" / CONFIGURATION / "net10.0" / f"{assembly}.dll"
    if not path.exists():
        raise RuntimeError(f"{path} is not built. Run: dotnet build Patina.slnx -c {CONFIGURATION}")
    return str(path)


def run(*arguments: str) -> None:
    subprocess.run(["dotnet", *arguments], check=True, stdout=subprocess.DEVNULL)


class Session:
    """The folder a Blender add-on would know: a library, and the discovery file that finds its Server."""

    def __init__(self, folder: pathlib.Path) -> None:
        self.folder = folder
        self.library = folder / "Game.ptlib"
        self.meshes = folder / "meshes"
        self.output = folder / "maps"
        self.discovery = folder / "servers.json"
        # A Unix domain socket on all three platforms, whose paths are short, which a test's folder is not.
        self.socket = str(pathlib.Path(tempfile.gettempdir()) / f"{uuid.uuid4().hex}.sock")
        self.processes: list[subprocess.Popen] = []

    def start(self, *command: str) -> subprocess.Popen:
        process = subprocess.Popen(
            ["dotnet", *command],
            stdin=subprocess.PIPE,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        self.processes.append(process)
        return process

    def attach(self) -> tuple[str, str]:
        """Reads the discovery file for this library: the one thing a client needs to know."""
        deadline = time.monotonic() + 60
        wanted = os.path.normcase(os.path.realpath(self.library))
        while time.monotonic() < deadline:
            if self.discovery.exists():
                try:
                    found = json.loads(self.discovery.read_text()).get("instances", [])
                except json.JSONDecodeError:
                    found = []  # Being replaced this moment.
                for instance in found:
                    if os.path.normcase(os.path.realpath(instance["library"])) == wanted:
                        return f"http://127.0.0.1:{instance['port']}", instance["token"]
            time.sleep(0.05)
        raise TimeoutError("The Server never appeared in the discovery file.")

    def stop(self) -> None:
        for process in self.processes:
            process.kill()
            process.wait()
            for stream in (process.stdin,):
                if stream:
                    stream.close()


@pytest.fixture
def session(tmp_path: pathlib.Path):
    made = Session(tmp_path)
    bench = dll("tools/Patina.Bench", "patina-bench")
    run(bench, "bake-library", str(made.library))
    run(bench, "bake-pair", str(made.meshes), "0.02")
    run(bench, "graph", str(made.library), "5", "Rust")
    made.start(
        dll("src/Patina.Server", "patina-server"),
        "--library", str(made.library),
        f"--Patina:DiscoveryFile={made.discovery}",
        f"--Patina:Worker:SocketPath={made.socket}",
        "--Patina:WatchStdin=true",
    )
    made.attach()
    made.start(dll("src/Patina.Worker", "patina-worker"), "--socket", made.socket, "--watch-stdin")
    yield made
    made.stop()
