"""Bakes a project's mesh maps with the settings it keeps, as the editor's Quick Bake does (#186).

    python examples/bake_project.py <library> <project>

The Server works out the requests from the project's own file (`GET /library/project/bake/plan`), so a script sends
exactly what the Bake dialog would, and the maps land where the project's documents read them. With no Server running,
`patina bake-maps --project <folder>` does the same in one process.
"""
import sys
import time

from patina_attach import attach

from patina_client.api.jobs import get_job, start_bake
from patina_client.api.library import get_project_bake_plan
from patina_client.models import ProblemDetails

OVER = ("done", "failed", "cancelled")


def main(library: str, project: str) -> int:
    client = attach(library)
    plan = get_project_bake_plan.sync(client=client, project=project)
    if isinstance(plan, ProblemDetails) or plan is None:
        print(f"Nothing to bake: {plan.detail if plan else 'no answer'}", file=sys.stderr)
        return 1

    failed = 0
    for request in plan.requests:
        job = start_bake.sync(client=client, body=request)
        while job.state not in OVER:
            time.sleep(0.25)
            job = get_job.sync(id=job.id, client=client)
            print(f"\r{request.output}: {job.progress:4.0%}", end="", file=sys.stderr)
        print(file=sys.stderr)
        if job.state != "done":
            failed += 1
            print(f"{request.output}: {job.state}, {job.error}", file=sys.stderr)
            continue
        for baked in job.result.maps:
            print(baked.path)
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(*sys.argv[1:3]))
