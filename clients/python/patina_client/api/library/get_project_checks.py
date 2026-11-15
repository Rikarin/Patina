from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx2

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.problem_details import ProblemDetails
from ...models.project_checks import ProjectChecks
from ...types import UNSET, Unset
from typing import cast



def _get_kwargs(
    *,
    project: str,
    preset: str | Unset = UNSET,

) -> dict[str, Any]:
    

    

    params: dict[str, Any] = {}

    params["project"] = project

    params["preset"] = preset


    params = {k: v for k, v in params.items() if v is not UNSET and v is not None}


    _kwargs: dict[str, Any] = {
        "method": "get",
        "url": "/api/v1/library/project/checks",
        "params": params,
    }


    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> ProblemDetails | ProjectChecks | None:
    if response.status_code == 200:
        response_200 = ProjectChecks.from_dict(response.json())



        return response_200

    if response.status_code == 400:
        response_400 = ProblemDetails.from_dict(response.json())



        return response_400

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if response.status_code == 409:
        response_409 = ProblemDetails.from_dict(response.json())



        return response_409

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Response[ProblemDetails | ProjectChecks]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    *,
    client: AuthenticatedClient | Client,
    project: str,
    preset: str | Unset = UNSET,

) -> Response[ProblemDetails | ProjectChecks]:
    """ Checks every texture set and layered material of a project at once, as GET /stacks/{id}/checks does
    one. A set open in a session is measured by the GPU engine; one nobody has open is judged on what
    its file says, its limits, its files and its bake, and says it was not measured. The export preset
    of a platform to check for, or none for the project's own.

    Args:
        project (str):
        preset (str | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | ProjectChecks]
     """


    kwargs = _get_kwargs(
        project=project,
preset=preset,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    *,
    client: AuthenticatedClient | Client,
    project: str,
    preset: str | Unset = UNSET,

) -> ProblemDetails | ProjectChecks | None:
    """ Checks every texture set and layered material of a project at once, as GET /stacks/{id}/checks does
    one. A set open in a session is measured by the GPU engine; one nobody has open is judged on what
    its file says, its limits, its files and its bake, and says it was not measured. The export preset
    of a platform to check for, or none for the project's own.

    Args:
        project (str):
        preset (str | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | ProjectChecks
     """


    return sync_detailed(
        client=client,
project=project,
preset=preset,

    ).parsed

async def asyncio_detailed(
    *,
    client: AuthenticatedClient | Client,
    project: str,
    preset: str | Unset = UNSET,

) -> Response[ProblemDetails | ProjectChecks]:
    """ Checks every texture set and layered material of a project at once, as GET /stacks/{id}/checks does
    one. A set open in a session is measured by the GPU engine; one nobody has open is judged on what
    its file says, its limits, its files and its bake, and says it was not measured. The export preset
    of a platform to check for, or none for the project's own.

    Args:
        project (str):
        preset (str | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | ProjectChecks]
     """


    kwargs = _get_kwargs(
        project=project,
preset=preset,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    *,
    client: AuthenticatedClient | Client,
    project: str,
    preset: str | Unset = UNSET,

) -> ProblemDetails | ProjectChecks | None:
    """ Checks every texture set and layered material of a project at once, as GET /stacks/{id}/checks does
    one. A set open in a session is measured by the GPU engine; one nobody has open is judged on what
    its file says, its limits, its files and its bake, and says it was not measured. The export preset
    of a platform to check for, or none for the project's own.

    Args:
        project (str):
        preset (str | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | ProjectChecks
     """


    return (await asyncio_detailed(
        client=client,
project=project,
preset=preset,

    )).parsed
