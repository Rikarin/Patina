"""Finds the Server a library is open in, through the per-user discovery file, as every client does (plan §8).

A Server writes its port and token to the discovery file when it opens a library, and removes them when it stops. A
client reads the file, picks the entry for its library, and talks HTTP with the token: nothing else is shared.
"""
import json
import os
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))

from patina_client import AuthenticatedClient  # noqa: E402


def discovery_file() -> pathlib.Path:
    """Where the Server keeps it: the per-user local data folder, under Patina, unless PATINA_DISCOVERY names one."""
    if "PATINA_DISCOVERY" in os.environ:
        return pathlib.Path(os.environ["PATINA_DISCOVERY"])
    if sys.platform == "win32":
        base = pathlib.Path(os.environ["LOCALAPPDATA"])
    elif sys.platform == "darwin":
        base = pathlib.Path.home() / "Library" / "Application Support"
    else:
        base = pathlib.Path(os.environ.get("XDG_DATA_HOME", pathlib.Path.home() / ".local" / "share"))
    return base / "Patina" / "servers.json"


def attach(library: str, discovery: pathlib.Path | None = None) -> AuthenticatedClient:
    """A client of the Server that has `library` open. Start one with `patina-server --library <path>` first."""
    wanted = os.path.normcase(os.path.realpath(library))
    found = json.loads((discovery or discovery_file()).read_text()).get("instances", [])
    for instance in found:
        if os.path.normcase(os.path.realpath(instance["library"])) == wanted:
            return AuthenticatedClient(
                base_url=f"http://127.0.0.1:{instance['port']}", token=instance["token"], timeout=None
            )
    raise LookupError(f"No Server has {library} open. Start one with: patina-server --library {library}")
