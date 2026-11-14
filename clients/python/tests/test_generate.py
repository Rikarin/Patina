"""generate.py swaps a fresh client in only once it is whole: a generator that fails leaves the committed one."""
import pathlib

import pytest

import generate


def test_a_failed_generation_leaves_the_client_as_it_was(tmp_path: pathlib.Path) -> None:
    target = tmp_path / "patina_client"
    target.mkdir()
    (target / "client.py").write_text("committed")

    def fail(into: pathlib.Path) -> None:
        into.mkdir()
        (into / "client.py").write_text("half")
        raise RuntimeError("openapi_python_client is not installed")

    with pytest.raises(RuntimeError):
        generate.replace(target, fail)

    assert (target / "client.py").read_text() == "committed"
    assert [p.name for p in tmp_path.iterdir()] == ["patina_client"]


def test_a_whole_generation_replaces_the_client(tmp_path: pathlib.Path) -> None:
    target = tmp_path / "patina_client"
    target.mkdir()
    (target / "gone.py").write_text("old")

    def make(into: pathlib.Path) -> None:
        into.mkdir()
        (into / "client.py").write_text("fresh")

    generate.replace(target, make)

    assert [p.name for p in target.iterdir()] == ["client.py"]
    assert [p.name for p in tmp_path.iterdir()] == ["patina_client"]
