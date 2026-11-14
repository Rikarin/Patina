"""The engine runs under the `dotnet` that holds .NET 11, not whichever comes first on PATH (#291)."""
import os
import pathlib

from conftest import dotnet_host

EXECUTABLE = "dotnet.exe" if os.name == "nt" else "dotnet"


def install(root: pathlib.Path) -> pathlib.Path:
    root.mkdir(parents=True)
    (root / EXECUTABLE).write_text("")
    for framework in ("Microsoft.NETCore.App", "Microsoft.AspNetCore.App"):
        (root / "shared" / framework / "11.0.0").mkdir(parents=True)
    return root


def test_dotnet_root_comes_before_home_and_is_passed_on(tmp_path: pathlib.Path) -> None:
    chosen = install(tmp_path / "chosen")
    install(tmp_path / "home" / ".dotnet")

    command, env = dotnet_host({"DOTNET_ROOT": str(chosen), "PATH": "elsewhere"}, tmp_path / "home")

    assert command == str(chosen / EXECUTABLE)
    assert env["DOTNET_ROOT"] == str(chosen)
    assert env["PATH"] == "elsewhere"


def test_home_dotnet_is_used_and_named_when_dotnet_root_is_unset(tmp_path: pathlib.Path) -> None:
    home = install(tmp_path / "home" / ".dotnet")

    command, env = dotnet_host({}, tmp_path / "home")

    assert command == str(home / EXECUTABLE)
    assert env["DOTNET_ROOT"] == str(home)


def test_a_root_without_net_11_is_passed_over(tmp_path: pathlib.Path) -> None:
    old = tmp_path / "old"
    old.mkdir()
    (old / EXECUTABLE).write_text("")
    home = install(tmp_path / "home" / ".dotnet")

    command, _ = dotnet_host({"DOTNET_ROOT": str(old)}, tmp_path / "home")

    assert command == str(home / EXECUTABLE)


def test_path_is_the_last_resort(tmp_path: pathlib.Path) -> None:
    command, env = dotnet_host({"DOTNET_ROOT": str(tmp_path / "gone")}, tmp_path / "home")

    assert command == "dotnet"
    assert env["DOTNET_ROOT"] == str(tmp_path / "gone")
