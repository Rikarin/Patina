"""Brings a zipped package into a library and lists what the shelf now offers from packages (ADR 0027, #185).

    python examples/import_package.py <library> <package.zip>

The Server reads the whole zip as a package before it writes any of it, and refuses one that is newer than it, names a
file it does not hold, or reaches outside its own folder.
"""
import sys

from patina_attach import attach

from patina_client.api.library import get_materials, import_package
from patina_client.models import PackageImportRequest, ProblemDetails


def main(library: str, package: str) -> int:
    client = attach(library)
    imported = import_package.sync(client=client, body=PackageImportRequest(source=package))
    if isinstance(imported, ProblemDetails) or imported is None:
        print(f"Not brought in: {imported.detail if imported else 'no answer'}", file=sys.stderr)
        return 1

    manifest = imported.manifest
    print(f"{manifest.name} ({manifest.role}, by {manifest.writer}) is in {imported.path}")
    for material in get_materials.sync(client=client, search="packages").items:
        if material.origin == "package":
            knobs = ", ".join(parameter.name for parameter in material.parameters or [])
            print(f"  {material.name}: {material.source} [{knobs}]")
    return 0


if __name__ == "__main__":
    sys.exit(main(*sys.argv[1:3]))
