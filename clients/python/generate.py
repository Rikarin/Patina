#!/usr/bin/env python3
"""Generates the client from engine/src/Patina.Server/openapi.json, or checks that the committed one is what that gives.

A folder of its own and a generator in one pinned version, so that a check is a plain directory compare.
"""
import filecmp
import pathlib
import subprocess
import sys
import tempfile

ROOT = pathlib.Path(__file__).resolve().parent
SPEC = ROOT.parents[1] / "engine" / "src" / "Patina.Server" / "openapi.json"
TARGET = ROOT / "patina_client"


def generate(into: pathlib.Path) -> None:
    subprocess.run(
        [
            sys.executable, "-m", "openapi_python_client", "generate",
            "--path", str(SPEC),
            "--config", str(ROOT / "generator.yaml"),
            "--meta", "none",
            "--output-path", str(into),
            "--overwrite",
        ],
        check=True,
        stdout=subprocess.DEVNULL,
    )


def same_text(left: pathlib.Path, right: pathlib.Path) -> bool:
    # The generator writes the platform's line endings, and the repository holds LF on every platform.
    return left.read_bytes().replace(b"\r\n", b"\n") == right.read_bytes().replace(b"\r\n", b"\n")


def differs(left: pathlib.Path, right: pathlib.Path) -> list[str]:
    compare = filecmp.dircmp(left, right, ignore=["__pycache__"])
    found = compare.left_only + compare.right_only
    found += [name for name in compare.common_files if not same_text(left / name, right / name)]
    for name, below in compare.subdirs.items():
        found += [f"{name}/{inner}" for inner in differs(left / name, right / name)]
    return found


def main() -> int:
    if "--check" in sys.argv:
        with tempfile.TemporaryDirectory() as scratch:
            fresh = pathlib.Path(scratch) / "patina_client"
            generate(fresh)
            drift = differs(TARGET, fresh)
        if drift:
            print("clients/python/patina_client is out of date; run generate.py:", *drift, sep="\n  ")
            return 1
        print("patina_client is up to date")
        return 0

    replace(TARGET, generate)
    return 0


def replace(target: pathlib.Path, make) -> None:
    """Makes a fresh copy of `target` beside it and only then swaps it in, so a generator that fails or is missing
    leaves the committed client as it was rather than deleted."""
    with tempfile.TemporaryDirectory(dir=target.parent, prefix=f".{target.name}-") as scratch:
        fresh = pathlib.Path(scratch) / target.name
        make(fresh)
        old = pathlib.Path(scratch) / f"{target.name}.old"
        if target.exists():
            target.rename(old)
        fresh.rename(target)


if __name__ == "__main__":
    raise SystemExit(main())
